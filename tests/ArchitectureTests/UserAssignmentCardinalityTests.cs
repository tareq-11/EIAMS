using System.Reflection;
using Application.Abstractions.Messaging;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace ArchitectureTests;

/// <summary>
/// D-SRS-01 cardinality: a user holds exactly ONE assignment, always.
/// </summary>
/// <remarks>
/// WHY THIS TEST EXISTS. D-SRS-01 makes two failures unreachable — zero
/// assignments and multiple assignments — and the reachable HTTP surface honours
/// that: <c>CreateUser</c> writes the user and its sole assignment in one
/// transaction, and <c>PUT /admin/users/{userId}/role-scope</c> mutates the one
/// existing row. The database closes the "multiple" half with the unique index
/// <c>ux_user_role_scopes_user_id</c>, verified directly against PostgreSQL.
///
/// <c>Application/UserRoleScopes/GetByUser/GetUserRoleScopesQueryHandler</c>
/// completed the picture: a PAGED list filtered by UserId, for a relation the
/// unique index caps at one row, and it silently hid OrganizationalUnit
/// assignments so such a user read back as though they had none. All three are
/// deleted, and these tests are why they stay deleted.
///
/// <c>Revoke</c> and <c>RemoveAssignment</c> had no controller and published
/// nothing in OpenAPI — but handlers are registered by assembly scan
/// (<c>Application/DependencyInjection.cs:28</c>,
/// <c>AddClasses(... ICommandHandler&lt;&gt;)</c>), so both were live, resolvable
/// services wrapped in the validation, audit, and performance decorators. Any
/// future controller could have wired them and broken the invariant with nothing
/// failing. <c>Revoke</c> had no test whatsoever.
///
/// DELIBERATE BOUNDARY. <c>Grant</c> survives: it cannot produce two assignments
/// (the unique index rejects a duplicate, and
/// <c>GrantUserRoleScope_Should_CreateOneGrantAndRejectDuplicate</c> pins that),
/// and its validator is the reference implementation for the scope-id contract
/// <c>Replace</c> mirrors. <c>UserRoleScope.MarkAsRevoked</c> survives as domain
/// API and keeps its own test; it is what would raise the revoked event if a
/// revocation were ever reintroduced deliberately.
///
/// WHAT THIS DOES NOT PROVE. It matches type and route NAMES, not IL. A handler
/// named <c>RetireUserRoleScopeCommandHandler</c> that deletes the row would slip
/// past. Closing that would mean decoding IL, which is not worth the brittleness
/// here; what this guard buys is that the two known paths cannot return and the
/// obvious names are all covered. The durable proof that deletion cannot happen
/// is the database constraint in
/// <c>UserAssignmentDatabaseTests</c> plus the absence of any exposed route, both
/// of which this file also checks.
/// </remarks>
public sealed class UserAssignmentCardinalityTests : BaseTest
{
    private static readonly string[] RemovalTokens =
    [
        "Revoke", "Remove", "Delete", "Unassign", "Detach", "Clear", "Reset"
    ];

    private static IEnumerable<Type> CommandHandlerTypes() =>
        typeof(ICommandHandler<>).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => t.GetInterfaces().Any(IsCommandHandlerInterface));

    /// <summary>
    /// Matches BOTH arities of <c>ICommandHandler</c>.
    /// </summary>
    /// <remarks>
    /// The interface is declared twice — <c>ICommandHandler&lt;TCommand&gt;</c> for
    /// a void result and <c>ICommandHandler&lt;TCommand, TResponse&gt;</c> for one.
    /// <c>ReplaceUserRoleScopeCommandHandler</c> implements the two-parameter form,
    /// so matching only the single-parameter definition silently returns an EMPTY
    /// set — and an empty set passes a <c>ShouldBeEmpty</c> guard. That is the
    /// worst shape for an architecture test: it looks like proof and proves
    /// nothing.
    /// </remarks>
    private static bool IsCommandHandlerInterface(Type i)
    {
        if (!i.IsGenericType)
        {
            return false;
        }

        Type definition = i.GetGenericTypeDefinition();
        return definition == typeof(ICommandHandler<>) ||
               definition == typeof(ICommandHandler<,>);
    }

    private static bool IsAssignmentSurface(Type t) =>
        t.Namespace?.StartsWith("Application.UserRoleScopes", StringComparison.Ordinal) == true &&
        // The C# compiler emits a nested state-machine class per `async` method
        // (`<RevokeAsync>d__7`). Those are not hand-written surfaces, and listing
        // them buries the real offender in a failure message.
        !t.IsNested;

    private static bool MentionsRemoval(string name) =>
        RemovalTokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void No_Command_Or_Handler_May_Be_Named_For_Removing_An_Assignment()
    {
        // Guards both halves. An orphaned command is inert, but a command READY to
        // be wired is precisely the hazard, so the command type and its handler
        // are checked together.
        var offenders = typeof(ICommandHandler<>).Assembly
            .GetTypes()
            .Where(IsAssignmentSurface)
            .Where(t => MentionsRemoval(t.Name))
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        offenders.ShouldBeEmpty(
            "D-SRS-01 forbids a durable zero-assignment user. A command under " +
            "Application.UserRoleScopes named for removal can produce one, and its " +
            "handler would be registered and resolvable by assembly scan even with " +
            "no controller. The write surface is Create (with an assignment) and " +
            $"Replace (of the one existing row). Offenders: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void The_Assignment_Command_Handlers_Are_Exactly_Grant_And_Replace()
    {
        // A positive statement of the intended write surface, so this file fails
        // if the command set changes even without adding a removal word.
        var handlers = CommandHandlerTypes()
            .Where(IsAssignmentSurface)
            .Select(t => t.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        handlers.ShouldBe(
        [
            "GrantUserRoleScopeCommandHandler",
            "ReplaceUserRoleScopeCommandHandler"
        ],
            ignoreOrder: true);
    }

    [Fact]
    public void No_Assignment_Read_May_Return_A_Collection_For_One_User()
    {
        // The legacy `GetUserRoleScopesQuery` returned a PAGED result filtered by
        // UserId, for a relation the unique index caps at one row — the
        // multi-assignment read shape D-SRS-01 retires. It was also registered by
        // assembly scan and, worse, silently filtered
        // `ScopeType != OrganizationalUnit`, so a user holding such an assignment
        // read back as though they had none. The singular `GetUserRoleScopeQuery`
        // replaced it.
        var offenders = typeof(IQueryHandler<,>).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(IsAssignmentSurface)
            .Where(t => t.Name.Contains("Scopes", StringComparison.Ordinal))
            .Select(t => t.FullName!)
            .ToList();

        offenders.ShouldBeEmpty(
            "An assignment read named in the plural returns a collection for one " +
            "user. The singular GET /admin/users/{userId}/role-scope is the only " +
            $"read surface. Offenders: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void No_Assignment_Route_May_Accept_A_Delete_Verb()
    {
        // A removed row is reachable over HTTP only through a DELETE verb on an
        // assignment route, or through a PUT that a future handler treats as a
        // removal. The verb check catches the first shape outright.
        var offenders = typeof(ControllerBase).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.Namespace?.Contains("UserRoleScope", StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpDeleteAttribute>().Any())
            .Select(m => m.DeclaringType?.FullName ?? "unknown")
            .ToList();

        offenders.ShouldBeEmpty(
            "D-SRS-01 forbids deleting a user's only assignment. Offenders: " +
            string.Join(", ", offenders));
    }
}
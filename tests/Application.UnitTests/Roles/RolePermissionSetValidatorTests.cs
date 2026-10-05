using Application.Abstractions.Authorization;
using Application.Roles;
using Domain.Common;
using Domain.Roles;
using SharedKernel;

namespace Application.UnitTests.Roles;

/// <summary>
/// Covers the ruling-3 gate: a code that could never take effect for a role is rejected outright
/// rather than recorded as an inert grant.
/// </summary>
public sealed class RolePermissionSetValidatorTests
{
    private const string ActiveVocabulary = "dotted-v1";

    [Fact]
    public void Validate_ShouldRejectCodeOutsideTheActiveVocabulary()
    {
        IReadOnlyDictionary<string, RolePermissionSetValidator.CatalogEntry> catalog = BuildCatalog(
            ("catalog.view", [ScopeType.Enterprise, ScopeType.Warehouse]));

        IReadOnlyList<RolePermissionSetValidator.ResolvedPermission> resolved =
            RolePermissionSetValidator.Validate(
                ["catalog.view", "roles:manage"],
                [ScopeType.Enterprise],
                ActiveVocabulary,
                catalog,
                out Error error);

        error.Code.ShouldBe("Roles.UnknownPermissionCodes");
        resolved.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ShouldRejectCodeWithNoScopeOverlap()
    {
        // Warehouse-only permission offered to an Enterprise-only role: granting it would record a
        // grant that grants nothing at any scope the role can occupy.
        IReadOnlyDictionary<string, RolePermissionSetValidator.CatalogEntry> catalog = BuildCatalog(
            ("catalog.view", [ScopeType.Warehouse]));

        IReadOnlyList<RolePermissionSetValidator.ResolvedPermission> resolved =
            RolePermissionSetValidator.Validate(
                ["catalog.view"],
                [ScopeType.Enterprise],
                ActiveVocabulary,
                catalog,
                out Error error);

        error.Code.ShouldBe("Roles.PermissionCodesNotAllowedForRoleScopes");
        resolved.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ShouldRejectTheWholeSet_WhenOneCodeIsIncompatible()
    {
        // All-or-nothing: one bad code must not silently drop the good ones.
        IReadOnlyDictionary<string, RolePermissionSetValidator.CatalogEntry> catalog = BuildCatalog(
            ("catalog.view", [ScopeType.Enterprise, ScopeType.Warehouse]),
            ("catalog.manage", [ScopeType.Warehouse]));

        IReadOnlyList<RolePermissionSetValidator.ResolvedPermission> resolved =
            RolePermissionSetValidator.Validate(
                ["catalog.view", "catalog.manage"],
                [ScopeType.Enterprise],
                ActiveVocabulary,
                catalog,
                out Error error);

        error.Code.ShouldBe("Roles.PermissionCodesNotAllowedForRoleScopes");
        resolved.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ShouldRejectCodeWithNoAllowedScopeTypesAtAll()
    {
        // A catalogued but scopeless code cannot take effect anywhere, so it must be reported as
        // incompatible with an empty allowed-scope list rather than as an unknown code.
        IReadOnlyDictionary<string, RolePermissionSetValidator.CatalogEntry> catalog = BuildCatalog(
            ("catalog.view", []));

        RolePermissionSetValidator.Validate(
            ["catalog.view"],
            [ScopeType.Enterprise],
            ActiveVocabulary,
            catalog,
            out Error error);

        error.Code.ShouldBe("Roles.PermissionCodesNotAllowedForRoleScopes");
    }

    [Fact]
    public void Validate_ShouldAcceptAnyCodeThatOverlapsAtLeastOneAllowedScope()
    {
        IReadOnlyDictionary<string, RolePermissionSetValidator.CatalogEntry> catalog = BuildCatalog(
            ("catalog.view", [ScopeType.Enterprise, ScopeType.Warehouse]),
            ("catalog.manage", [ScopeType.Warehouse]));

        IReadOnlyList<RolePermissionSetValidator.ResolvedPermission> resolved =
            RolePermissionSetValidator.Validate(
                ["catalog.view", "catalog.manage"],
                [ScopeType.Enterprise, ScopeType.Warehouse],
                ActiveVocabulary,
                catalog,
                out Error error);

        error.Code.ShouldBe(Error.None.Code);
        resolved.Select(item => item.Code).ShouldBe(["catalog.manage", "catalog.view"], ignoreOrder: true);
    }

    [Fact]
    public void Validate_ShouldTreatDuplicatesAndBlanksAsOneEntry()
    {
        IReadOnlyDictionary<string, RolePermissionSetValidator.CatalogEntry> catalog = BuildCatalog(
            ("catalog.view", [ScopeType.Enterprise]));

        IReadOnlyList<RolePermissionSetValidator.ResolvedPermission> resolved =
            RolePermissionSetValidator.Validate(
                ["catalog.view", " catalog.view ", "   "],
                [ScopeType.Enterprise],
                ActiveVocabulary,
                catalog,
                out Error error);

        error.Code.ShouldBe(Error.None.Code);
        resolved.Count.ShouldBe(1);
    }

    [Fact]
    public void Validate_ShouldAcceptAnEmptySet_AsAValidRequestToRevokeEverything()
    {
        RolePermissionSetValidator.Validate(
            [],
            [ScopeType.Enterprise],
            ActiveVocabulary,
            BuildCatalog(("catalog.view", [ScopeType.Enterprise])),
            out Error error);

        error.Code.ShouldBe(Error.None.Code);
    }

    private static Dictionary<string, RolePermissionSetValidator.CatalogEntry> BuildCatalog(
        params (string Code, ScopeType[] Scopes)[] entries)
    {
        var byCode = new Dictionary<string, RolePermissionSetValidator.CatalogEntry>(StringComparer.Ordinal);

        foreach ((string code, ScopeType[] scopes) in entries)
        {
            byCode[code] = new RolePermissionSetValidator.CatalogEntry(
                Guid.NewGuid(),
                code,
                scopes);
        }

        return byCode;
    }
}
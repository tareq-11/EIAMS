using System.Reflection;
using Application.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Web.Api.Infrastructure;

namespace ArchitectureTests;

public sealed class EndpointAuthorizationTests : BaseTest
{
    private static readonly HashSet<string> AnonymousAllowlist =
    [
        "Web.Api.Controllers.Users.RecoverAdministratorController",
        "Web.Api.Controllers.Users.LoginController",
        "Web.Api.Controllers.Users.RefreshTokenController",
        "Web.Api.Controllers.Users.LogoutController"
    ];

    private static readonly HashSet<string> AuthenticatedUserAllowlist =
    [
        "Web.Api.Controllers.Users.GetSessionController"
    ];

    [Fact]
    public void Every_Protected_Controller_Action_Should_Require_A_Specific_Permission()
    {
        // Arrange
        var controllerTypes = PresentationAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        var unauthorizedEndpoints = new List<string>();

        // Act
        foreach (Type controller in controllerTypes)
        {
            if (AnonymousAllowlist.Contains(controller.FullName ?? string.Empty) ||
                AuthenticatedUserAllowlist.Contains(controller.FullName ?? string.Empty))
            {
                continue;
            }

            bool controllerHasPermission = controller
                .GetCustomAttributes(typeof(HasPermissionAttribute), true)
                .Length != 0;
            bool controllerHasAnyPermission = controller
                .GetCustomAttributes(typeof(HasAnyPermissionAttribute), true)
                .Length != 0;

            MethodInfo[] actionMethods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .ToArray();

            foreach (MethodInfo method in actionMethods)
            {
                bool methodHasPermission = method
                    .GetCustomAttributes(typeof(HasPermissionAttribute), true)
                    .Length != 0;
                bool methodHasAnyPermission = method
                    .GetCustomAttributes(typeof(HasAnyPermissionAttribute), true)
                    .Length != 0;

                if (!controllerHasPermission && !controllerHasAnyPermission && !methodHasPermission && !methodHasAnyPermission)
                {
                    unauthorizedEndpoints.Add($"{controller.FullName}.{method.Name}");
                }
            }
        }

        // Assert
        unauthorizedEndpoints.ShouldBeEmpty(
            $"The following controller actions do not require a specific permission: {string.Join(", ", unauthorizedEndpoints)}");
    }

    [Fact]
    public void Declared_Permission_Requirements_Should_Use_Only_Dotted_Vocabulary()
    {
        AuthorizeAttribute[] attributes = PresentationAssembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetCustomAttributes<AuthorizeAttribute>(true)
                .Concat(type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                    .SelectMany(method => method.GetCustomAttributes<AuthorizeAttribute>(true))))
            .Where(attribute => attribute is HasPermissionAttribute or HasAnyPermissionAttribute)
            .ToArray();

        foreach (AuthorizeAttribute attribute in attributes)
        {
            string[] permissions = attribute.Policy?.StartsWith("any:", StringComparison.Ordinal) == true
                ? attribute.Policy[4..].Split('|', StringSplitOptions.RemoveEmptyEntries)
                : [attribute.Policy ?? string.Empty];

            permissions.ShouldNotBeEmpty();
            foreach (string permission in permissions)
            {
                permission.ShouldNotContain(":");
                PermissionVocabulary.DottedV1Codes.ShouldContain(permission);
            }
        }
    }

    [Fact]
    public void Authenticated_User_Allowlist_Controllers_Should_Require_Authentication()
    {
        // Arrange
        var controllerTypes = PresentationAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => AuthenticatedUserAllowlist.Contains(t.FullName ?? string.Empty))
            .ToList();

        // Act & Assert
        foreach (Type controller in controllerTypes)
        {
            bool hasAuthorize = controller
                .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .Length != 0;

            hasAuthorize.ShouldBeTrue($"Controller {controller.FullName} must have [Authorize] attribute");
        }
    }

    [Fact]
    public void Anonymous_Allowlist_Controllers_Should_Declare_AllowAnonymous_Explicitly()
    {
        var controllerTypes = PresentationAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => AnonymousAllowlist.Contains(t.FullName ?? string.Empty))
            .ToList();

        foreach (Type controller in controllerTypes)
        {
            bool hasAllowAnonymous = controller
                .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true)
                .Length != 0;

            hasAllowAnonymous.ShouldBeTrue($"Controller {controller.FullName} must declare [AllowAnonymous] explicitly");
        }
    }
}

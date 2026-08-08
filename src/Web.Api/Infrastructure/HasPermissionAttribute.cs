using Microsoft.AspNetCore.Authorization;

namespace Web.Api.Infrastructure;

/// <summary>
/// Controller-action equivalent of the old Minimal API <c>.HasPermission(permission)</c> extension -
/// resolves through the same <c>PermissionAuthorizationPolicyProvider</c>, which turns a permission
/// code into an authorization policy on demand.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class HasPermissionAttribute(string permission) : AuthorizeAttribute(permission);

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Web.Api.Extensions;

/// <summary>Counts final security-relevant HTTP outcomes without request, identity, or resource labels.</summary>
internal static class SecurityOutcomeMetricsExtensions
{
    internal const string MeterName = "EIAMS.SecurityOperations";
    internal const string OutcomesInstrumentName = "eiam.security.http.outcomes";
    internal const string OperationsInstrumentName = "eiam.security.operations";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>(OutcomesInstrumentName);
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>(OperationsInstrumentName);

    internal static IApplicationBuilder UseSecurityOutcomeMetrics(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        string? eventType = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() is { } action &&
            TryGetOperationEventType(action.ControllerTypeInfo.Name, out string classifiedEventType)
            ? classifiedEventType
            : null;

        await next(context);
        RecordFinalSecurityOutcome(context.Response.StatusCode);

        if (eventType is not null)
        {
            RecordOperation(eventType, ToOperationOutcome(context.Response.StatusCode));
        }
    });

    // Kept separate from ASP.NET Core's built-in request-duration metric. These are security
    // outcomes only; neither the route nor any caller-supplied value is exported as a tag.
    internal static void RecordFinalSecurityOutcome(int statusCode)
    {
        string? outcome = statusCode switch
        {
            StatusCodes.Status401Unauthorized => "unauthorized",
            StatusCodes.Status403Forbidden => "forbidden",
            StatusCodes.Status429TooManyRequests => "rate_limited",
            _ => null
        };

        if (outcome is not null)
        {
            Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
        }
    }

    internal static void RecordOperation(string eventType, string outcome) =>
        Operations.Add(1, OperationTags(eventType, outcome));

    private static TagList OperationTags(string eventType, string outcome)
    {
        if (!IsKnownEventType(eventType))
        {
            throw new ArgumentOutOfRangeException(nameof(eventType));
        }

        if (outcome is not ("succeeded" or "rejected" or "failed"))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        return new TagList { { "event_type", eventType }, { "outcome", outcome } };
    }

    private static string ToOperationOutcome(int statusCode) => statusCode switch
    {
        >= StatusCodes.Status200OK and < StatusCodes.Status300MultipleChoices => "succeeded",
        >= StatusCodes.Status400BadRequest and < StatusCodes.Status500InternalServerError => "rejected",
        _ => "failed"
    };

    private static bool TryGetOperationEventType(string controllerName, out string eventType)
    {
        eventType = controllerName switch
        {
            "RegisterController" => "administrator_bootstrap",
            "RecoverAdministratorController" => "administrator_recovery",
            "CreateUserController" => "administrator_user_create",
            "UpdateUserController" => "administrator_user_update",
            "LinkEmployeeController" => "administrator_user_link_employee",
            "CreateRoleController" => "administrator_role_create",
            "UpdateRoleController" => "administrator_role_update",
            "AssignPermissionController" => "administrator_role_permission_assign",
            "RemovePermissionController" => "administrator_role_permission_remove",
            "GrantController" => "administrator_role_scope_grant",
            "ReplaceAssignmentController" => "administrator_role_scope_replace",
            "RevokeController" => "administrator_role_scope_revoke",
            "RemoveAssignmentController" => "administrator_role_scope_remove",
            _ => string.Empty
        };

        return eventType.Length > 0;
    }

    private static bool IsKnownEventType(string eventType) => eventType is
        "administrator_bootstrap" or
        "administrator_recovery" or
        "administrator_user_create" or
        "administrator_user_update" or
        "administrator_user_link_employee" or
        "administrator_role_create" or
        "administrator_role_update" or
        "administrator_role_permission_assign" or
        "administrator_role_permission_remove" or
        "administrator_role_scope_grant" or
        "administrator_role_scope_replace" or
        "administrator_role_scope_revoke" or
        "administrator_role_scope_remove";
}

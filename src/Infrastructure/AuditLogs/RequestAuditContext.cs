using System.Net;
using Application.Abstractions.Audit;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.AuditLogs;

internal sealed class RequestAuditContext(IHttpContextAccessor httpContextAccessor) : IRequestAuditContext
{
    public string? GetRequestId()
    {
        HttpContext? context = httpContextAccessor.HttpContext;

        if (context is null)
        {
            return null;
        }

        return string.IsNullOrEmpty(context.TraceIdentifier) ? null : context.TraceIdentifier;
    }

    public string? GetClientIpAddress()
    {
        IPAddress? remoteIpAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;

        if (remoteIpAddress is null)
        {
            return null;
        }

        return remoteIpAddress.IsIPv4MappedToIPv6
            ? remoteIpAddress.MapToIPv4().ToString()
            : remoteIpAddress.ToString();
    }
}

using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;

namespace Web.Api.Extensions;

internal static class ForwardedHeadersExtensions
{
    internal static IServiceCollection AddForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string[] knownProxyValues =
            configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        string[] knownNetworkValues =
            configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            if (knownProxyValues.Length == 0 && knownNetworkValues.Length == 0)
            {
                // Empty known-proxy collections mean "trust every proxy" in the middleware.
                // Disable forwarded-header processing instead when no trusted peer is configured.
                options.ForwardedHeaders = ForwardedHeaders.None;
                return;
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                       ForwardedHeaders.XForwardedProto |
                                       ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = 1;
            foreach (string allowedHost in (configuration["AllowedHosts"] ?? string.Empty)
                         .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                         .Where(host => !host.Contains('*', StringComparison.Ordinal)))
            {
                options.AllowedHosts.Add(allowedHost);
            }

            foreach (string proxy in knownProxyValues)
            {
                if (!IPAddress.TryParse(proxy, out IPAddress? address))
                {
                    throw new InvalidOperationException(
                        $"Invalid IP address '{proxy}' in 'ForwardedHeaders:KnownProxies'. " +
                        "Provide a valid IPv4/IPv6 address.");
                }

                options.KnownProxies.Add(address);
            }

            foreach (string network in knownNetworkValues)
            {
                if (!IPNetwork.TryParse(network, out IPNetwork parsedNetwork))
                {
                    throw new InvalidOperationException(
                        $"Invalid CIDR network '{network}' in 'ForwardedHeaders:KnownNetworks'. " +
                        "Provide a valid network in 'ip/prefix' form (e.g. 10.0.0.0/8).");
                }

                options.KnownIPNetworks.Add(parsedNetwork);
            }
        });

        return services;
    }
}

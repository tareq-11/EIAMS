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
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Only explicitly configured proxies are trusted. With an empty configuration no
            // proxy is trusted, so forwarded headers are ignored and the direct peer is used.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (string proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            {
                if (!IPAddress.TryParse(proxy, out IPAddress? address))
                {
                    throw new InvalidOperationException(
                        $"Invalid IP address '{proxy}' in 'ForwardedHeaders:KnownProxies'. " +
                        "Provide a valid IPv4/IPv6 address.");
                }

                options.KnownProxies.Add(address);
            }

            foreach (string network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
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

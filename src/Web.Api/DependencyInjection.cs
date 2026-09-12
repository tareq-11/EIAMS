using System.Text.Json.Serialization;
using Application.Abstractions.Storage;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Web.Api.Infrastructure;

namespace Web.Api;

public static class DependencyInjection
{
    private const long MultipartOverheadBytes = 64 * 1024;

    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        long maxFileSize = configuration.GetValue<long?>(
                $"{AttachmentStorageOptions.SectionName}:MaxFileSizeInBytes")
            ?? new AttachmentStorageOptions().MaxFileSizeInBytes;
        long multipartBodyLimit = maxFileSize > long.MaxValue - MultipartOverheadBytes
            ? long.MaxValue
            : maxFileSize + MultipartOverheadBytes;

        services.Configure<FormOptions>(options =>
            options.MultipartBodyLengthLimit = multipartBodyLimit);

        services.Configure<KestrelServerOptions>(options =>
            options.Limits.MaxRequestBodySize = multipartBodyLimit);

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        RefreshTokenTransportOptions refreshTokenTransportOptions = configuration
            .GetSection(RefreshTokenTransportOptions.SectionName)
            .Get<RefreshTokenTransportOptions>() ?? new RefreshTokenTransportOptions();
        if (refreshTokenTransportOptions.AllowedCookieOrigins.Length == 0)
        {
            refreshTokenTransportOptions = new RefreshTokenTransportOptions
            {
                AllowRequestBody = refreshTokenTransportOptions.AllowRequestBody,
                IncludeInResponseBody = refreshTokenTransportOptions.IncludeInResponseBody,
                AllowedCookieOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []
            };
        }

        services.AddSingleton(refreshTokenTransportOptions);
        services.AddSingleton<RefreshTokenTransport>();

        services
            .AddControllers(options =>
                options.Conventions.Insert(0, new ApiVersionRouteConvention("api/v1")))
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter(allowIntegerValues: false));
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = ApiProblemDetails.CreateValidationResponse;
            });

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}

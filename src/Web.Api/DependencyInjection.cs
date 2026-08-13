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

        services
            .AddControllers()
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = ApiProblemDetails.CreateValidationResponse;
            });

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }
}

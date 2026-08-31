using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;

namespace Web.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddSwaggerGenWithAuth(this IServiceCollection services)
    {
        services.AddSwaggerGen(static o =>
        {
            o.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
            {
                Title = "EIAMS Backend API",
                Version = "v1",
                Description = "Authoritative Clean Architecture Backend API for Enterprise Inventory and Asset Management System"
            });

            o.CustomSchemaIds(id => id.FullName!.Replace('+', '-'));
            o.CustomOperationIds(static apiDescription =>
            {
                if (apiDescription.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
                {
                    return null;
                }

                string controllerName = actionDescriptor.ControllerTypeInfo.FullName!
                    .Replace("Web.Api.Controllers.", string.Empty, StringComparison.Ordinal)
                    .Replace("Controller", string.Empty, StringComparison.Ordinal)
                    .Replace('.', '_');
                string? httpMethod = apiDescription.HttpMethod;
                string route = (apiDescription.RelativePath ?? "root")
                    .Replace("{", string.Empty, StringComparison.Ordinal)
                    .Replace("}", string.Empty, StringComparison.Ordinal)
                    .Replace('/', '_')
                    .Replace('-', '_');

                return string.IsNullOrWhiteSpace(httpMethod)
                    ? $"{controllerName}_{route}"
                    : $"{controllerName}_{httpMethod}_{route}";
            });

            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "JWT Authentication",
                Description = "Enter your JWT token in this field",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
                BearerFormat = "JWT"
            };

            o.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);

            o.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document),
                    []
                }
            });
        });

        return services;
    }
}

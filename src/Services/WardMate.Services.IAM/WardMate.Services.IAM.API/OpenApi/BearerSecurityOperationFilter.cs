using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WardMate.Services.IAM.API.OpenApi;

public sealed class BearerSecurityOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<WardMate.Services.IAM.API.Security.BrowserAuthProtectionAttribute>().Any())
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-CSRF-Protection", In = ParameterLocation.Header, Required = true,
                Description = "Gửi giá trị 1 để bảo vệ thao tác dùng cookie khỏi CSRF.",
                Schema = new OpenApiSchema { Type = "string", Default = new Microsoft.OpenApi.Any.OpenApiString("1") }
            });
            operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Thiếu hoặc sai header bảo vệ CSRF." });
        }
        if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any()) return;
        operation.Security = [new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
        }];
    }
}

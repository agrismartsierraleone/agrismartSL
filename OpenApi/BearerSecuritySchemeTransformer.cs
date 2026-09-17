using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace AgrismartAPI.OpenApi;

internal sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var schemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (schemes.All(scheme => scheme.Name != "Bearer"))
        {
            return;
        }

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the JWT returned by POST /auth/login or /auth/register."
        };

        var apiDescriptions = context.DescriptionGroups
            .SelectMany(group => group.Items)
            .ToList();

        foreach (var (path, pathItem) in document.Paths)
        {
            foreach (var (method, operation) in pathItem.Operations!)
            {
                var matching = apiDescriptions.FirstOrDefault(description =>
                    string.Equals("/" + description.RelativePath!.Split('?')[0].TrimEnd('/'), path.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(description.HttpMethod, method.ToString(), StringComparison.OrdinalIgnoreCase));

                var endpointMetadata = matching?.ActionDescriptor.EndpointMetadata ?? [];
                var requiresAuth = endpointMetadata.OfType<IAuthorizeData>().Any()
                                    && !endpointMetadata.OfType<IAllowAnonymous>().Any();

                if (!requiresAuth)
                {
                    continue;
                }

                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
            }
        }
    }
}

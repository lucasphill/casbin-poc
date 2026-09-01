using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace casbin_poc
{
    internal sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider schemes) : IOpenApiDocumentTransformer
    {
        public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            var all = await schemes.GetAllSchemesAsync();
            if (!all.Any(s => s.Name == "Bearer"))
                return;

            var bearerScheme = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."
            };

            document.Components ??= new OpenApiComponents();

            // Em .NET 10 você pode usar AddComponent (depende da versão do Microsoft.OpenApi)
            document.AddComponent("Bearer", bearerScheme);

            var requirement = new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
            };

            foreach (var path in document.Paths.Values)
                foreach (var op in path.Operations.Values)
                {
                    op.Security ??= new List<OpenApiSecurityRequirement>();
                    op.Security.Add(requirement);
                }
        }
    }
}

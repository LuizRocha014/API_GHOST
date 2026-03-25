using System.Reflection;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Swagger;

internal static class SwaggerXmlCommentsExtensions
{
    internal static void IncludeAssemblyXmlComments(this SwaggerGenOptions swagger, Assembly assembly)
    {
        var xmlFile = $"{assembly.GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (!File.Exists(xmlPath))
            return;

        swagger.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
}

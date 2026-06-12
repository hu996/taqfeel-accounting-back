using System.Text;

namespace AccountingSaaS.Api.Swagger;

public static class SwaggerSchemaNameHelper
{
    public static string GetSchemaId(Type type)
    {
        if (!type.IsGenericType)
        {
            return (type.FullName ?? type.Name).Replace("+", ".");
        }

        var genericName = type.Name[..type.Name.IndexOf('`')];
        var arguments = string.Join("And", type.GetGenericArguments().Select(x => x.Name));
        var namespacePrefix = type.Namespace is null ? string.Empty : $"{type.Namespace}.";
        return Sanitize($"{namespacePrefix}{genericName}Of{arguments}");
    }

    private static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) || c is '.' or '_' ? c : '_');
        }

        return builder.ToString();
    }
}

namespace AccountingSaaS.Api.Swagger;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TenantContextAttribute(bool required, string description = "") : Attribute
{
    public bool Required { get; } = required;
    public string Description { get; } = description;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiredPermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}

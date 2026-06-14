using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AccountingSaaS.Application.Authorization;

public sealed class PermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    private const string PermissionPrefix = "Permission:";
    private const string ModulePrefix = "Module:";

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(
                PermissionPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName[PermissionPrefix.Length..];
            var permissionPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(permissionPolicy);
        }

        if (policyName.StartsWith(
                ModulePrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            var module = policyName[ModulePrefix.Length..];
            var modulePolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new ModuleRequirement(module))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(modulePolicy);
        }

        return base.GetPolicyAsync(policyName);
    }
}

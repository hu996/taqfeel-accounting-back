using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace AccountingSaaS.Application.Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}

public sealed class ModuleRequirement : IAuthorizationRequirement
{
    public ModuleRequirement(string module)
    {
        Module = module;
    }

    public string Module { get; }
}

public sealed class PermissionAuthorizationHandler :
    AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentSessionService currentSession;

    public PermissionAuthorizationHandler(ICurrentSessionService currentSession)
    {
        this.currentSession = currentSession;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (currentSession.IsAuthenticated &&
            (currentSession.IsSuperAdmin ||
             currentSession.HasPermission(requirement.Permission)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class ModuleAuthorizationHandler :
    AuthorizationHandler<ModuleRequirement>
{
    private readonly ICurrentSessionService currentSession;

    public ModuleAuthorizationHandler(ICurrentSessionService currentSession)
    {
        this.currentSession = currentSession;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ModuleRequirement requirement)
    {
        if (currentSession.IsAuthenticated &&
            (currentSession.IsSuperAdmin ||
             currentSession.HasModule(requirement.Module)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
        : base($"Permission:{permission}")
    {
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireModuleAttribute : AuthorizeAttribute
{
    public RequireModuleAttribute(string module)
        : base($"Module:{module}")
    {
    }
}

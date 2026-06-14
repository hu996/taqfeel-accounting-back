using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Constants;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public string CreateAccessToken(LoginResponseDto context)
    {
        var claims = new List<Claim>
        {
            new(SessionClaimNames.UserId, context.UserId.ToString()),
            new(SessionClaimNames.UserName, context.UserName),
            new(SessionClaimNames.Email, context.Email),
            new(SessionClaimNames.TenantId, context.TenantId.ToString()),
            new(SessionClaimNames.CompanyId, context.CompanyId.ToString()),
            new(SessionClaimNames.CompanyCode, context.CompanyCode),
            new(SessionClaimNames.CompanyNameAr, context.CompanyNameAr),
            new(SessionClaimNames.CompanyNameEn, context.CompanyNameEn),
            new(SessionClaimNames.ActiveFinancialYearId, context.ActiveFinancialYearId.ToString()),
            new(SessionClaimNames.ActiveFinancialYearCode, context.ActiveFinancialYearCode),
            new(SessionClaimNames.ActiveFinancialYearName, context.ActiveFinancialYearName),
            new(SessionClaimNames.IsSuperAdmin, context.IsSuperAdmin.ToString().ToLowerInvariant()),
            new(SessionClaimNames.Language, context.Language),
            new(JwtRegisteredClaimNames.Sub, context.UserId.ToString())
        };

        AddOptionalClaim(claims, SessionClaimNames.EmployeeId, context.EmployeeId?.ToString());
        AddOptionalClaim(claims, SessionClaimNames.EmployeeCode, context.EmployeeCode);
        AddOptionalClaim(claims, SessionClaimNames.EmployeeName, context.EmployeeName);
        AddOptionalClaim(claims, SessionClaimNames.CompanyLogoUrl, context.CompanyLogoUrl);
        AddOptionalClaim(claims, SessionClaimNames.ActiveAccountingPeriodId, context.ActiveAccountingPeriodId?.ToString());
        AddOptionalClaim(claims, SessionClaimNames.ActiveAccountingPeriodCode, context.ActiveAccountingPeriodCode);
        AddOptionalClaim(claims, SessionClaimNames.ActiveAccountingPeriodName, context.ActiveAccountingPeriodName);
        AddOptionalClaim(claims, SessionClaimNames.BranchId, context.BranchId?.ToString());
        AddOptionalClaim(claims, SessionClaimNames.DepartmentId, context.DepartmentId?.ToString());

        claims.AddRange(context.Roles.Select(role => new Claim(SessionClaimNames.Role, role)));
        claims.AddRange(context.Permissions.Select(permission => new Claim(SessionClaimNames.Permission, permission)));
        claims.AddRange(context.EnabledModules.Select(module => new Claim(SessionClaimNames.Module, module)));

        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("Jwt:Secret is missing.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: context.ExpiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void AddOptionalClaim(List<Claim> claims, string claimType, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            claims.Add(new Claim(claimType, value));
        }
    }
}

public sealed class SessionContextFactory : ISessionContextFactory
{
    private readonly AppDbContext dbContext;
    private readonly UserManager<ApplicationUser> userManager;
    private readonly IConfiguration configuration;
    private readonly IJwtTokenService jwtTokenService;

    public SessionContextFactory(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        IJwtTokenService jwtTokenService)
    {
        this.dbContext = dbContext;
        this.userManager = userManager;
        this.configuration = configuration;
        this.jwtTokenService = jwtTokenService;
    }

    public async Task<LoginResponseDto> CreateAsync(
        Guid userId,
        Guid? financialYearId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                candidate => candidate.Id == userId && !candidate.IsDeleted && candidate.IsActive,
                cancellationToken);
        if (user is null)
        {
            throw new UnauthorizedAccessException("User is inactive or was not found.");
        }

        if (!user.TenantId.HasValue || user.TenantId.Value == Guid.Empty)
        {
            throw new UnauthorizedAccessException("The user is not assigned to a company.");
        }

        var tenantId = user.TenantId.Value;
        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                company => company.Id == tenantId && !company.IsDeleted && company.IsActive,
                cancellationToken);
        if (tenant is null)
        {
            throw new UnauthorizedAccessException("The company is inactive or was not found.");
        }

        var activeYear = await ResolveFinancialYearAsync(
            tenantId,
            financialYearId ?? user.ActiveFinancialYearId,
            cancellationToken);
        if (activeYear is null)
        {
            throw new InvalidOperationException("No active financial year found for this company.");
        }

        var activePeriod = await ResolveAccountingPeriodAsync(
            tenantId,
            activeYear.Id,
            cancellationToken);

        Employee? employee = null;
        if (user.EmployeeId.HasValue)
        {
            employee = await dbContext.Employees
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    item => item.Id == user.EmployeeId.Value &&
                            item.TenantId == tenantId &&
                            !item.IsDeleted &&
                            item.IsActive,
                    cancellationToken);
        }

        var roles = (await userManager.GetRolesAsync(user))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var isSuperAdmin = roles.Contains(Roles.SuperAdmin, StringComparer.OrdinalIgnoreCase);
        var permissions = await GetPermissionsAsync(roles, isSuperAdmin, cancellationToken);
        var modules = await dbContext.TenantModules
            .IgnoreQueryFilters()
            .Where(module =>
                module.TenantId == tenantId &&
                !module.IsDeleted &&
                module.IsEnabled)
            .Select(module => module.ModuleKey)
            .Distinct()
            .ToListAsync(cancellationToken);

        var companyNameAr = string.IsNullOrWhiteSpace(tenant.CompanyNameAr)
            ? tenant.CompanyName
            : tenant.CompanyNameAr;
        var companyNameEn = string.IsNullOrWhiteSpace(tenant.CompanyNameEn)
            ? tenant.CompanyName
            : tenant.CompanyNameEn;
        var companyCode = string.IsNullOrWhiteSpace(tenant.CompanyCode)
            ? $"COMP-{tenant.TenantNo:000}"
            : tenant.CompanyCode;
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(
            configuration.GetValue<int>("Jwt:AccessTokenMinutes", 15));

        var result = new LoginResponseDto
        {
            ExpiresAt = expiresAt,
            UserId = user.Id,
            UserName = user.UserName ?? user.Email ?? user.Id.ToString(),
            Email = user.Email ?? string.Empty,
            EmployeeId = employee?.Id,
            EmployeeCode = employee?.Code,
            EmployeeName = employee is null
                ? null
                : string.IsNullOrWhiteSpace(employee.NameAr) ? employee.NameEn : employee.NameAr,
            TenantId = tenant.Id,
            CompanyId = tenant.Id,
            CompanyCode = companyCode,
            CompanyNameAr = companyNameAr,
            CompanyNameEn = companyNameEn,
            CompanyLogoUrl = tenant.CompanyLogoUrl,
            ActiveFinancialYearId = activeYear.Id,
            ActiveFinancialYearCode = string.IsNullOrWhiteSpace(activeYear.YearCode)
                ? activeYear.YearName
                : activeYear.YearCode,
            ActiveFinancialYearName = activeYear.YearName,
            ActiveAccountingPeriodId = activePeriod?.Id,
            ActiveAccountingPeriodCode = activePeriod?.PeriodCode,
            ActiveAccountingPeriodName = activePeriod?.PeriodName,
            BranchId = employee?.BranchId,
            DepartmentId = employee?.DepartmentId,
            IsSuperAdmin = isSuperAdmin,
            MustChangePassword = user.MustChangePassword,
            Language = string.IsNullOrWhiteSpace(user.Language) ? "ar" : user.Language,
            Roles = roles,
            Permissions = permissions,
            EnabledModules = modules
        };
        result.Token = jwtTokenService.CreateAccessToken(result);
        return result;
    }

    private async Task<FinancialYear?> ResolveFinancialYearAsync(
        Guid tenantId,
        Guid? requestedFinancialYearId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.FinancialYears
            .IgnoreQueryFilters()
            .Where(year =>
                year.TenantId == tenantId &&
                !year.IsDeleted &&
                year.Status == FinancialYearStatus.Open);

        if (requestedFinancialYearId.HasValue)
        {
            return await query.FirstOrDefaultAsync(
                year => year.Id == requestedFinancialYearId.Value,
                cancellationToken);
        }

        return await query
            .OrderByDescending(year => year.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<AccountingPeriod?> ResolveAccountingPeriodAsync(
        Guid tenantId,
        Guid financialYearId,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = dbContext.AccountingPeriods
            .IgnoreQueryFilters()
            .Where(period =>
                period.TenantId == tenantId &&
                period.FinancialYearId == financialYearId &&
                !period.IsDeleted &&
                period.Status == AccountingPeriodStatus.Open);

        return await query.FirstOrDefaultAsync(
            period => period.StartDate <= today && period.EndDate >= today,
            cancellationToken);
    }

    private async Task<List<string>> GetPermissionsAsync(
        IReadOnlyList<string> roles,
        bool isSuperAdmin,
        CancellationToken cancellationToken)
    {
        if (isSuperAdmin)
        {
            return Permissions.All.ToList();
        }

        return await dbContext.RolePermissions
            .Where(item => roles.Contains(item.Role.Name!))
            .Select(item => item.Permission.Name)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}

public sealed class SessionService : ISessionService
{
    private readonly AppDbContext dbContext;
    private readonly ICurrentSessionService currentSession;
    private readonly ISessionContextFactory sessionContextFactory;

    public SessionService(
        AppDbContext dbContext,
        ICurrentSessionService currentSession,
        ISessionContextFactory sessionContextFactory)
    {
        this.dbContext = dbContext;
        this.currentSession = currentSession;
        this.sessionContextFactory = sessionContextFactory;
    }

    public Task<BaseResponseDto<SessionContextDto>> GetContextAsync(
        CancellationToken cancellationToken)
    {
        var context = new SessionContextDto
        {
            UserId = currentSession.UserId,
            UserName = currentSession.UserName,
            Email = currentSession.Email,
            EmployeeId = currentSession.EmployeeId,
            EmployeeCode = currentSession.EmployeeCode,
            EmployeeName = currentSession.EmployeeName,
            TenantId = currentSession.TenantId,
            CompanyId = currentSession.CompanyId,
            CompanyCode = currentSession.CompanyCode,
            CompanyNameAr = currentSession.CompanyNameAr,
            CompanyNameEn = currentSession.CompanyNameEn,
            CompanyLogoUrl = currentSession.CompanyLogoUrl,
            ActiveFinancialYearId = currentSession.ActiveFinancialYearId,
            ActiveFinancialYearCode = currentSession.ActiveFinancialYearCode,
            ActiveFinancialYearName = currentSession.ActiveFinancialYearName,
            ActiveAccountingPeriodId = currentSession.ActiveAccountingPeriodId,
            ActiveAccountingPeriodCode = currentSession.ActiveAccountingPeriodCode,
            ActiveAccountingPeriodName = currentSession.ActiveAccountingPeriodName,
            BranchId = currentSession.BranchId,
            DepartmentId = currentSession.DepartmentId,
            IsSuperAdmin = currentSession.IsSuperAdmin,
            Language = currentSession.Language,
            Roles = currentSession.Roles.ToList(),
            Permissions = currentSession.Permissions.ToList(),
            EnabledModules = currentSession.EnabledModules.ToList()
        };

        return Task.FromResult(BaseResponseDto<SessionContextDto>.Ok(context));
    }

    public async Task<BaseResponseDto<LoginResponseDto>> SwitchActiveFinancialYearAsync(
        SwitchActiveFinancialYearRequest request,
        CancellationToken cancellationToken)
    {
        var yearExists = await dbContext.FinancialYears.AnyAsync(
            year =>
                year.Id == request.FinancialYearId &&
                year.TenantId == currentSession.TenantId &&
                year.Status == FinancialYearStatus.Open,
            cancellationToken);
        if (!yearExists)
        {
            return BaseResponseDto<LoginResponseDto>.Fail(
                "The financial year is not active or does not belong to this company.");
        }

        var user = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                item => item.Id == currentSession.UserId &&
                        item.TenantId == currentSession.TenantId &&
                        !item.IsDeleted &&
                        item.IsActive,
                cancellationToken);
        if (user is null)
        {
            return BaseResponseDto<LoginResponseDto>.NotFound("User was not found.");
        }

        user.ActiveFinancialYearId = request.FinancialYearId;
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await sessionContextFactory.CreateAsync(
            user.Id,
            request.FinancialYearId,
            cancellationToken);
        return BaseResponseDto<LoginResponseDto>.Ok(
            response,
            "Active financial year switched successfully.");
    }
}

using System.Text;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AccountingSaaS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddInfrastructureServices(configuration);
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = 5;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var key = Encoding.UTF8.GetBytes(configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is missing."));
        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = SessionClaimNames.UserName,
                    RoleClaimType = SessionClaimNames.Role
                };
                options.MapInboundClaims = false;
            });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentSessionService, CurrentSessionService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ISessionContextFactory, SessionContextFactory>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITenantAccessService, TenantAccessService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<INumberSequenceService, NumberSequenceService>();
        services.AddScoped<IWorkflowAccessService, WorkflowAccessService>();
        services.AddScoped<IFinancialYearService, FinancialYearService>();
        services.AddScoped<IAccountingPeriodService, AccountingPeriodService>();
        services.AddScoped<IChartOfAccountsService, ChartOfAccountsService>();
        services.AddScoped<ICostCenterService, CostCenterService>();
        services.AddScoped<IJournalEntryService, JournalEntryService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IClosingChecklistService, ClosingChecklistService>();
        services.AddScoped<IClosingTaskService, ClosingTaskService>();
        services.AddScoped<IClosingSubmissionService, ClosingSubmissionService>();
        services.AddScoped<IAccountingReportService, AccountingReportService>();
        services.AddScoped<IExcelReaderService, ExcelReaderService>();
        services.AddScoped<IImportService, ImportService>();
        services.AddScoped<IImportHandlerFactory, ImportHandlerFactory>();
        services.AddScoped<IImportHandler, ChartOfAccountsImportHandler>();
        services.AddScoped<IImportHandler, OpeningBalancesImportHandler>();
        services.AddScoped<IImportHandler, CostCentersImportHandler>();
        services.AddScoped<IImportHandler, JournalEntriesImportHandler>();
        services.AddScoped<IImportHandler, CustomersImportHandler>();
        services.AddScoped<IImportHandler, SuppliersImportHandler>();
        services.AddScoped<IImportHandler, BankTransactionsImportHandler>();
        services.AddScoped<ILookupService, LookupService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IDynamicWorkflowService, DynamicWorkflowService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IUniversalSearchService, UniversalSearchService>();
        services.AddScoped<ICustomFieldService, CustomFieldService>();
        services.AddScoped<IDocumentNumberService, DocumentNumberService>();
        services.AddScoped<IOpeningBalanceService, OpeningBalanceService>();
        services.AddScoped<IBankReconciliationService, BankReconciliationService>();
        services.AddScoped<IFixedAssetService, FixedAssetService>();
        services.AddScoped<IRecurringJournalService, RecurringJournalService>();
        services.AddScoped<IClosingAssistantService, ClosingAssistantService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportBuilderService, ReportBuilderService>();
        services.AddScoped<IBusinessPartyService, BusinessPartyService>();
        return services;
    }
}

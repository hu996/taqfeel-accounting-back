using AccountingSaaS.Domain.Constants;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AccountingSaaS.Infrastructure.Persistence;

public static class SeedData
{
    public static async Task SeedAsync(AppDbContext dbContext, UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, IConfiguration configuration, IHostEnvironment environment)
    {
        await InitializeDatabaseAsync(dbContext, environment);

        foreach (var roleName in new[] { Roles.SuperAdmin, Roles.AccountingOfficeAdmin, Roles.Accountant, Roles.Reviewer, Roles.CompanyOwner, Roles.CompanyUser })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = roleName, NormalizedName = roleName.ToUpperInvariant() });
            }
        }

        foreach (var permissionName in Permissions.All)
        {
            if (!await dbContext.Permissions.AnyAsync(x => x.Name == permissionName))
            {
                dbContext.Permissions.Add(new Permission
                {
                    Id = Guid.NewGuid(),
                    Name = permissionName,
                    Description = permissionName,
                    Category = permissionName.Split('.')[0]
                });
            }
        }

        await dbContext.SaveChangesAsync();
        await SeedRolePermissionsAsync(dbContext);
        await SeedSuperAdminAsync(userManager, configuration);
    }

    private static async Task InitializeDatabaseAsync(AppDbContext dbContext, IHostEnvironment environment)
    {
        var migrations = dbContext.Database.GetMigrations();
        if (migrations.Any())
        {
            await dbContext.Database.MigrateAsync();
            return;
        }

        if (environment.IsProduction())
        {
            throw new InvalidOperationException("No EF Core migrations were found. Create and apply migrations before running in production.");
        }

        await dbContext.Database.EnsureCreatedAsync();
    }

    private static async Task SeedRolePermissionsAsync(AppDbContext dbContext)
    {
        var roles = await dbContext.Roles.ToDictionaryAsync(x => x.Name!);
        var permissions = await dbContext.Permissions.ToDictionaryAsync(x => x.Name);

        var map = new Dictionary<string, IEnumerable<string>>
        {
            [Roles.SuperAdmin] = Permissions.All,
            [Roles.AccountingOfficeAdmin] = Permissions.All,
            [Roles.Accountant] = Permissions.All.Where(x =>
                x.StartsWith("FinancialYears.") || x.StartsWith("AccountingPeriods.") || x.StartsWith("ChartOfAccounts.")
                || x.StartsWith("CostCenters.") || x is "JournalEntries.View" or "JournalEntries.Create" or "JournalEntries.Update"
                    or "JournalEntries.Post" or "JournalEntries.Reverse" or "JournalEntries.Cancel" or "JournalEntries.Submit"
                || x is "Documents.View" or "Documents.Upload" or "Documents.Download" or "Documents.Delete" or "Documents.Submit"
                || x.StartsWith("ClosingChecklist.") || x.StartsWith("ClosingTasks.") || x.StartsWith("ClosingSubmissions.")
                || x.StartsWith("AccountingReports.") || x.StartsWith("Notifications.") || x.StartsWith("Activities.")
                || x.StartsWith("Comments.") || x == "Search.Use" || x.StartsWith("CustomFields.") || x.StartsWith("OpeningBalances.")
                || x.StartsWith("BankReconciliation.") || x.StartsWith("FixedAssets.") || x.StartsWith("RecurringEntries.")
                || x.StartsWith("ClosingAssistant.") || x.StartsWith("Dashboard.") || x.StartsWith("BusinessParties.")
                || x == "Imports.View" || x == "Imports.Upload" || x == "Imports.Confirm" || x == "Imports.Submit" || x == "Imports.DownloadTemplate" || x == "Accounting.View" || x == "Reports.View"),
            [Roles.Reviewer] = ["Accounting.View", "Reports.View", "JournalEntries.View", "JournalEntries.Review", "JournalEntries.Approve", "JournalEntries.Reject", "JournalEntries.ReturnForCorrection", "Documents.View", "Documents.Download", "Documents.Review", "Documents.Approve", "Documents.Reject", "ClosingTasks.View", "ClosingTasks.Approve", "ClosingTasks.Reject", "ClosingSubmissions.View", "ClosingSubmissions.Review", "ClosingSubmissions.Approve", "ClosingSubmissions.Reject", "AccountingReports.TrialBalance", "AccountingReports.GeneralLedger", "AccountingReports.AccountStatement", "AccountingReports.ClosingProgress", "Imports.View", "Imports.Review", "Imports.Approve", "Imports.Reject", "AuditLogs.View"],
            [Roles.CompanyOwner] = ["Accounting.View", "Reports.View", "Documents.View", "Documents.Upload", "Documents.Download", "ClosingTasks.View", "ClosingSubmissions.View", "AccountingReports.TrialBalance", "AccountingReports.GeneralLedger", "AccountingReports.AccountStatement", "AccountingReports.ClosingProgress", "Imports.View", "Imports.Upload", "Imports.DownloadTemplate"],
            [Roles.CompanyUser] = ["Documents.View", "Documents.Upload", "Documents.Download", "Reports.View", "ClosingTasks.View", "ClosingTasks.Submit", "AccountingReports.ClosingProgress", "Imports.Upload", "Imports.DownloadTemplate"]
        };

        foreach (var (roleName, permissionNames) in map)
        {
            foreach (var permissionName in permissionNames)
            {
                var roleId = roles[roleName].Id;
                var permissionId = permissions[permissionName].Id;
                if (!await dbContext.RolePermissions.AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId))
                {
                    dbContext.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
                }
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedProfessionalDefaultsAsync(AppDbContext dbContext, RoleManager<ApplicationRole> roleManager)
    {
        var reviewer = await roleManager.FindByNameAsync(Roles.Reviewer);
        foreach (var entityType in new[] { nameof(JournalEntry), nameof(OpeningBalanceBatch), nameof(ClosingSubmission) })
        {
            if (await dbContext.WorkflowDefinitions.AnyAsync(x => x.EntityType == entityType))
            {
                continue;
            }

            dbContext.WorkflowDefinitions.Add(new WorkflowDefinition
            {
                NameAr = $"دورة اعتماد {entityType}",
                NameEn = $"{entityType} approval workflow",
                EntityType = entityType,
                IsActive = true,
                Steps =
                [
                    new WorkflowStep
                    {
                        StepOrder = 1,
                        StepNameAr = "مراجعة واعتماد",
                        StepNameEn = "Review and approve",
                        RequiredRoleId = reviewer?.Id,
                        CanApprove = true,
                        CanReject = true,
                        CanReturn = true,
                        IsFinalApproval = true
                    }
                ]
            });
        }

        var templates = new[]
        {
            new DocumentNumberTemplate { EntityType = "JE", Template = "JE-{YEAR}-{SEQ}", ResetPeriod = Domain.Enums.ResetPeriod.Yearly },
            new DocumentNumberTemplate { EntityType = "OB", Template = "OB-{YEAR}-{SEQ}", ResetPeriod = Domain.Enums.ResetPeriod.Yearly }
        };
        foreach (var template in templates)
        {
            if (!await dbContext.DocumentNumberTemplates.AnyAsync(x => x.EntityType == template.EntityType && x.IsActive))
            {
                dbContext.DocumentNumberTemplates.Add(template);
            }
        }

        foreach (var module in new[] { "CoreAccounting", "FixedAssets", "BankReconciliation", "RecurringEntries", "ReportBuilder" })
        {
            if (!await dbContext.TenantModules.AnyAsync(x => x.ModuleKey == module))
            {
                dbContext.TenantModules.Add(new TenantModule { ModuleKey = module });
            }
        }

        await dbContext.SaveChangesAsync();
    }

    public static async Task SeedTenantSessionDefaultsAsync(
        AppDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        ICurrentTenantService currentTenant)
    {
        var reviewer = await roleManager.FindByNameAsync(Roles.Reviewer);
        var tenantIds = await dbContext.Tenants
            .IgnoreQueryFilters()
            .Where(tenant => tenant.IsActive && !tenant.IsDeleted)
            .Select(tenant => tenant.Id)
            .ToListAsync();

        foreach (var tenantId in tenantIds)
        {
            currentTenant.SetTenant(tenantId);
            foreach (var entityType in new[]
                     {
                         nameof(JournalEntry),
                         nameof(OpeningBalanceBatch),
                         nameof(ClosingSubmission)
                     })
            {
                if (!await dbContext.WorkflowDefinitions.AnyAsync(
                        workflow => workflow.EntityType == entityType))
                {
                    dbContext.WorkflowDefinitions.Add(new WorkflowDefinition
                    {
                        NameAr = $"دورة اعتماد {entityType}",
                        NameEn = $"{entityType} approval workflow",
                        EntityType = entityType,
                        IsActive = true,
                        Steps =
                        [
                            new WorkflowStep
                            {
                                StepOrder = 1,
                                StepNameAr = "مراجعة واعتماد",
                                StepNameEn = "Review and approve",
                                RequiredRoleId = reviewer?.Id,
                                CanApprove = true,
                                CanReject = true,
                                CanReturn = true,
                                IsFinalApproval = true
                            }
                        ]
                    });
                }
            }

            foreach (var module in new[]
                     {
                         "CoreAccounting",
                         "FixedAssets",
                         "BankReconciliation",
                         "RecurringEntries",
                         "ReportBuilder"
                     })
            {
                if (!await dbContext.TenantModules.AnyAsync(
                        item => item.ModuleKey == module))
                {
                    dbContext.TenantModules.Add(new TenantModule
                    {
                        ModuleKey = module
                    });
                }
            }

            foreach (var template in new[]
                     {
                         new DocumentNumberTemplate
                         {
                             EntityType = "JE",
                             Template = "JE-{YEAR}-{SEQ}",
                             ResetPeriod = Domain.Enums.ResetPeriod.Yearly
                         },
                         new DocumentNumberTemplate
                         {
                             EntityType = "OB",
                             Template = "OB-{YEAR}-{SEQ}",
                             ResetPeriod = Domain.Enums.ResetPeriod.Yearly
                         }
                     })
            {
                if (!await dbContext.DocumentNumberTemplates.AnyAsync(
                        item =>
                            item.EntityType == template.EntityType &&
                            item.IsActive))
                {
                    dbContext.DocumentNumberTemplates.Add(template);
                }
            }

            await dbContext.SaveChangesAsync();
        }

        currentTenant.Clear();
    }

    public static async Task AssignSuperAdminSessionAsync(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        var email = configuration["SeedAdmin:Email"]
            ?? "admin@accountingsaas.local";
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || user.TenantId.HasValue)
        {
            return;
        }

        var tenant = await dbContext.Tenants
            .IgnoreQueryFilters()
            .Where(item => item.IsActive && !item.IsDeleted)
            .OrderBy(item => item.TenantNo)
            .FirstOrDefaultAsync();
        if (tenant is null)
        {
            return;
        }

        var year = await dbContext.FinancialYears
            .IgnoreQueryFilters()
            .Where(item =>
                item.TenantId == tenant.Id &&
                item.Status == Domain.Enums.FinancialYearStatus.Open &&
                !item.IsDeleted)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync();

        user.TenantId = tenant.Id;
        user.ActiveFinancialYearId = year?.Id;
        await userManager.UpdateAsync(user);
    }

    private static async Task SeedSuperAdminAsync(UserManager<ApplicationUser> userManager, IConfiguration configuration)
    {
        var email = configuration["SeedAdmin:Email"] ?? "admin@accountingsaas.local";
        var password = configuration["SeedAdmin:Password"] ?? "ChangeMe123!ChangeMe123!";
        var fullName = configuration["SeedAdmin:FullName"] ?? "System Super Admin";

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserNo = (await userManager.Users.MaxAsync(x => (long?)x.UserNo) ?? 0) + 1,
                UserName = email,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = email.ToUpperInvariant(),
                FullName = fullName,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(user, Roles.SuperAdmin))
        {
            await userManager.AddToRoleAsync(user, Roles.SuperAdmin);
        }
    }
}

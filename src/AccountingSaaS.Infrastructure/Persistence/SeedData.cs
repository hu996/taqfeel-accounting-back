using AccountingSaaS.Domain.Constants;
using AccountingSaaS.Domain.Entities;
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
            [Roles.AccountingOfficeAdmin] = Permissions.All.Where(x =>
                x.StartsWith("Tenants.View") || x.StartsWith("Users.") || x.StartsWith("Accounting.") || x.StartsWith("Reports.")
                || x.StartsWith("FinancialYears.") || x.StartsWith("AccountingPeriods.") || x.StartsWith("ChartOfAccounts.")
                || x.StartsWith("CostCenters.") || x.StartsWith("JournalEntries.") || x.StartsWith("Documents.")
                || x.StartsWith("ClosingChecklist.") || x.StartsWith("ClosingTasks.") || x.StartsWith("ClosingSubmissions.")
                || x.StartsWith("AccountingReports.") || x.StartsWith("Imports.") || x == "AuditLogs.View"),
            [Roles.Accountant] = Permissions.All.Where(x =>
                x.StartsWith("FinancialYears.") || x.StartsWith("AccountingPeriods.") || x.StartsWith("ChartOfAccounts.")
                || x.StartsWith("CostCenters.") || x is "JournalEntries.View" or "JournalEntries.Create" or "JournalEntries.Update"
                    or "JournalEntries.Post" or "JournalEntries.Reverse" or "JournalEntries.Cancel" or "JournalEntries.Submit"
                || x is "Documents.View" or "Documents.Upload" or "Documents.Download" or "Documents.Delete" or "Documents.Submit"
                || x.StartsWith("ClosingChecklist.") || x.StartsWith("ClosingTasks.") || x.StartsWith("ClosingSubmissions.")
                || x.StartsWith("AccountingReports.") || x == "Imports.View" || x == "Imports.Upload" || x == "Imports.Confirm" || x == "Imports.Submit" || x == "Imports.DownloadTemplate" || x == "Accounting.View" || x == "Reports.View"),
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

using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Constants;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AccountingSaaS.Infrastructure.Persistence;

public static class DevelopmentSeedData
{
    private const int SeedYear = 2026;

    public static async Task SeedAsync(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantService currentTenant,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue("DevelopmentSeed:Enabled", true))
        {
            return;
        }

        var password = configuration["DevelopmentSeed:Password"] ?? "DevPass123!DevPass123!";

        var officeTenant = await EnsureTenantAsync(
            dbContext,
            "مكتب محاسبة تجريبي",
            "DEV-OFFICE-001",
            "office-dev@accountingsaas.local",
            "القاهرة - عنوان تجريبي");

        var firstCompany = await EnsureTenantAsync(
            dbContext,
            "شركة الاختبار الأولى",
            "DEV-COMPANY-001",
            "company-one-dev@accountingsaas.local",
            "القاهرة - فرع تجريبي 1");

        var secondCompany = await EnsureTenantAsync(
            dbContext,
            "شركة الاختبار الثانية",
            "DEV-COMPANY-002",
            "company-two-dev@accountingsaas.local",
            "القاهرة - فرع تجريبي 2");

        var officeAdmin = await EnsureUserAsync(userManager, "office.admin@accountingsaas.local", "مدير مكتب محاسبة تجريبي", password, Roles.AccountingOfficeAdmin, officeTenant.Id);
        var accountant = await EnsureUserAsync(userManager, "accountant@accountingsaas.local", "محاسب تجريبي", password, Roles.Accountant, firstCompany.Id);
        var reviewer = await EnsureUserAsync(userManager, "reviewer@accountingsaas.local", "مراجع تجريبي", password, Roles.Reviewer, firstCompany.Id);
        var owner = await EnsureUserAsync(userManager, "owner@company-one.local", "مالك شركة الاختبار الأولى", password, Roles.CompanyOwner, firstCompany.Id);
        var companyUser = await EnsureUserAsync(userManager, "user@company-one.local", "مستخدم شركة تجريبي", password, Roles.CompanyUser, firstCompany.Id);

        await EnsureTenantAccessAsync(dbContext, officeAdmin.Id, firstCompany.Id);
        await EnsureTenantAccessAsync(dbContext, officeAdmin.Id, secondCompany.Id);
        await EnsureTenantAccessAsync(dbContext, accountant.Id, firstCompany.Id);
        await EnsureTenantAccessAsync(dbContext, reviewer.Id, firstCompany.Id);
        await EnsureReviewerAssignmentAsync(dbContext, reviewer.Id, firstCompany.Id);
        await EnsureTenantAccessAsync(dbContext, owner.Id, firstCompany.Id);
        await EnsureTenantAccessAsync(dbContext, companyUser.Id, firstCompany.Id);
        await dbContext.SaveChangesAsync();

        await SeedTenantAccountingDataAsync(dbContext, currentTenant, environment, firstCompany.Id, accountant.Id, reviewer.Id, includeFullScenario: true);
        await SeedTenantAccountingDataAsync(dbContext, currentTenant, environment, secondCompany.Id, officeAdmin.Id, officeAdmin.Id, includeFullScenario: false);

        currentTenant.Clear();

        if (!await dbContext.AuditLogs.AnyAsync(x => x.Action == "Development seed data created" && x.EntityName == nameof(Tenant)))
        {
            dbContext.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = "Development seed data created",
                EntityName = nameof(Tenant),
                NewValues = "Development-only accounting sample data was created idempotently.",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }
    }

    private static async Task<Tenant> EnsureTenantAsync(AppDbContext dbContext, string companyName, string registrationNo, string email, string address)
    {
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.CommercialRegistrationNo == registrationNo || x.CompanyName == companyName);
        if (tenant is not null)
        {
            return tenant;
        }

        tenant = new Tenant
        {
            TenantNo = (await dbContext.Tenants.MaxAsync(x => (long?)x.TenantNo) ?? 0) + 1,
            CompanyName = companyName,
            CommercialRegistrationNo = registrationNo,
            TaxNumber = $"TAX-{registrationNo}",
            Email = email,
            Phone = "01000000000",
            Address = address,
            IsActive = true
        };

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        return tenant;
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string password,
        string roleName,
        Guid tenantId)
    {
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
                TenantId = tenantId,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Could not create development user {email}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            var result = await userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Could not assign role {roleName} to development user {email}: {string.Join("; ", result.Errors.Select(x => x.Description))}");
            }
        }

        return user;
    }

    private static async Task EnsureTenantAccessAsync(AppDbContext dbContext, Guid userId, Guid tenantId)
    {
        if (!await dbContext.UserTenantAccesses.AnyAsync(x => x.UserId == userId && x.TenantId == tenantId))
        {
            dbContext.UserTenantAccesses.Add(new UserTenantAccess { UserId = userId, TenantId = tenantId });
        }
    }

    private static async Task EnsureReviewerAssignmentAsync(AppDbContext dbContext, Guid userId, Guid tenantId)
    {
        if (!await dbContext.ReviewerTenantAssignments.AnyAsync(
                x => x.ReviewerUserId == userId && x.TenantId == tenantId))
        {
            dbContext.ReviewerTenantAssignments.Add(new ReviewerTenantAssignment
            {
                ReviewerUserId = userId,
                TenantId = tenantId,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private static async Task SeedTenantAccountingDataAsync(
        AppDbContext dbContext,
        ICurrentTenantService currentTenant,
        IHostEnvironment environment,
        Guid tenantId,
        Guid mainUserId,
        Guid reviewerUserId,
        bool includeFullScenario)
    {
        currentTenant.SetTenant(tenantId);

        var financialYear = await EnsureFinancialYearAsync(dbContext);
        var periods = await EnsurePeriodsAsync(dbContext, financialYear.Id);
        var accounts = await EnsureAccountsAsync(dbContext);
        var costCenters = await EnsureCostCentersAsync(dbContext);

        var january = periods[0];
        var february = periods[1];
        var mainCostCenter = costCenters["MAIN"];
        var adminCostCenter = costCenters["ADMIN"];

        await EnsureJournalEntryAsync(
            dbContext,
            financialYear.Id,
            january.Id,
            "OB-DEV-2026",
            new DateOnly(SeedYear, 1, 1),
            "رصيد افتتاحي تجريبي موزون",
            JournalEntryStatus.Posted,
            mainUserId,
            [
                new SeedLine(accounts["1200"].Id, mainCostCenter.Id, 100000m, 0m, "رصيد بنك افتتاحي"),
                new SeedLine(accounts["1300"].Id, mainCostCenter.Id, 20000m, 0m, "رصيد عملاء افتتاحي تجريبي"),
                new SeedLine(accounts["3100"].Id, null, 0m, 120000m, "رأس مال افتتاحي")
            ]);

        await EnsureJournalEntryAsync(
            dbContext,
            financialYear.Id,
            january.Id,
            "JE-DEV-001",
            new DateOnly(SeedYear, 1, 10),
            "فاتورة بيع تجريبية لعميل تجريبي",
            JournalEntryStatus.Posted,
            mainUserId,
            [
                new SeedLine(accounts["1300"].Id, mainCostCenter.Id, 15000m, 0m, "مدينون - عميل تجريبي"),
                new SeedLine(accounts["4100"].Id, mainCostCenter.Id, 0m, 15000m, "إيراد مبيعات تجريبي")
            ]);

        await EnsureJournalEntryAsync(
            dbContext,
            financialYear.Id,
            january.Id,
            "JE-DEV-002",
            new DateOnly(SeedYear, 1, 20),
            "سداد مصروف إيجار تجريبي",
            JournalEntryStatus.Posted,
            mainUserId,
            [
                new SeedLine(accounts["5100"].Id, adminCostCenter.Id, 3000m, 0m, "مصروف إيجار تجريبي"),
                new SeedLine(accounts["1200"].Id, adminCostCenter.Id, 0m, 3000m, "سداد من البنك التجريبي")
            ]);

        await EnsureJournalEntryAsync(
            dbContext,
            financialYear.Id,
            february.Id,
            "ADJ-DEV-001",
            new DateOnly(SeedYear, 2, 28),
            "قيد تسوية تجريبي - السبب: تحميل مصروف مستحق",
            JournalEntryStatus.Posted,
            mainUserId,
            [
                new SeedLine(accounts["5200"].Id, adminCostCenter.Id, 5000m, 0m, "مصروف رواتب مستحق"),
                new SeedLine(accounts["2100"].Id, adminCostCenter.Id, 0m, 5000m, "مورد/دائن تجريبي")
            ]);

        if (includeFullScenario)
        {
            await EnsureDocumentAsync(dbContext, environment, financialYear.Id, february.Id, mainUserId);
            var templateItems = await EnsureClosingChecklistTemplateAsync(dbContext);
            await EnsureClosingTasksAndSubmissionAsync(dbContext, financialYear.Id, january, templateItems, mainUserId, reviewerUserId);
            await EnsureImportBatchAsync(dbContext, financialYear.Id, february.Id, mainUserId);
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task<FinancialYear> EnsureFinancialYearAsync(AppDbContext dbContext)
    {
        var yearName = $"FY-{SeedYear}";
        var financialYear = await dbContext.FinancialYears.FirstOrDefaultAsync(x => x.YearName == yearName);
        if (financialYear is not null)
        {
            return financialYear;
        }

        financialYear = new FinancialYear
        {
            YearName = yearName,
            StartDate = new DateOnly(SeedYear, 1, 1),
            EndDate = new DateOnly(SeedYear, 12, 31),
            Status = FinancialYearStatus.Open
        };

        dbContext.FinancialYears.Add(financialYear);
        await dbContext.SaveChangesAsync();
        return financialYear;
    }

    private static async Task<IReadOnlyList<AccountingPeriod>> EnsurePeriodsAsync(AppDbContext dbContext, Guid financialYearId)
    {
        var periods = new List<AccountingPeriod>();
        for (var month = 1; month <= 12; month++)
        {
            var periodName = $"{SeedYear}-{month:00}";
            var period = await dbContext.AccountingPeriods.FirstOrDefaultAsync(x => x.FinancialYearId == financialYearId && x.PeriodName == periodName);
            if (period is null)
            {
                period = new AccountingPeriod
                {
                    FinancialYearId = financialYearId,
                    PeriodName = periodName,
                    StartDate = new DateOnly(SeedYear, month, 1),
                    EndDate = new DateOnly(SeedYear, month, DateTime.DaysInMonth(SeedYear, month)),
                    Status = AccountingPeriodStatus.Open
                };
                dbContext.AccountingPeriods.Add(period);
                await dbContext.SaveChangesAsync();
            }

            periods.Add(period);
        }

        return periods;
    }

    private static async Task<Dictionary<string, Account>> EnsureAccountsAsync(AppDbContext dbContext)
    {
        var accounts = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);

        accounts["1000"] = await EnsureAccountAsync(dbContext, "1000", "الأصول", "Assets", AccountType.Asset, NormalBalance.Debit, null, isPosting: false);
        accounts["2000"] = await EnsureAccountAsync(dbContext, "2000", "الالتزامات", "Liabilities", AccountType.Liability, NormalBalance.Credit, null, isPosting: false);
        accounts["3000"] = await EnsureAccountAsync(dbContext, "3000", "حقوق الملكية", "Equity", AccountType.Equity, NormalBalance.Credit, null, isPosting: false);
        accounts["4000"] = await EnsureAccountAsync(dbContext, "4000", "الإيرادات", "Revenue", AccountType.Revenue, NormalBalance.Credit, null, isPosting: false);
        accounts["5000"] = await EnsureAccountAsync(dbContext, "5000", "المصروفات", "Expenses", AccountType.Expense, NormalBalance.Debit, null, isPosting: false);

        accounts["1100"] = await EnsureAccountAsync(dbContext, "1100", "الصندوق التجريبي", "Development Cash", AccountType.Asset, NormalBalance.Debit, accounts["1000"].Id, isPosting: true);
        accounts["1200"] = await EnsureAccountAsync(dbContext, "1200", "بنك تجريبي", "Development Bank", AccountType.Asset, NormalBalance.Debit, accounts["1000"].Id, isPosting: true);
        accounts["1300"] = await EnsureAccountAsync(dbContext, "1300", "عملاء تجريبيون", "Development Customers", AccountType.Asset, NormalBalance.Debit, accounts["1000"].Id, isPosting: true);
        accounts["2100"] = await EnsureAccountAsync(dbContext, "2100", "موردون تجريبيون", "Development Suppliers", AccountType.Liability, NormalBalance.Credit, accounts["2000"].Id, isPosting: true);
        accounts["3100"] = await EnsureAccountAsync(dbContext, "3100", "رأس مال تجريبي", "Development Capital", AccountType.Equity, NormalBalance.Credit, accounts["3000"].Id, isPosting: true);
        accounts["4100"] = await EnsureAccountAsync(dbContext, "4100", "إيرادات مبيعات تجريبية", "Development Sales Revenue", AccountType.Revenue, NormalBalance.Credit, accounts["4000"].Id, isPosting: true);
        accounts["5100"] = await EnsureAccountAsync(dbContext, "5100", "مصروف إيجار تجريبي", "Development Rent Expense", AccountType.Expense, NormalBalance.Debit, accounts["5000"].Id, isPosting: true);
        accounts["5200"] = await EnsureAccountAsync(dbContext, "5200", "مصروف رواتب تجريبي", "Development Salaries Expense", AccountType.Expense, NormalBalance.Debit, accounts["5000"].Id, isPosting: true);

        return accounts;
    }

    private static async Task<Account> EnsureAccountAsync(
        AppDbContext dbContext,
        string code,
        string nameAr,
        string nameEn,
        AccountType type,
        NormalBalance normalBalance,
        Guid? parentAccountId,
        bool isPosting)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(x => x.Code == code);
        if (account is not null)
        {
            return account;
        }

        account = new Account
        {
            Id = Guid.NewGuid(),
            AccountNo = (await dbContext.Accounts.MaxAsync(x => (long?)x.AccountNo) ?? 0) + 1,
            Code = code,
            NameAr = nameAr,
            NameEn = nameEn,
            AccountType = type,
            NormalBalance = normalBalance,
            ParentAccountId = parentAccountId,
            IsPostingAccount = isPosting,
            IsActive = true
        };

        dbContext.Accounts.Add(account);
        await dbContext.SaveChangesAsync();
        return account;
    }

    private static async Task<Dictionary<string, CostCenter>> EnsureCostCentersAsync(AppDbContext dbContext)
    {
        var result = new Dictionary<string, CostCenter>(StringComparer.OrdinalIgnoreCase)
        {
            ["MAIN"] = await EnsureCostCenterAsync(dbContext, "MAIN", "مركز تكلفة رئيسي تجريبي"),
            ["SALES"] = await EnsureCostCenterAsync(dbContext, "SALES", "مبيعات تجريبية"),
            ["ADMIN"] = await EnsureCostCenterAsync(dbContext, "ADMIN", "إدارة تجريبية")
        };

        return result;
    }

    private static async Task<CostCenter> EnsureCostCenterAsync(AppDbContext dbContext, string code, string name)
    {
        var costCenter = await dbContext.CostCenters.FirstOrDefaultAsync(x => x.Code == code);
        if (costCenter is not null)
        {
            return costCenter;
        }

        costCenter = new CostCenter
        {
            CostCenterNo = (await dbContext.CostCenters.MaxAsync(x => (long?)x.CostCenterNo) ?? 0) + 1,
            Code = code,
            Name = name,
            IsActive = true
        };
        dbContext.CostCenters.Add(costCenter);
        await dbContext.SaveChangesAsync();
        return costCenter;
    }

    private static async Task EnsureJournalEntryAsync(
        AppDbContext dbContext,
        Guid financialYearId,
        Guid accountingPeriodId,
        string entryNumber,
        DateOnly entryDate,
        string description,
        JournalEntryStatus status,
        Guid postedByUserId,
        IReadOnlyList<SeedLine> lines)
    {
        if (await dbContext.JournalEntries.AnyAsync(x => x.FinancialYearId == financialYearId && x.EntryNumber == entryNumber))
        {
            return;
        }

        var totalDebit = lines.Sum(x => x.Debit);
        var totalCredit = lines.Sum(x => x.Credit);
        if (totalDebit != totalCredit)
        {
            throw new InvalidOperationException($"Development journal entry {entryNumber} is not balanced.");
        }

        dbContext.JournalEntries.Add(new JournalEntry
        {
            JournalEntryNo = (await dbContext.JournalEntries.MaxAsync(x => (long?)x.JournalEntryNo) ?? 0) + 1,
            FinancialYearId = financialYearId,
            AccountingPeriodId = accountingPeriodId,
            EntryNumber = entryNumber,
            EntryDate = entryDate,
            Description = description,
            Status = status,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            PostedAt = status == JournalEntryStatus.Posted ? DateTimeOffset.UtcNow : null,
            PostedByUserId = status == JournalEntryStatus.Posted ? postedByUserId : null,
            Lines = lines.Select(x => new JournalEntryLine
            {
                AccountId = x.AccountId,
                CostCenterId = x.CostCenterId,
                Debit = x.Debit,
                Credit = x.Credit,
                Description = x.Description
            }).ToList()
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task EnsureDocumentAsync(AppDbContext dbContext, IHostEnvironment environment, Guid financialYearId, Guid accountingPeriodId, Guid uploadedByUserId)
    {
        const string originalFileName = "development-sample-invoice.pdf";
        if (await dbContext.Documents.AnyAsync(x => x.OriginalFileName == originalFileName))
        {
            return;
        }

        var directory = Path.Combine(environment.ContentRootPath, "App_Data", "DevelopmentSeed");
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, originalFileName);
        if (!File.Exists(filePath))
        {
            await File.WriteAllTextAsync(filePath, "Development-only placeholder document for API testing.");
        }

        var fileInfo = new FileInfo(filePath);
        dbContext.Documents.Add(new Document
        {
            DocumentNo = (await dbContext.Documents.MaxAsync(x => (long?)x.DocumentNo) ?? 0) + 1,
            FinancialYearId = financialYearId,
            AccountingPeriodId = accountingPeriodId,
            DocumentType = DocumentType.Invoice,
            OriginalFileName = originalFileName,
            StoredFileName = originalFileName,
            FilePath = filePath,
            ContentType = "application/pdf",
            SizeInBytes = fileInfo.Length,
            RelatedEntityName = nameof(JournalEntry),
            RelatedEntityId = "JE-DEV-001",
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTimeOffset.UtcNow,
            Notes = "مستند تجريبي لاختبار شاشة المستندات"
        });

        await dbContext.SaveChangesAsync();
    }

    private static async Task<IReadOnlyList<ClosingChecklistTemplateItem>> EnsureClosingChecklistTemplateAsync(AppDbContext dbContext)
    {
        const string templateName = "قائمة إغلاق شهرية تجريبية";
        var template = await dbContext.ClosingChecklistTemplates.FirstOrDefaultAsync(x => x.Name == templateName);
        if (template is null)
        {
            template = new ClosingChecklistTemplate
            {
                Name = templateName,
                Description = "قائمة اختبار لإغلاق الفترة الشهرية في بيئة Development",
                IsDefault = true,
                IsActive = true
            };
            dbContext.ClosingChecklistTemplates.Add(template);
            await dbContext.SaveChangesAsync();
        }

        var seedItems = new[]
        {
            new { Title = "مراجعة القيود المرحلة", Description = "التأكد من عدم وجود قيود غير موزونة", SortOrder = 1, IsRequired = true },
            new { Title = "مراجعة المستندات", Description = "التأكد من رفع المستندات الأساسية", SortOrder = 2, IsRequired = true },
            new { Title = "مطابقة البنك التجريبية", Description = "مطابقة أرصدة البنك التجريبية", SortOrder = 3, IsRequired = false }
        };

        foreach (var item in seedItems)
        {
            if (!await dbContext.ClosingChecklistTemplateItems.AnyAsync(x => x.TemplateId == template.Id && x.SortOrder == item.SortOrder))
            {
                dbContext.ClosingChecklistTemplateItems.Add(new ClosingChecklistTemplateItem
                {
                    TemplateId = template.Id,
                    Title = item.Title,
                    Description = item.Description,
                    SortOrder = item.SortOrder,
                    IsRequired = item.IsRequired
                });
            }
        }

        await dbContext.SaveChangesAsync();
        return await dbContext.ClosingChecklistTemplateItems
            .Where(x => x.TemplateId == template.Id)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();
    }

    private static async Task EnsureClosingTasksAndSubmissionAsync(
        AppDbContext dbContext,
        Guid financialYearId,
        AccountingPeriod period,
        IReadOnlyList<ClosingChecklistTemplateItem> templateItems,
        Guid submittedByUserId,
        Guid approvedByUserId)
    {
        foreach (var item in templateItems)
        {
            if (!await dbContext.ClosingTasks.AnyAsync(x => x.AccountingPeriodId == period.Id && x.TemplateItemId == item.Id))
            {
                dbContext.ClosingTasks.Add(new ClosingTask
                {
                    FinancialYearId = financialYearId,
                    AccountingPeriodId = period.Id,
                    TemplateItemId = item.Id,
                    Title = item.Title,
                    Description = item.Description,
                    SortOrder = item.SortOrder,
                    IsRequired = item.IsRequired,
                    AssignedToUserId = submittedByUserId,
                    Status = item.IsRequired ? ClosingTaskStatus.Approved : ClosingTaskStatus.NotApplicable,
                    DueDate = period.EndDate,
                    SubmittedAt = DateTimeOffset.UtcNow,
                    SubmittedByUserId = submittedByUserId,
                    ApprovedAt = item.IsRequired ? DateTimeOffset.UtcNow : null,
                    ApprovedByUserId = item.IsRequired ? approvedByUserId : null
                });
            }
        }

        var submission = await dbContext.ClosingSubmissions.FirstOrDefaultAsync(x => x.AccountingPeriodId == period.Id);
        if (submission is null)
        {
            dbContext.ClosingSubmissions.Add(new ClosingSubmission
            {
                FinancialYearId = financialYearId,
                AccountingPeriodId = period.Id,
                Status = ClosingSubmissionStatus.Closed,
                SubmittedAt = DateTimeOffset.UtcNow,
                SubmittedByUserId = submittedByUserId,
                ReviewedAt = DateTimeOffset.UtcNow,
                ReviewedByUserId = approvedByUserId,
                ApprovedAt = DateTimeOffset.UtcNow,
                ApprovedByUserId = approvedByUserId,
                ClosedAt = DateTimeOffset.UtcNow,
                ClosedByUserId = approvedByUserId,
                Notes = "إغلاق تجريبي لفترة يناير بعد اعتماد كل المهام المطلوبة"
            });
        }

        period.Status = AccountingPeriodStatus.Closed;
        period.ClosedAt ??= DateTimeOffset.UtcNow;
        period.ClosedByUserId ??= approvedByUserId;
        await dbContext.SaveChangesAsync();
    }

    private static async Task EnsureImportBatchAsync(AppDbContext dbContext, Guid financialYearId, Guid accountingPeriodId, Guid uploadedByUserId)
    {
        const string fileName = "development-customers-import.xlsx";
        if (await dbContext.ImportBatches.AnyAsync(x => x.OriginalFileName == fileName))
        {
            return;
        }

        var batch = new ImportBatch
        {
            ImportBatchNo = (await dbContext.ImportBatches.MaxAsync(x => (long?)x.ImportBatchNo) ?? 0) + 1,
            ImportType = ImportType.Customers,
            Status = ImportBatchStatus.HasErrors,
            OriginalFileName = fileName,
            StoredFileName = fileName,
            FilePath = "App_Data/DevelopmentSeed/development-customers-import.xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileSizeInBytes = 1024,
            FinancialYearId = financialYearId,
            AccountingPeriodId = accountingPeriodId,
            TotalRows = 2,
            ValidRows = 1,
            InvalidRows = 1,
            WarningRows = 0,
            ImportedRows = 0,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = DateTimeOffset.UtcNow,
            ValidatedAt = DateTimeOffset.UtcNow,
            ErrorSummary = "صف تجريبي غير صالح بسبب بريد إلكتروني غير صحيح",
            Notes = "Batch تجريبي لاختبار staging/preview بدون إدخال بيانات نهائية"
        };

        batch.Rows.Add(new ImportBatchRow
        {
            RowNumber = 2,
            RawJson = """{"Name":"عميل تجريبي","TaxNumber":"DEV-CUST-001","Phone":"01000000001","Email":"customer@example.local","Address":"عنوان تجريبي","IsActive":"true"}""",
            NormalizedJson = """{"Name":"عميل تجريبي","TaxNumber":"DEV-CUST-001","Phone":"01000000001","Email":"customer@example.local","Address":"عنوان تجريبي","IsActive":"true"}""",
            Status = ImportRowStatus.Valid
        });

        batch.Rows.Add(new ImportBatchRow
        {
            RowNumber = 3,
            RawJson = """{"Name":"عميل ببريد غير صحيح","TaxNumber":"DEV-CUST-002","Phone":"01000000002","Email":"invalid-email","Address":"عنوان تجريبي","IsActive":"true"}""",
            Status = ImportRowStatus.Invalid,
            ErrorMessages = "Email is invalid."
        });

        dbContext.ImportBatches.Add(batch);
        await dbContext.SaveChangesAsync();
    }

    private sealed record SeedLine(Guid AccountId, Guid? CostCenterId, decimal Debit, decimal Credit, string Description);
}

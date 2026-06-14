using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class OpeningBalanceService(
    AppDbContext db,
    ICurrentTenantService tenant,
    ICurrentUserService user,
    IDocumentNumberService numbers,
    IActivityService activities,
    IDynamicWorkflowService workflow) : AccountingServiceBase(db, tenant), IOpeningBalanceService
{
    public async Task<BaseResponseDto<OpeningBalanceBatchDto>> CreateAsync(CreateOpeningBalanceBatchRequest request, CancellationToken ct)
    {
        var debit = request.Lines.Sum(x => x.Debit);
        var credit = request.Lines.Sum(x => x.Credit);
        if (request.Lines.Count < 2 || debit <= 0 || debit != credit)
            return BaseResponseDto<OpeningBalanceBatchDto>.Fail("Opening balance must be complete and balanced.");
        if (!await DbContext.FinancialYears.AnyAsync(x => x.Id == request.FinancialYearId && x.Status == FinancialYearStatus.Open, ct))
            return BaseResponseDto<OpeningBalanceBatchDto>.Fail("Financial year is not open.");
        if (await DbContext.OpeningBalanceBatches.AnyAsync(x => x.FinancialYearId == request.FinancialYearId && x.Status >= OpeningBalanceStatus.Approved, ct))
            return BaseResponseDto<OpeningBalanceBatchDto>.Fail("An approved opening balance already exists for this financial year.");
        var batch = new OpeningBalanceBatch
        {
            FinancialYearId = request.FinancialYearId,
            BatchNo = await numbers.GenerateAsync("OB", DateOnly.FromDateTime(DateTime.UtcNow), null, ct),
            Lines = request.Lines.Select(x => new OpeningBalanceLine
            {
                AccountId = x.AccountId, Debit = x.Debit, Credit = x.Credit, CostCenterId = x.CostCenterId, Notes = x.Notes
            }).ToList()
        };
        DbContext.OpeningBalanceBatches.Add(batch);
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<OpeningBalanceBatchDto>.Ok(ToDto(batch));
    }

    public async Task<BaseResponseDto<OpeningBalanceBatchDto>> SubmitAsync(Guid id, CancellationToken ct)
    {
        var batch = await DbContext.OpeningBalanceBatches.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (batch is null) return BaseResponseDto<OpeningBalanceBatchDto>.NotFound("Opening balance was not found.");
        if (batch.Status is not (OpeningBalanceStatus.Draft or OpeningBalanceStatus.Rejected))
            return BaseResponseDto<OpeningBalanceBatchDto>.Fail("Only draft or rejected batches can be submitted.");
        var firstStep = await workflow.GetFirstStepAsync(nameof(OpeningBalanceBatch), ct);
        if (firstStep is null) return BaseResponseDto<OpeningBalanceBatchDto>.Fail("No active opening balance workflow is configured.");
        batch.Status = OpeningBalanceStatus.Submitted;
        batch.WorkflowDefinitionId = firstStep.WorkflowDefinitionId;
        batch.WorkflowStepId = firstStep.Id;
        batch.SubmittedAt = DateTimeOffset.UtcNow;
        batch.SubmittedByUserId = user.UserId;
        await DbContext.SaveChangesAsync(ct);
        await workflow.RecordActionAsync(nameof(OpeningBalanceBatch), batch.Id, firstStep.WorkflowDefinitionId, firstStep.Id,
            OpeningBalanceStatus.Draft.ToString(), OpeningBalanceStatus.Submitted.ToString(), WorkflowActionType.Submit, null, null, ct);
        return BaseResponseDto<OpeningBalanceBatchDto>.Ok(ToDto(batch));
    }

    public async Task<BaseResponseDto<OpeningBalanceBatchDto>> ApproveAsync(Guid id, CancellationToken ct)
    {
        var batch = await DbContext.OpeningBalanceBatches.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (batch is null) return BaseResponseDto<OpeningBalanceBatchDto>.NotFound("Opening balance was not found.");
        if (batch.Status is not (OpeningBalanceStatus.Submitted or OpeningBalanceStatus.UnderReview))
            return BaseResponseDto<OpeningBalanceBatchDto>.Fail("Opening balance must be submitted through workflow.");
        var step = batch.WorkflowStepId.HasValue
            ? await DbContext.WorkflowSteps.FirstOrDefaultAsync(x => x.Id == batch.WorkflowStepId, ct)
            : null;
        if (step is null || !await workflow.CanActAsync(step, WorkflowActionType.Approve, ct))
            return BaseResponseDto<OpeningBalanceBatchDto>.Fail("The current workflow step does not allow approval.");
        if (await DbContext.OpeningBalanceBatches.AnyAsync(x => x.Id != id && x.FinancialYearId == batch.FinancialYearId && x.Status >= OpeningBalanceStatus.Approved, ct))
            return BaseResponseDto<OpeningBalanceBatchDto>.Fail("An approved opening balance already exists for this financial year.");
        var period = await DbContext.AccountingPeriods.Where(x => x.FinancialYearId == batch.FinancialYearId && x.Status == AccountingPeriodStatus.Open)
            .OrderBy(x => x.StartDate).FirstOrDefaultAsync(ct);
        if (period is null) return BaseResponseDto<OpeningBalanceBatchDto>.Fail("No open accounting period exists.");
        var entry = new JournalEntry
        {
            FinancialYearId = batch.FinancialYearId, AccountingPeriodId = period.Id, EntryDate = period.StartDate,
            EntryNumber = await numbers.GenerateAsync("JE", period.StartDate, null, ct),
            Description = $"Opening balance {batch.BatchNo}", Status = JournalEntryStatus.Draft,
            WorkflowStatus = WorkflowStatus.Approved, TotalDebit = batch.Lines.Sum(x => x.Debit), TotalCredit = batch.Lines.Sum(x => x.Credit),
            Lines = batch.Lines.Select(x => new JournalEntryLine { AccountId = x.AccountId, CostCenterId = x.CostCenterId, Debit = x.Debit, Credit = x.Credit, Description = x.Notes }).ToList()
        };
        DbContext.JournalEntries.Add(entry);
        batch.Status = OpeningBalanceStatus.Approved;
        batch.ApprovedAt = DateTimeOffset.UtcNow;
        batch.ApprovedByUserId = user.UserId;
        await DbContext.SaveChangesAsync(ct);
        await workflow.RecordActionAsync(nameof(OpeningBalanceBatch), batch.Id, step.WorkflowDefinitionId, step.Id,
            OpeningBalanceStatus.Submitted.ToString(), OpeningBalanceStatus.Approved.ToString(), WorkflowActionType.Approve, null, null, ct);
        batch.JournalEntryId = entry.Id;
        await DbContext.SaveChangesAsync(ct);
        await activities.CreateAsync(new("Approved", nameof(OpeningBalanceBatch), batch.Id, "اعتماد الأرصدة الافتتاحية", "Opening balances approved", batch.BatchNo, batch.BatchNo), ct);
        return BaseResponseDto<OpeningBalanceBatchDto>.Ok(ToDto(batch));
    }

    private static OpeningBalanceBatchDto ToDto(OpeningBalanceBatch x) =>
        new(x.Id, x.FinancialYearId, x.BatchNo, x.Status, x.JournalEntryId, x.Lines.Sum(l => l.Debit), x.Lines.Sum(l => l.Credit));
}

public sealed class BankReconciliationService(
    AppDbContext db,
    ICurrentTenantService tenant,
    ICurrentUserService user) : AccountingServiceBase(db, tenant), IBankReconciliationService
{
    public async Task<BaseResponseDto<BankAccountDto>> CreateBankAccountAsync(BankAccountRequest request, CancellationToken ct)
    {
        if (!await DbContext.Accounts.AnyAsync(x => x.Id == request.AccountId && x.IsActive && x.IsPostingAccount, ct))
            return BaseResponseDto<BankAccountDto>.Fail("A valid posting account is required.");
        var item = new BankAccount { AccountId = request.AccountId, BankName = request.BankName, AccountNumber = request.AccountNumber, Iban = request.Iban, IsActive = request.IsActive };
        DbContext.BankAccounts.Add(item);
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<BankAccountDto>.Ok(new(item.Id, item.AccountId, item.BankName, item.AccountNumber, item.Iban, item.IsActive));
    }

    public async Task<BaseResponseDto<object>> AddStatementAsync(BankStatementRequest request, CancellationToken ct)
    {
        if (request.Debit < 0 || request.Credit < 0 || request.Debit > 0 && request.Credit > 0 || request.Debit == 0 && request.Credit == 0)
            return BaseResponseDto<object>.Fail("Statement line must contain either debit or credit.");
        if (!await DbContext.BankAccounts.AnyAsync(x => x.Id == request.BankAccountId && x.IsActive, ct))
            return BaseResponseDto<object>.NotFound("Bank account was not found.");
        DbContext.BankStatements.Add(new BankStatement
        {
            BankAccountId = request.BankAccountId, StatementDate = request.StatementDate, Description = request.Description,
            Debit = request.Debit, Credit = request.Credit, ReferenceNo = request.ReferenceNo
        });
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<object>.Ok(null);
    }

    public async Task<BaseResponseDto<BankReconciliationDto>> CreateAsync(CreateBankReconciliationRequest request, CancellationToken ct)
    {
        if (await DbContext.BankReconciliations.AnyAsync(x => x.BankAccountId == request.BankAccountId && x.AccountingPeriodId == request.AccountingPeriodId, ct))
            return BaseResponseDto<BankReconciliationDto>.Fail("Reconciliation already exists for this period.");
        var item = new BankReconciliation { BankAccountId = request.BankAccountId, AccountingPeriodId = request.AccountingPeriodId, Status = ReconciliationStatus.InProgress };
        DbContext.BankReconciliations.Add(item);
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<BankReconciliationDto>.Ok(new(item.Id, item.BankAccountId, item.AccountingPeriodId, item.Status));
    }

    public async Task<BaseResponseDto<object>> MatchAsync(Guid reconciliationId, MatchBankStatementRequest request, CancellationToken ct)
    {
        var reconciliation = await DbContext.BankReconciliations.FirstOrDefaultAsync(x => x.Id == reconciliationId, ct);
        var statement = await DbContext.BankStatements.FirstOrDefaultAsync(x => x.Id == request.BankStatementId && !x.IsMatched, ct);
        var line = await DbContext.JournalEntryLines.Include(x => x.JournalEntry).FirstOrDefaultAsync(x => x.Id == request.JournalEntryLineId && x.JournalEntry.Status == JournalEntryStatus.Posted, ct);
        if (reconciliation is null || statement is null || line is null) return BaseResponseDto<object>.Fail("Valid reconciliation, unmatched statement and posted journal line are required.");
        var statementAmount = Math.Abs(statement.Debit - statement.Credit);
        var lineAmount = Math.Abs(line.Debit - line.Credit);
        if (request.MatchedAmount <= 0 || request.MatchedAmount > statementAmount || request.MatchedAmount > lineAmount)
            return BaseResponseDto<object>.Fail("Matched amount exceeds the source amount.");
        DbContext.BankReconciliationMatches.Add(new BankReconciliationMatch
        {
            ReconciliationId = reconciliationId, BankStatementId = statement.Id, JournalEntryLineId = line.Id,
            MatchType = BankMatchType.Manual, MatchedAmount = request.MatchedAmount,
            MatchedByUserId = user.UserId ?? throw new InvalidOperationException("Authenticated user is required."), MatchedAt = DateTimeOffset.UtcNow
        });
        statement.IsMatched = true;
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<object>.Ok(null);
    }

    public async Task<BaseResponseDto<object>> UnmatchAsync(Guid reconciliationId, Guid statementId, CancellationToken ct)
    {
        var match = await DbContext.BankReconciliationMatches.FirstOrDefaultAsync(x => x.ReconciliationId == reconciliationId && x.BankStatementId == statementId, ct);
        if (match is null) return BaseResponseDto<object>.NotFound("Match was not found.");
        var statement = await DbContext.BankStatements.FirstAsync(x => x.Id == statementId, ct);
        DbContext.BankReconciliationMatches.Remove(match);
        statement.IsMatched = false;
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<object>.Ok(null);
    }

    public async Task<BaseResponseDto<IReadOnlyList<ReconciliationDifferenceDto>>> GetDifferencesAsync(Guid reconciliationId, CancellationToken ct)
    {
        var reconciliation = await DbContext.BankReconciliations.FirstOrDefaultAsync(x => x.Id == reconciliationId, ct);
        if (reconciliation is null) return BaseResponseDto<IReadOnlyList<ReconciliationDifferenceDto>>.NotFound("Reconciliation was not found.");
        var rows = await DbContext.BankStatements.Where(x => x.BankAccountId == reconciliation.BankAccountId && !x.IsMatched)
            .Select(x => new ReconciliationDifferenceDto(x.Id, x.StatementDate, x.Description, x.Debit - x.Credit, x.ReferenceNo)).ToListAsync(ct);
        return BaseResponseDto<IReadOnlyList<ReconciliationDifferenceDto>>.Ok(rows);
    }
}

public sealed class FixedAssetService(
    AppDbContext db,
    ICurrentTenantService tenant,
    ICurrentUserService user,
    IDocumentNumberService numbers) : AccountingServiceBase(db, tenant), IFixedAssetService
{
    public async Task<BaseResponseDto<FixedAssetDto>> CreateAsync(FixedAssetRequest request, CancellationToken ct)
    {
        if (request.PurchaseCost <= 0 || request.UsefulLifeMonths <= 0 || request.SalvageValue < 0 || request.SalvageValue >= request.PurchaseCost)
            return BaseResponseDto<FixedAssetDto>.Fail("Invalid asset depreciation values.");
        var accountIds = new[] { request.AssetAccountId, request.DepreciationExpenseAccountId, request.AccumulatedDepreciationAccountId };
        if (await DbContext.Accounts.CountAsync(x => accountIds.Contains(x.Id) && x.IsActive && x.IsPostingAccount, ct) != accountIds.Distinct().Count())
            return BaseResponseDto<FixedAssetDto>.Fail("All asset accounts must be active posting accounts.");
        var item = new FixedAsset
        {
            AssetCode = request.AssetCode, AssetNameAr = request.AssetNameAr, AssetNameEn = request.AssetNameEn,
            PurchaseDate = request.PurchaseDate, PurchaseCost = request.PurchaseCost, UsefulLifeMonths = request.UsefulLifeMonths,
            SalvageValue = request.SalvageValue, BookValue = request.PurchaseCost, AssetAccountId = request.AssetAccountId,
            DepreciationExpenseAccountId = request.DepreciationExpenseAccountId,
            AccumulatedDepreciationAccountId = request.AccumulatedDepreciationAccountId
        };
        DbContext.FixedAssets.Add(item);
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<FixedAssetDto>.Ok(ToDto(item));
    }

    public async Task<BaseResponseDto<DepreciationRunDto>> RunAsync(Guid accountingPeriodId, CancellationToken ct)
    {
        if (await DbContext.AssetDepreciationRuns.AnyAsync(x => x.AccountingPeriodId == accountingPeriodId, ct))
            return BaseResponseDto<DepreciationRunDto>.Fail("Depreciation was already run for this period.");
        var period = await DbContext.AccountingPeriods.FirstOrDefaultAsync(x => x.Id == accountingPeriodId && x.Status == AccountingPeriodStatus.Open, ct);
        if (period is null) return BaseResponseDto<DepreciationRunDto>.Fail("Accounting period is not open.");
        var assets = await DbContext.FixedAssets.Where(x => x.Status == FixedAssetStatus.Active && x.PurchaseDate <= period.EndDate).ToListAsync(ct);
        var run = new AssetDepreciationRun { AccountingPeriodId = accountingPeriodId, RunDate = period.EndDate };
        foreach (var asset in assets)
        {
            var monthly = Math.Round((asset.PurchaseCost - asset.SalvageValue) / asset.UsefulLifeMonths, 2);
            var amount = Math.Min(monthly, Math.Max(0, asset.BookValue - asset.SalvageValue));
            if (amount > 0) run.Lines.Add(new AssetDepreciationLine { AssetId = asset.Id, DepreciationAmount = amount });
        }
        DbContext.AssetDepreciationRuns.Add(run);
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<DepreciationRunDto>.Ok(ToDto(run));
    }

    public async Task<BaseResponseDto<DepreciationRunDto>> ApproveAsync(Guid runId, CancellationToken ct)
    {
        var run = await DbContext.AssetDepreciationRuns.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == runId, ct);
        if (run is null) return BaseResponseDto<DepreciationRunDto>.NotFound("Depreciation run was not found.");
        if (run.Status != DepreciationRunStatus.Draft) return BaseResponseDto<DepreciationRunDto>.Fail("Only draft runs can be approved.");
        var period = await DbContext.AccountingPeriods.Include(x => x.FinancialYear).FirstAsync(x => x.Id == run.AccountingPeriodId, ct);
        if (period.Status != AccountingPeriodStatus.Open) return BaseResponseDto<DepreciationRunDto>.Fail("Accounting period is not open.");
        var assets = await DbContext.FixedAssets.Where(x => run.Lines.Select(l => l.AssetId).Contains(x.Id)).ToListAsync(ct);
        var lines = new List<JournalEntryLine>();
        foreach (var depreciation in run.Lines)
        {
            var asset = assets.First(x => x.Id == depreciation.AssetId);
            lines.Add(new JournalEntryLine { AccountId = asset.DepreciationExpenseAccountId, Debit = depreciation.DepreciationAmount, Description = asset.AssetCode });
            lines.Add(new JournalEntryLine { AccountId = asset.AccumulatedDepreciationAccountId, Credit = depreciation.DepreciationAmount, Description = asset.AssetCode });
            asset.AccumulatedDepreciation += depreciation.DepreciationAmount;
            asset.BookValue -= depreciation.DepreciationAmount;
            if (asset.BookValue <= asset.SalvageValue) asset.Status = FixedAssetStatus.FullyDepreciated;
        }
        var total = run.Lines.Sum(x => x.DepreciationAmount);
        var entry = new JournalEntry
        {
            FinancialYearId = period.FinancialYearId, AccountingPeriodId = period.Id, EntryDate = period.EndDate,
            EntryNumber = await numbers.GenerateAsync("JE", period.EndDate, null, ct), Description = $"Depreciation {period.PeriodName}",
            TotalDebit = total, TotalCredit = total, WorkflowStatus = WorkflowStatus.Approved, Lines = lines
        };
        DbContext.JournalEntries.Add(entry);
        run.Status = DepreciationRunStatus.Approved;
        run.ApprovedByUserId = user.UserId;
        await DbContext.SaveChangesAsync(ct);
        foreach (var line in run.Lines) line.JournalEntryId = entry.Id;
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<DepreciationRunDto>.Ok(ToDto(run));
    }

    private static FixedAssetDto ToDto(FixedAsset x) => new(x.Id, x.AssetCode, x.AssetNameAr, x.AssetNameEn, x.PurchaseCost, x.AccumulatedDepreciation, x.BookValue, x.Status);
    private static DepreciationRunDto ToDto(AssetDepreciationRun x) => new(x.Id, x.AccountingPeriodId, x.RunDate, x.Status, x.Lines.Sum(l => l.DepreciationAmount));
}

public sealed class RecurringJournalService(
    AppDbContext db,
    ICurrentTenantService tenant,
    IDocumentNumberService numbers) : AccountingServiceBase(db, tenant), IRecurringJournalService
{
    public async Task<BaseResponseDto<RecurringJournalDto>> CreateAsync(CreateRecurringJournalRequest request, CancellationToken ct)
    {
        var debit = request.Lines.Sum(x => x.Debit);
        var credit = request.Lines.Sum(x => x.Credit);
        if (request.Lines.Count < 2 || debit <= 0 || debit != credit)
            return BaseResponseDto<RecurringJournalDto>.Fail("Recurring journal must be balanced.");
        var item = new RecurringJournalEntry
        {
            Name = request.Name, Frequency = request.Frequency, StartDate = request.StartDate, EndDate = request.EndDate,
            NextRunDate = request.NextRunDate, Lines = request.Lines.Select(x => new RecurringJournalEntryLine
            {
                AccountId = x.AccountId, Debit = x.Debit, Credit = x.Credit, CostCenterId = x.CostCenterId, Description = x.Description
            }).ToList()
        };
        DbContext.RecurringJournalEntries.Add(item);
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<RecurringJournalDto>.Ok(ToDto(item));
    }

    public async Task<BaseResponseDto<int>> GenerateDueAsync(DateOnly runDate, CancellationToken ct)
    {
        var templates = await DbContext.RecurringJournalEntries.Include(x => x.Lines)
            .Where(x => x.IsActive && x.NextRunDate <= runDate && (!x.EndDate.HasValue || x.EndDate >= x.NextRunDate)).ToListAsync(ct);
        var generated = 0;
        foreach (var template in templates)
        {
            var period = await DbContext.AccountingPeriods.FirstOrDefaultAsync(x => x.StartDate <= template.NextRunDate && x.EndDate >= template.NextRunDate && x.Status == AccountingPeriodStatus.Open, ct);
            if (period is null || await DbContext.GeneratedRecurringEntries.AnyAsync(x => x.RecurringJournalEntryId == template.Id && x.AccountingPeriodId == period.Id, ct))
                continue;
            var entry = new JournalEntry
            {
                FinancialYearId = period.FinancialYearId, AccountingPeriodId = period.Id, EntryDate = template.NextRunDate,
                EntryNumber = await numbers.GenerateAsync("JE", template.NextRunDate, null, ct), Description = template.Name,
                TotalDebit = template.Lines.Sum(x => x.Debit), TotalCredit = template.Lines.Sum(x => x.Credit),
                Lines = template.Lines.Select(x => new JournalEntryLine { AccountId = x.AccountId, Debit = x.Debit, Credit = x.Credit, CostCenterId = x.CostCenterId, Description = x.Description }).ToList()
            };
            DbContext.JournalEntries.Add(entry);
            await DbContext.SaveChangesAsync(ct);
            DbContext.GeneratedRecurringEntries.Add(new GeneratedRecurringEntry { RecurringJournalEntryId = template.Id, JournalEntryId = entry.Id, GeneratedDate = DateOnly.FromDateTime(DateTime.UtcNow), AccountingPeriodId = period.Id });
            template.NextRunDate = template.Frequency switch
            {
                RecurringFrequency.Monthly => template.NextRunDate.AddMonths(1),
                RecurringFrequency.Quarterly => template.NextRunDate.AddMonths(3),
                _ => template.NextRunDate.AddYears(1)
            };
            generated++;
            await DbContext.SaveChangesAsync(ct);
        }
        return BaseResponseDto<int>.Ok(generated);
    }

    private static RecurringJournalDto ToDto(RecurringJournalEntry x) => new(x.Id, x.Name, x.Frequency, x.NextRunDate, x.IsActive);
}

public sealed class ClosingAssistantService(
    AppDbContext db,
    ICurrentTenantService tenant) : AccountingServiceBase(db, tenant), IClosingAssistantService
{
    public async Task<BaseResponseDto<IReadOnlyList<ClosingCheckDto>>> RunAsync(Guid accountingPeriodId, CancellationToken ct)
    {
        var period = await DbContext.AccountingPeriods.FirstOrDefaultAsync(x => x.Id == accountingPeriodId, ct);
        if (period is null) return BaseResponseDto<IReadOnlyList<ClosingCheckDto>>.NotFound("Accounting period was not found.");
        var checks = new List<(string Key, string Ar, string En, ClosingCheckStatus Status, string Message)>
        {
            ("DRAFT_ENTRIES", "قيود مسودة", "Draft entries", await ExistsEntry(WorkflowStatus.Draft, JournalEntryStatus.Draft, accountingPeriodId, ct) ? ClosingCheckStatus.Failed : ClosingCheckStatus.Passed, "Draft journal entries must be completed."),
            ("SUBMITTED_ENTRIES", "قيود قيد المراجعة", "Submitted entries", await DbContext.JournalEntries.AnyAsync(x => x.AccountingPeriodId == accountingPeriodId && (x.WorkflowStatus == WorkflowStatus.Submitted || x.WorkflowStatus == WorkflowStatus.UnderReview), ct) ? ClosingCheckStatus.Failed : ClosingCheckStatus.Passed, "Submitted journal entries must be approved."),
            ("APPROVED_NOT_POSTED", "قيود معتمدة غير مرحلة", "Approved entries not posted", await DbContext.JournalEntries.AnyAsync(x => x.AccountingPeriodId == accountingPeriodId && x.WorkflowStatus == WorkflowStatus.Approved && x.Status != JournalEntryStatus.Posted, ct) ? ClosingCheckStatus.Failed : ClosingCheckStatus.Passed, "Approved journal entries must be posted."),
            ("TRIAL_BALANCE", "توازن ميزان المراجعة", "Trial balance", await IsTrialBalanced(accountingPeriodId, ct) ? ClosingCheckStatus.Passed : ClosingCheckStatus.Failed, "Trial balance must be balanced."),
            ("RECURRING", "القيود المتكررة", "Recurring entries", await HasMissingRecurring(period, ct) ? ClosingCheckStatus.Warning : ClosingCheckStatus.Passed, "Some recurring entries were not generated."),
            ("DEPRECIATION", "إهلاك الأصول", "Asset depreciation", await DbContext.FixedAssets.AnyAsync(x => x.Status == FixedAssetStatus.Active, ct) && !await DbContext.AssetDepreciationRuns.AnyAsync(x => x.AccountingPeriodId == accountingPeriodId && x.Status >= DepreciationRunStatus.Approved, ct) ? ClosingCheckStatus.Warning : ClosingCheckStatus.Passed, "Depreciation run is missing."),
            ("BANK_RECONCILIATION", "التسويات البنكية", "Bank reconciliation", await DbContext.BankReconciliations.AnyAsync(x => x.AccountingPeriodId == accountingPeriodId && x.Status < ReconciliationStatus.Completed, ct) ? ClosingCheckStatus.Warning : ClosingCheckStatus.Passed, "Bank reconciliation is incomplete."),
            ("OPENING_BALANCE", "الأرصدة الافتتاحية", "Opening balance", await DbContext.OpeningBalanceBatches.AnyAsync(x => x.FinancialYearId == period.FinancialYearId && x.Status < OpeningBalanceStatus.Approved, ct) ? ClosingCheckStatus.Warning : ClosingCheckStatus.Passed, "Opening balance is not approved.")
        };
        var existing = await DbContext.ClosingChecks.Where(x => x.AccountingPeriodId == accountingPeriodId).ToListAsync(ct);
        var entities = new List<ClosingCheck>();
        foreach (var check in checks)
        {
            var entity = existing.FirstOrDefault(x => x.CheckKey == check.Key);
            if (entity is null)
            {
                entity = new ClosingCheck { AccountingPeriodId = accountingPeriodId, CheckKey = check.Key };
                DbContext.ClosingChecks.Add(entity);
            }
            entity.CheckNameAr = check.Ar;
            entity.CheckNameEn = check.En;
            entity.Status = check.Status;
            entity.MessageAr = check.Message;
            entity.MessageEn = check.Message;
            entities.Add(entity);
        }
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<IReadOnlyList<ClosingCheckDto>>.Ok(entities.Select(ToDto).ToList());
    }

    public Task<bool> HasBlockingFailuresAsync(Guid accountingPeriodId, CancellationToken ct) =>
        DbContext.ClosingChecks.AnyAsync(x => x.AccountingPeriodId == accountingPeriodId && x.Status == ClosingCheckStatus.Failed, ct);

    private Task<bool> ExistsEntry(WorkflowStatus workflow, JournalEntryStatus status, Guid periodId, CancellationToken ct) =>
        DbContext.JournalEntries.AnyAsync(x => x.AccountingPeriodId == periodId && x.WorkflowStatus == workflow && x.Status == status, ct);

    private async Task<bool> IsTrialBalanced(Guid periodId, CancellationToken ct)
    {
        var totals = await DbContext.JournalEntries.Where(x => x.AccountingPeriodId == periodId && x.Status == JournalEntryStatus.Posted)
            .GroupBy(_ => 1).Select(g => new { Debit = g.Sum(x => x.TotalDebit), Credit = g.Sum(x => x.TotalCredit) }).FirstOrDefaultAsync(ct);
        return totals is null || totals.Debit == totals.Credit;
    }

    private Task<bool> HasMissingRecurring(AccountingPeriod period, CancellationToken ct) =>
        DbContext.RecurringJournalEntries.AnyAsync(x => x.IsActive && x.NextRunDate <= period.EndDate &&
            !DbContext.GeneratedRecurringEntries.Any(g => g.RecurringJournalEntryId == x.Id && g.AccountingPeriodId == period.Id), ct);

    private static ClosingCheckDto ToDto(ClosingCheck x) => new(x.Id, x.CheckKey, x.CheckNameAr, x.CheckNameEn, x.Status, x.MessageAr, x.MessageEn);
}

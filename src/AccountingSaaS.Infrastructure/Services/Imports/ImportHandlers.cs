using System.Globalization;
using System.Text.Json;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class ImportHandlerFactory(IEnumerable<IImportHandler> handlers) : IImportHandlerFactory
{
    public IImportHandler GetHandler(ImportType importType) =>
        handlers.FirstOrDefault(x => x.SupportedType == importType)
        ?? throw new InvalidOperationException($"No import handler registered for {importType}.");
}

public abstract class ImportHandlerBase(AppDbContext dbContext) : IImportHandler
{
    protected AppDbContext DbContext { get; } = dbContext;
    public abstract ImportType SupportedType { get; }
    public abstract IReadOnlyList<string> TemplateHeaders { get; }
    protected virtual IReadOnlyList<string> RequiredHeaders => [];

    public abstract Task<IReadOnlyList<ImportRowValidationResult>> ValidateRowsAsync(ImportValidationContext context, IReadOnlyList<ExcelRowData> rows, CancellationToken cancellationToken);
    public abstract Task<ImportConfirmResult> ConfirmImportAsync(Guid batchId, CancellationToken cancellationToken);

    public virtual ImportTemplateDto GenerateTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Template");
        for (var i = 0; i < TemplateHeaders.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = TemplateHeaders[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var instructions = workbook.Worksheets.Add("Instructions");
        instructions.Cell(1, 1).Value = "Required columns";
        instructions.Cell(2, 1).Value = string.Join(", ", RequiredHeaders);
        instructions.Cell(4, 1).Value = "Use .xlsx only. Dates should be yyyy-MM-dd. Decimals should use dot as separator.";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ImportTemplateDto($"{SupportedType}-template.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", stream.ToArray());
    }

    protected ImportRowValidationResult ValidateRequired(ExcelRowData row, Dictionary<string, string?> normalized, List<string> errors, List<string> warnings)
    {
        foreach (var header in RequiredHeaders)
        {
            if (string.IsNullOrWhiteSpace(Get(row.Values, header)))
            {
                errors.Add($"{header} is required.");
            }
        }

        foreach (var header in row.Values.Keys.Where(x => !TemplateHeaders.Contains(x, StringComparer.OrdinalIgnoreCase)))
        {
            warnings.Add($"Unknown column ignored: {header}");
        }

        return Result(row, normalized, errors, warnings);
    }

    protected static ImportRowValidationResult Result(ExcelRowData row, Dictionary<string, string?> normalized, List<string> errors, List<string> warnings)
    {
        var status = errors.Count > 0 ? ImportRowStatus.Invalid : warnings.Count > 0 ? ImportRowStatus.Warning : ImportRowStatus.Valid;
        return new ImportRowValidationResult(row.RowNumber, row.Values, normalized, status, errors, warnings);
    }

    protected static string? Get(IReadOnlyDictionary<string, string?> row, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (row.TryGetValue(alias, out var value))
            {
                return value?.Trim();
            }
        }

        return null;
    }

    protected static bool TryParseBool(string? value, out bool result)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (normalized is "true" or "yes" or "1" or "y" or "نعم")
        {
            result = true;
            return true;
        }

        if (normalized is "false" or "no" or "0" or "n" or "لا")
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }

    protected static bool TryParseDecimal(string? value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
        || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result);

    protected async Task<List<ImportBatchRow>> ValidRowsAsync(Guid batchId, CancellationToken cancellationToken) =>
        await DbContext.ImportBatchRows.Where(x => x.ImportBatchId == batchId && (x.Status == ImportRowStatus.Valid || x.Status == ImportRowStatus.Warning)).OrderBy(x => x.RowNumber).ToListAsync(cancellationToken);

    protected static Dictionary<string, string?> JsonToDictionary(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? [];
}

public sealed class ChartOfAccountsImportHandler(AppDbContext dbContext) : ImportHandlerBase(dbContext)
{
    public override ImportType SupportedType => ImportType.ChartOfAccounts;
    public override IReadOnlyList<string> TemplateHeaders => ["Code", "NameAr", "NameEn", "AccountType", "NormalBalance", "ParentCode", "IsPostingAccount", "IsActive"];
    protected override IReadOnlyList<string> RequiredHeaders => ["Code", "NameAr", "AccountType", "NormalBalance", "IsPostingAccount"];

    public override async Task<IReadOnlyList<ImportRowValidationResult>> ValidateRowsAsync(ImportValidationContext context, IReadOnlyList<ExcelRowData> rows, CancellationToken cancellationToken)
    {
        var dbCodes = await DbContext.Accounts.Select(x => x.Code).ToListAsync(cancellationToken);
        var batchCodes = rows.Select(x => Get(x.Values, "Code")).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return rows.Select(row =>
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var normalized = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var code = Get(row.Values, "Code");
            var parentCode = Get(row.Values, "ParentCode");
            if (!string.IsNullOrWhiteSpace(code) && dbCodes.Contains(code, StringComparer.OrdinalIgnoreCase)) errors.Add("Code already exists.");
            if (!string.IsNullOrWhiteSpace(parentCode) && !dbCodes.Contains(parentCode, StringComparer.OrdinalIgnoreCase) && !batchCodes.Contains(parentCode)) errors.Add("ParentCode was not found.");
            if (!Enum.TryParse<AccountType>(Get(row.Values, "AccountType"), true, out var accountType)) errors.Add("AccountType is invalid.");
            if (!Enum.TryParse<NormalBalance>(Get(row.Values, "NormalBalance"), true, out var normalBalance)) errors.Add("NormalBalance is invalid.");
            if (!TryParseBool(Get(row.Values, "IsPostingAccount"), out var isPosting)) errors.Add("IsPostingAccount is invalid.");
            var isActive = !TryParseBool(Get(row.Values, "IsActive"), out var parsedActive) || parsedActive;
            normalized["Code"] = code; normalized["NameAr"] = Get(row.Values, "NameAr"); normalized["NameEn"] = Get(row.Values, "NameEn"); normalized["AccountType"] = accountType.ToString(); normalized["NormalBalance"] = normalBalance.ToString(); normalized["ParentCode"] = parentCode; normalized["IsPostingAccount"] = isPosting.ToString(); normalized["IsActive"] = isActive.ToString();
            ValidateRequired(row, normalized, errors, warnings);
            return Result(row, normalized, errors, warnings);
        }).ToList();
    }

    public override async Task<ImportConfirmResult> ConfirmImportAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var rows = await ValidRowsAsync(batchId, cancellationToken);
        var created = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var data = JsonToDictionary(row.NormalizedJson!);
            var parent = !string.IsNullOrWhiteSpace(data["ParentCode"]) ? await DbContext.Accounts.FirstOrDefaultAsync(x => x.Code == data["ParentCode"], cancellationToken) ?? created.GetValueOrDefault(data["ParentCode"]!) : null;
            var account = new Account { Code = data["Code"]!, NameAr = data["NameAr"]!, NameEn = data["NameEn"] ?? data["NameAr"]!, AccountType = Enum.Parse<AccountType>(data["AccountType"]!), NormalBalance = Enum.Parse<NormalBalance>(data["NormalBalance"]!), ParentAccountId = parent?.Id, IsPostingAccount = bool.Parse(data["IsPostingAccount"]!), IsActive = bool.Parse(data["IsActive"]!) };
            DbContext.Accounts.Add(account);
            created[account.Code] = account;
            row.Status = ImportRowStatus.Imported; row.ImportedEntityName = nameof(Account); row.ImportedEntityId = account.Id.ToString();
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        return new ImportConfirmResult(rows.Count);
    }
}

public sealed class CostCentersImportHandler(AppDbContext dbContext) : ImportHandlerBase(dbContext)
{
    public override ImportType SupportedType => ImportType.CostCenters;
    public override IReadOnlyList<string> TemplateHeaders => ["Code", "Name", "IsActive"];
    protected override IReadOnlyList<string> RequiredHeaders => ["Code", "Name"];

    public override async Task<IReadOnlyList<ImportRowValidationResult>> ValidateRowsAsync(ImportValidationContext context, IReadOnlyList<ExcelRowData> rows, CancellationToken cancellationToken)
    {
        var dbCodes = await DbContext.CostCenters.Select(x => x.Code).ToListAsync(cancellationToken);
        return rows.Select(row =>
        {
            var errors = new List<string>(); var warnings = new List<string>();
            var code = Get(row.Values, "Code");
            if (!string.IsNullOrWhiteSpace(code) && dbCodes.Contains(code, StringComparer.OrdinalIgnoreCase)) errors.Add("Code already exists.");
            var active = !TryParseBool(Get(row.Values, "IsActive"), out var parsedActive) || parsedActive;
            var normalized = new Dictionary<string, string?> { ["Code"] = code, ["Name"] = Get(row.Values, "Name"), ["IsActive"] = active.ToString() };
            ValidateRequired(row, normalized, errors, warnings);
            return Result(row, normalized, errors, warnings);
        }).ToList();
    }

    public override async Task<ImportConfirmResult> ConfirmImportAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var rows = await ValidRowsAsync(batchId, cancellationToken);
        foreach (var row in rows)
        {
            var data = JsonToDictionary(row.NormalizedJson!);
            var entity = new CostCenter { Code = data["Code"]!, Name = data["Name"]!, IsActive = bool.Parse(data["IsActive"]!) };
            DbContext.CostCenters.Add(entity);
            row.Status = ImportRowStatus.Imported; row.ImportedEntityName = nameof(CostCenter); row.ImportedEntityId = entity.Id.ToString();
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        return new ImportConfirmResult(rows.Count);
    }
}

public sealed class OpeningBalancesImportHandler(AppDbContext dbContext) : ImportHandlerBase(dbContext)
{
    public override ImportType SupportedType => ImportType.OpeningBalances;
    public override IReadOnlyList<string> TemplateHeaders => ["AccountCode", "Debit", "Credit", "CostCenterCode", "Description"];
    protected override IReadOnlyList<string> RequiredHeaders => ["AccountCode", "Debit", "Credit"];

    public override async Task<IReadOnlyList<ImportRowValidationResult>> ValidateRowsAsync(ImportValidationContext context, IReadOnlyList<ExcelRowData> rows, CancellationToken cancellationToken)
    {
        if (context.FinancialYearId is null)
        {
            return rows.Select(r => Result(r, [], ["FinancialYearId is required for opening balances."], [])).ToList();
        }

        var yearIsOpen = await DbContext.FinancialYears.AnyAsync(x => x.Id == context.FinancialYearId && x.Status == FinancialYearStatus.Open, cancellationToken);
        var accounts = await DbContext.Accounts.Where(x => x.IsActive && x.IsPostingAccount).ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        decimal totalDebit = 0, totalCredit = 0;
        var results = rows.Select(row =>
        {
            var errors = new List<string>(); var warnings = new List<string>();
            var accountCode = Get(row.Values, "AccountCode");
            if (!yearIsOpen) errors.Add("Financial year is closed or not found.");
            if (string.IsNullOrWhiteSpace(accountCode) || !accounts.ContainsKey(accountCode)) errors.Add("AccountCode was not found or is not an active posting account.");
            if (!TryParseDecimal(Get(row.Values, "Debit"), out var debit)) errors.Add("Debit is invalid.");
            if (!TryParseDecimal(Get(row.Values, "Credit"), out var credit)) errors.Add("Credit is invalid.");
            if (debit > 0 && credit > 0) errors.Add("Debit and Credit cannot both be greater than zero.");
            if (debit == 0 && credit == 0) errors.Add("Debit and Credit cannot both be zero.");
            totalDebit += debit; totalCredit += credit;
            var normalized = new Dictionary<string, string?> { ["AccountCode"] = accountCode, ["Debit"] = debit.ToString(CultureInfo.InvariantCulture), ["Credit"] = credit.ToString(CultureInfo.InvariantCulture), ["CostCenterCode"] = Get(row.Values, "CostCenterCode"), ["Description"] = Get(row.Values, "Description") };
            ValidateRequired(row, normalized, errors, warnings);
            return Result(row, normalized, errors, warnings);
        }).ToList();

        if (totalDebit != totalCredit)
        {
            results.Add(new ImportRowValidationResult(0, new Dictionary<string, string?>(), null, ImportRowStatus.Invalid, [$"Opening balances are not balanced. Debit={totalDebit}, Credit={totalCredit}."], []));
        }

        return results;
    }

    public override async Task<ImportConfirmResult> ConfirmImportAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await DbContext.ImportBatches.FindAsync([batchId], cancellationToken);
        var rows = await ValidRowsAsync(batchId, cancellationToken);
        if (batch is null || rows.Count == 0) return new ImportConfirmResult(0);
        if (batch.FinancialYearId is not { } yearId)
        {
            throw new InvalidOperationException("FinancialYearId is required for opening balances.");
        }

        var yearIsOpen = await DbContext.FinancialYears.AnyAsync(x => x.Id == yearId && x.Status == FinancialYearStatus.Open, cancellationToken);
        if (!yearIsOpen)
        {
            throw new InvalidOperationException("Selected financial year is closed or not found.");
        }

        var periodQuery = DbContext.AccountingPeriods.Where(x => x.FinancialYearId == yearId && x.Status == AccountingPeriodStatus.Open);
        if (batch.AccountingPeriodId.HasValue)
        {
            periodQuery = periodQuery.Where(x => x.Id == batch.AccountingPeriodId.Value);
        }

        var period = await periodQuery.OrderBy(x => x.StartDate).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("An open accounting period is required for opening balances.");

        var entry = new JournalEntry { FinancialYearId = yearId, AccountingPeriodId = period.Id, EntryDate = period.StartDate, EntryNumber = $"OB-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", Description = "Opening balances import", Status = JournalEntryStatus.Draft };
        foreach (var row in rows)
        {
            var data = JsonToDictionary(row.NormalizedJson!);
            var account = await DbContext.Accounts.FirstAsync(x => x.Code == data["AccountCode"], cancellationToken);
            entry.Lines.Add(new JournalEntryLine { AccountId = account.Id, Debit = decimal.Parse(data["Debit"]!, CultureInfo.InvariantCulture), Credit = decimal.Parse(data["Credit"]!, CultureInfo.InvariantCulture), Description = data["Description"] });
            row.Status = ImportRowStatus.Imported; row.ImportedEntityName = nameof(JournalEntry);
        }

        entry.TotalDebit = entry.Lines.Sum(x => x.Debit); entry.TotalCredit = entry.Lines.Sum(x => x.Credit);
        DbContext.JournalEntries.Add(entry);
        await DbContext.SaveChangesAsync(cancellationToken);
        foreach (var row in rows) row.ImportedEntityId = entry.Id.ToString();
        await DbContext.SaveChangesAsync(cancellationToken);
        return new ImportConfirmResult(rows.Count);
    }
}

public sealed class JournalEntriesImportHandler(AppDbContext dbContext) : ImportHandlerBase(dbContext)
{
    public override ImportType SupportedType => ImportType.JournalEntries;
    public override IReadOnlyList<string> TemplateHeaders => ["EntryDate", "EntryNumber", "AccountCode", "CostCenterCode", "Debit", "Credit", "Description", "LineDescription"];
    protected override IReadOnlyList<string> RequiredHeaders => ["EntryDate", "EntryNumber", "AccountCode", "Debit", "Credit"];

    public override async Task<IReadOnlyList<ImportRowValidationResult>> ValidateRowsAsync(ImportValidationContext context, IReadOnlyList<ExcelRowData> rows, CancellationToken cancellationToken)
    {
        if (context.FinancialYearId is null || context.AccountingPeriodId is null)
            return rows.Select(r => Result(r, [], ["FinancialYearId and AccountingPeriodId are required."], [])).ToList();
        var period = await DbContext.AccountingPeriods.FirstOrDefaultAsync(x => x.Id == context.AccountingPeriodId && x.FinancialYearId == context.FinancialYearId, cancellationToken);
        var accounts = await DbContext.Accounts.Where(x => x.IsActive && x.IsPostingAccount).ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var costCenters = await DbContext.CostCenters.Where(x => x.IsActive).Select(x => x.Code).ToListAsync(cancellationToken);
        var existingNumbers = await DbContext.JournalEntries.Where(x => x.FinancialYearId == context.FinancialYearId).Select(x => x.EntryNumber).ToListAsync(cancellationToken);
        var results = new List<ImportRowValidationResult>();
        foreach (var row in rows)
        {
            var errors = new List<string>(); var warnings = new List<string>();
            var number = Get(row.Values, "EntryNumber");
            var accountCode = Get(row.Values, "AccountCode");
            var costCenterCode = Get(row.Values, "CostCenterCode");
            if (period is null || period.Status != AccountingPeriodStatus.Open) errors.Add("Selected accounting period is not open.");
            if (!DateOnly.TryParse(Get(row.Values, "EntryDate"), out var entryDate)) errors.Add("EntryDate is invalid.");
            else if (period is not null && (entryDate < period.StartDate || entryDate > period.EndDate)) errors.Add("EntryDate is outside the selected period.");
            if (!string.IsNullOrWhiteSpace(number) && existingNumbers.Contains(number, StringComparer.OrdinalIgnoreCase)) errors.Add("EntryNumber already exists.");
            if (string.IsNullOrWhiteSpace(accountCode) || !accounts.ContainsKey(accountCode)) errors.Add("AccountCode was not found or is not active posting.");
            if (!string.IsNullOrWhiteSpace(costCenterCode) && !costCenters.Contains(costCenterCode, StringComparer.OrdinalIgnoreCase)) errors.Add("CostCenterCode was not found.");
            if (!TryParseDecimal(Get(row.Values, "Debit"), out var debit)) errors.Add("Debit is invalid.");
            if (!TryParseDecimal(Get(row.Values, "Credit"), out var credit)) errors.Add("Credit is invalid.");
            if (debit > 0 && credit > 0) errors.Add("Debit and Credit cannot both be greater than zero.");
            if (debit == 0 && credit == 0) errors.Add("Debit and Credit cannot both be zero.");
            var normalized = new Dictionary<string, string?> { ["EntryDate"] = entryDate.ToString("yyyy-MM-dd"), ["EntryNumber"] = number, ["AccountCode"] = accountCode, ["CostCenterCode"] = costCenterCode, ["Debit"] = debit.ToString(CultureInfo.InvariantCulture), ["Credit"] = credit.ToString(CultureInfo.InvariantCulture), ["Description"] = Get(row.Values, "Description"), ["LineDescription"] = Get(row.Values, "LineDescription") };
            ValidateRequired(row, normalized, errors, warnings);
            results.Add(Result(row, normalized, errors, warnings));
        }

        foreach (var group in results.Where(x => x.Status != ImportRowStatus.Invalid).GroupBy(x => x.NormalizedData!["EntryNumber"]))
        {
            if (group.Count() < 2 || group.Sum(x => decimal.Parse(x.NormalizedData!["Debit"]!, CultureInfo.InvariantCulture)) != group.Sum(x => decimal.Parse(x.NormalizedData!["Credit"]!, CultureInfo.InvariantCulture)))
            {
                results.Add(new ImportRowValidationResult(0, new Dictionary<string, string?>(), null, ImportRowStatus.Invalid, [$"EntryNumber {group.Key} is not balanced or has fewer than two lines."], []));
            }
        }

        return results;
    }

    public override async Task<ImportConfirmResult> ConfirmImportAsync(Guid batchId, CancellationToken cancellationToken)
    {
        var batch = await DbContext.ImportBatches.FindAsync([batchId], cancellationToken);
        var rows = await ValidRowsAsync(batchId, cancellationToken);
        if (batch is null) return new ImportConfirmResult(0);
        if (batch.FinancialYearId is not { } yearId || batch.AccountingPeriodId is not { } periodId)
        {
            throw new InvalidOperationException("FinancialYearId and AccountingPeriodId are required for journal entry imports.");
        }

        var period = await DbContext.AccountingPeriods.FirstOrDefaultAsync(x => x.Id == periodId && x.FinancialYearId == yearId && x.Status == AccountingPeriodStatus.Open, cancellationToken)
            ?? throw new InvalidOperationException("Selected accounting period is not open.");

        foreach (var group in rows.GroupBy(r => JsonToDictionary(r.NormalizedJson!)["EntryNumber"]))
        {
            var first = JsonToDictionary(group.First().NormalizedJson!);
            var entry = new JournalEntry { FinancialYearId = period.FinancialYearId, AccountingPeriodId = period.Id, EntryNumber = group.Key!, EntryDate = DateOnly.Parse(first["EntryDate"]!), Description = first["Description"] ?? $"Imported {group.Key}", Status = JournalEntryStatus.Draft };
            foreach (var row in group)
            {
                var data = JsonToDictionary(row.NormalizedJson!);
                var account = await DbContext.Accounts.FirstAsync(x => x.Code == data["AccountCode"], cancellationToken);
                var costCenter = !string.IsNullOrWhiteSpace(data["CostCenterCode"]) ? await DbContext.CostCenters.FirstOrDefaultAsync(x => x.Code == data["CostCenterCode"], cancellationToken) : null;
                entry.Lines.Add(new JournalEntryLine { AccountId = account.Id, CostCenterId = costCenter?.Id, Debit = decimal.Parse(data["Debit"]!, CultureInfo.InvariantCulture), Credit = decimal.Parse(data["Credit"]!, CultureInfo.InvariantCulture), Description = data["LineDescription"] });
                row.Status = ImportRowStatus.Imported; row.ImportedEntityName = nameof(JournalEntry);
            }
            entry.TotalDebit = entry.Lines.Sum(x => x.Debit); entry.TotalCredit = entry.Lines.Sum(x => x.Credit);
            DbContext.JournalEntries.Add(entry);
            await DbContext.SaveChangesAsync(cancellationToken);
            foreach (var row in group) row.ImportedEntityId = entry.Id.ToString();
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        return new ImportConfirmResult(rows.Count);
    }
}

public class StagingOnlyImportHandler(AppDbContext dbContext, ImportType type, string[] headers, string[] requiredHeaders) : ImportHandlerBase(dbContext)
{
    public override ImportType SupportedType => type;
    public override IReadOnlyList<string> TemplateHeaders => headers;
    protected override IReadOnlyList<string> RequiredHeaders => requiredHeaders;

    public override Task<IReadOnlyList<ImportRowValidationResult>> ValidateRowsAsync(ImportValidationContext context, IReadOnlyList<ExcelRowData> rows, CancellationToken cancellationToken)
    {
        IReadOnlyList<ImportRowValidationResult> result = rows.Select(row =>
        {
            var errors = new List<string>(); var warnings = new List<string> { "This import type is staged only because its final module is not implemented yet." };
            var normalized = row.Values.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            if ((SupportedType is ImportType.Customers or ImportType.Suppliers) && !string.IsNullOrWhiteSpace(Get(row.Values, "Email")) && !Get(row.Values, "Email")!.Contains('@')) errors.Add("Email is invalid.");
            ValidateRequired(row, normalized, errors, warnings);
            return Result(row, normalized, errors, warnings);
        }).ToList();
        return Task.FromResult(result);
    }

    public override Task<ImportConfirmResult> ConfirmImportAsync(Guid batchId, CancellationToken cancellationToken) => Task.FromResult(new ImportConfirmResult(0));
}

public sealed class CustomersImportHandler(AppDbContext dbContext)
    : StagingOnlyImportHandler(dbContext, ImportType.Customers, ["Name", "TaxNumber", "Phone", "Email", "Address", "IsActive"], ["Name"]);

public sealed class SuppliersImportHandler(AppDbContext dbContext)
    : StagingOnlyImportHandler(dbContext, ImportType.Suppliers, ["Name", "TaxNumber", "Phone", "Email", "Address", "IsActive"], ["Name"]);

public sealed class BankTransactionsImportHandler(AppDbContext dbContext)
    : StagingOnlyImportHandler(dbContext, ImportType.BankTransactions, ["TransactionDate", "Description", "Amount", "Reference", "Debit", "Credit", "Balance", "BankAccountCode"], ["TransactionDate", "Description", "Amount"]);

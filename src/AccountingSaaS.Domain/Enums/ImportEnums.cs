namespace AccountingSaaS.Domain.Enums;

public enum ImportType
{
    ChartOfAccounts = 1,
    OpeningBalances = 2,
    Customers = 3,
    Suppliers = 4,
    CostCenters = 5,
    JournalEntries = 6,
    BankTransactions = 7
}

public enum ImportBatchStatus
{
    Uploaded = 1,
    Validating = 2,
    Validated = 3,
    HasErrors = 4,
    ReadyToImport = 5,
    Imported = 6,
    Failed = 7,
    Cancelled = 8
}

public enum ImportRowStatus
{
    Valid = 1,
    Invalid = 2,
    Warning = 3,
    Imported = 4,
    Skipped = 5
}

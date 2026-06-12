namespace AccountingSaaS.Domain.Constants;

public static class Permissions
{
    public static readonly string[] All =
    [
        "Tenants.View", "Tenants.Create", "Tenants.Update", "Tenants.Activate", "Tenants.Deactivate",
        "Users.View", "Users.Create", "Users.Update", "Users.AssignRoles", "Users.AssignPermissions",
        "Accounting.View", "Accounting.CreateJournalEntry", "Accounting.UpdateJournalEntry",
        "Accounting.DeleteJournalEntry", "Accounting.PostJournalEntry", "Accounting.ClosePeriod", "Accounting.ReopenPeriod",
        "Reports.View", "Reports.Export", "Documents.Upload", "Documents.Delete", "Documents.DeleteClosedPeriod",
        "ClosingTasks.View", "ClosingTasks.Manage", "ClosingTasks.Approve", "AuditLogs.View",
        "FinancialYears.View", "FinancialYears.Create", "FinancialYears.Update", "FinancialYears.Close",
        "AccountingPeriods.View", "AccountingPeriods.Create", "AccountingPeriods.Update", "AccountingPeriods.Lock", "AccountingPeriods.Close", "AccountingPeriods.Reopen",
        "ChartOfAccounts.View", "ChartOfAccounts.Create", "ChartOfAccounts.Update", "ChartOfAccounts.Activate", "ChartOfAccounts.Deactivate",
        "CostCenters.View", "CostCenters.Create", "CostCenters.Update",
        "JournalEntries.View", "JournalEntries.Create", "JournalEntries.Update", "JournalEntries.Post", "JournalEntries.Reverse", "JournalEntries.Cancel", "JournalEntries.UpdatePostedJournalEntry",
        "Documents.View", "Documents.Download",
        "ClosingChecklist.View", "ClosingChecklist.Manage",
        "ClosingTasks.Submit", "ClosingTasks.Reject",
        "ClosingSubmissions.View", "ClosingSubmissions.Submit", "ClosingSubmissions.Review", "ClosingSubmissions.Approve", "ClosingSubmissions.Reject", "ClosingSubmissions.ClosePeriod", "ClosingSubmissions.ReopenPeriod",
        "AccountingReports.TrialBalance", "AccountingReports.GeneralLedger", "AccountingReports.AccountStatement", "AccountingReports.ClosingProgress",
        "Imports.View", "Imports.Upload", "Imports.Validate", "Imports.Confirm", "Imports.Cancel", "Imports.DownloadTemplate"
    ];
}

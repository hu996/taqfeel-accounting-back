namespace AccountingSaaS.Domain.Enums;

public enum FinancialYearStatus { Open = 1, Closed = 2 }
public enum AccountingPeriodStatus { Open = 1, Locked = 2, SubmittedForReview = 3, UnderReview = 4, Rejected = 5, Closed = 6 }
public enum JournalEntryStatus { Draft = 1, Posted = 2, Reversed = 3, Cancelled = 4 }
public enum WorkflowStatus
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Approved = 4,
    Rejected = 5,
    ReturnedForCorrection = 6,
    Closed = 7,
    Cancelled = 8
}
public enum AccountType { Asset = 1, Liability = 2, Equity = 3, Revenue = 4, Expense = 5 }
public enum NormalBalance { Debit = 1, Credit = 2 }
public enum ClosingTaskStatus { Pending = 1, InProgress = 2, Submitted = 3, Approved = 4, Rejected = 5, NotApplicable = 6 }
public enum ClosingSubmissionStatus { Draft = 1, Submitted = 2, UnderReview = 3, Approved = 4, Rejected = 5, Closed = 6, Reopened = 7, ReturnedForCorrection = 8, Cancelled = 9 }
public enum DocumentType { Invoice = 1, Receipt = 2, BankStatement = 3, Contract = 4, TaxDocument = 5, Payroll = 6, Other = 7 }
public enum WorkflowActionType { Submit = 1, Approve = 2, Reject = 3, Return = 4, Cancel = 5 }
public enum CustomFieldType { Text = 1, Number = 2, Date = 3, Boolean = 4, Select = 5 }
public enum ResetPeriod { Never = 1, Yearly = 2, Monthly = 3 }
public enum OpeningBalanceStatus { Draft = 1, Submitted = 2, UnderReview = 3, Approved = 4, Posted = 5, Rejected = 6 }
public enum ReconciliationStatus { Draft = 1, InProgress = 2, Completed = 3, Approved = 4 }
public enum BankMatchType { Manual = 1, Automatic = 2 }
public enum FixedAssetStatus { Active = 1, Suspended = 2, Disposed = 3, FullyDepreciated = 4 }
public enum DepreciationRunStatus { Draft = 1, Submitted = 2, Approved = 3, Posted = 4 }
public enum RecurringFrequency { Monthly = 1, Quarterly = 2, Yearly = 3 }
public enum ClosingCheckStatus { Passed = 1, Warning = 2, Failed = 3 }

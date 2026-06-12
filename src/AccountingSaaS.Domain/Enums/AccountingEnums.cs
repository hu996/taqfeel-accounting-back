namespace AccountingSaaS.Domain.Enums;

public enum FinancialYearStatus { Open = 1, Closed = 2 }
public enum AccountingPeriodStatus { Open = 1, Locked = 2, SubmittedForReview = 3, UnderReview = 4, Rejected = 5, Closed = 6 }
public enum JournalEntryStatus { Draft = 1, Posted = 2, Reversed = 3, Cancelled = 4 }
public enum AccountType { Asset = 1, Liability = 2, Equity = 3, Revenue = 4, Expense = 5 }
public enum NormalBalance { Debit = 1, Credit = 2 }
public enum ClosingTaskStatus { Pending = 1, InProgress = 2, Submitted = 3, Approved = 4, Rejected = 5, NotApplicable = 6 }
public enum ClosingSubmissionStatus { Draft = 1, Submitted = 2, UnderReview = 3, Approved = 4, Rejected = 5, Closed = 6, Reopened = 7 }
public enum DocumentType { Invoice = 1, Receipt = 2, BankStatement = 3, Contract = 4, TaxDocument = 5, Payroll = 6, Other = 7 }

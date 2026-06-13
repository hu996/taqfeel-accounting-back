using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Domain.Entities;

namespace AccountingSaaS.Infrastructure.Mapping;

internal static class AccountingMapper
{
    public static FinancialYearDto ToDto(FinancialYear x) => new(x.Id, x.YearName, x.StartDate, x.EndDate, x.Status);
    public static AccountingPeriodDto ToDto(AccountingPeriod x) => new(x.Id, x.FinancialYearId, x.PeriodName, x.StartDate, x.EndDate, x.Status);
    public static AccountDto ToDto(Account x) => new(x.Id, x.Code, x.NameAr, x.NameEn, x.AccountType, x.NormalBalance, x.ParentAccountId, x.IsPostingAccount, x.IsActive) { AccountNo = x.AccountNo };
    public static CostCenterDto ToDto(CostCenter x) => new(x.Id, x.Code, x.Name, x.IsActive) { CostCenterNo = x.CostCenterNo };
    public static DocumentDto ToDto(Document x) => new(x.Id, x.FinancialYearId, x.AccountingPeriodId, x.DocumentType, x.OriginalFileName, x.ContentType, x.SizeInBytes, x.RelatedEntityName, x.RelatedEntityId, x.UploadedAt, x.Notes) { DocumentNo = x.DocumentNo, WorkflowStatus = x.WorkflowStatus };
    public static ClosingTaskDto ToDto(ClosingTask x) => new(x.Id, x.FinancialYearId, x.AccountingPeriodId, x.TemplateItemId, x.Title, x.Description, x.SortOrder, x.IsRequired, x.AssignedToUserId, x.Status, x.DueDate, x.RejectionReason);
    public static ClosingSubmissionDto ToDto(ClosingSubmission x) => new(x.Id, x.FinancialYearId, x.AccountingPeriodId, x.Status, x.Notes, x.RejectionReason, x.ReopenReason)
    {
        AssignedReviewerUserId = x.AssignedReviewerUserId
    };
    public static ClosingChecklistTemplateItemDto ToDto(ClosingChecklistTemplateItem x) => new(x.Id, x.TemplateId, x.Title, x.Description, x.SortOrder, x.IsRequired);
    public static ClosingChecklistTemplateDto ToDto(ClosingChecklistTemplate x) => new(x.Id, x.Name, x.Description, x.IsDefault, x.IsActive, x.Items.OrderBy(i => i.SortOrder).Select(ToDto).ToList());
    public static JournalEntryDto ToDto(JournalEntry x) => new(x.Id, x.FinancialYearId, x.AccountingPeriodId, x.EntryNumber, x.EntryDate, x.Description, x.Status, x.TotalDebit, x.TotalCredit, x.Lines.Select(l => new JournalEntryLineDto(l.Id, l.AccountId, l.Account.Code, l.Account.NameEn, l.CostCenterId, l.Debit, l.Credit, l.Description)).ToList())
    {
        JournalEntryNo = x.JournalEntryNo,
        WorkflowStatus = x.WorkflowStatus,
        AssignedReviewerUserId = x.AssignedReviewerUserId
    };
}

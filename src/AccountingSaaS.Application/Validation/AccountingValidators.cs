using AccountingSaaS.Application.DTOs;
using FluentValidation;

namespace AccountingSaaS.Application.Validation;

public sealed class CreateFinancialYearRequestValidator : AbstractValidator<CreateFinancialYearRequest>
{
    public CreateFinancialYearRequestValidator()
    {
        RuleFor(x => x.YearName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate);
    }
}

public sealed class UpdateFinancialYearRequestValidator : AbstractValidator<UpdateFinancialYearRequest>
{
    public UpdateFinancialYearRequestValidator()
    {
        RuleFor(x => x.YearName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate);
    }
}

public sealed class CreateAccountingPeriodRequestValidator : AbstractValidator<CreateAccountingPeriodRequest>
{
    public CreateAccountingPeriodRequestValidator()
    {
        RuleFor(x => x.FinancialYearId).NotEmpty();
        RuleFor(x => x.PeriodName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate);
    }
}

public sealed class UpdateAccountingPeriodRequestValidator : AbstractValidator<UpdateAccountingPeriodRequest>
{
    public UpdateAccountingPeriodRequestValidator()
    {
        RuleFor(x => x.PeriodName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate);
    }
}

public sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameEn).NotEmpty().WithMessage("اسم الحساب مطلوب.").MaximumLength(200);
        RuleFor(x => x.NameAr).MaximumLength(200);
    }
}

public sealed class UpdateAccountRequestValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountRequestValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameEn).NotEmpty().WithMessage("اسم الحساب مطلوب.").MaximumLength(200);
        RuleFor(x => x.NameAr).MaximumLength(200);
    }
}

public sealed class CreateCostCenterRequestValidator : AbstractValidator<CreateCostCenterRequest>
{
    public CreateCostCenterRequestValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().WithMessage("اسم مركز التكلفة مطلوب.").MaximumLength(200);
    }
}

public sealed class UpdateCostCenterRequestValidator : AbstractValidator<UpdateCostCenterRequest>
{
    public UpdateCostCenterRequestValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().WithMessage("اسم مركز التكلفة مطلوب.").MaximumLength(200);
    }
}

public sealed class CreateJournalEntryRequestValidator : AbstractValidator<CreateJournalEntryRequest>
{
    public CreateJournalEntryRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Lines).NotNull().Must(x => x.Count >= 2).WithMessage("يجب أن يحتوي القيد على طرفين على الأقل.");
        RuleForEach(x => x.Lines).SetValidator(new JournalEntryLineRequestValidator());
        RuleFor(x => x.Lines.Sum(l => l.Debit)).Equal(x => x.Lines.Sum(l => l.Credit)).WithMessage("يجب أن يتساوى إجمالي المدين مع إجمالي الدائن.");
    }
}

public sealed class UpdateJournalEntryRequestValidator : AbstractValidator<UpdateJournalEntryRequest>
{
    public UpdateJournalEntryRequestValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Lines).NotNull().Must(x => x.Count >= 2).WithMessage("يجب أن يحتوي القيد على طرفين على الأقل.");
        RuleForEach(x => x.Lines).SetValidator(new JournalEntryLineRequestValidator());
        RuleFor(x => x.Lines.Sum(l => l.Debit)).Equal(x => x.Lines.Sum(l => l.Credit)).WithMessage("يجب أن يتساوى إجمالي المدين مع إجمالي الدائن.");
    }
}

public sealed class JournalEntryLineRequestValidator : AbstractValidator<JournalEntryLineRequest>
{
    public JournalEntryLineRequestValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Debit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Credit).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => x.Debit > 0 ^ x.Credit > 0).WithMessage("يجب أن يحتوي السطر على مبلغ مدين أو دائن فقط.");
    }
}

public sealed class ReverseJournalEntryRequestValidator : AbstractValidator<ReverseJournalEntryRequest>
{
    public ReverseJournalEntryRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ReopenPeriodRequestValidator : AbstractValidator<ReopenPeriodRequest>
{
    public ReopenPeriodRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class UploadDocumentRequestValidator : AbstractValidator<UploadDocumentRequest>
{
    public UploadDocumentRequestValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.RelatedEntityName).MaximumLength(120);
        RuleFor(x => x.RelatedEntityId).MaximumLength(80);
    }
}

public sealed class CreateClosingChecklistTemplateRequestValidator : AbstractValidator<CreateClosingChecklistTemplateRequest>
{
    public CreateClosingChecklistTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateClosingChecklistTemplateItemRequestValidator : AbstractValidator<CreateClosingChecklistTemplateItemRequest>
{
    public CreateClosingChecklistTemplateItemRequestValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateClosingTaskRequestValidator : AbstractValidator<CreateClosingTaskRequest>
{
    public CreateClosingTaskRequestValidator()
    {
        RuleFor(x => x.FinancialYearId).NotEmpty();
        RuleFor(x => x.AccountingPeriodId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
    }
}

public sealed class RejectClosingTaskRequestValidator : AbstractValidator<RejectClosingTaskRequest>
{
    public RejectClosingTaskRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class RejectClosingRequestValidator : AbstractValidator<RejectClosingRequest>
{
    public RejectClosingRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ReopenClosingRequestValidator : AbstractValidator<ReopenClosingRequest>
{
    public ReopenClosingRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

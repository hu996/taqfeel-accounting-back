using AccountingSaaS.Application.DTOs;
using FluentValidation;

namespace AccountingSaaS.Application.Validation;

public sealed class UploadImportRequestValidator : AbstractValidator<UploadImportRequest>
{
    public UploadImportRequestValidator()
    {
        RuleFor(x => x.ImportType).IsInEnum();
        RuleFor(x => x.WorksheetName).MaximumLength(80);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class ConfirmImportRequestValidator : AbstractValidator<ConfirmImportRequest>
{
    public ConfirmImportRequestValidator() => RuleFor(x => x.Notes).MaximumLength(1000);
}

public sealed class CancelImportRequestValidator : AbstractValidator<CancelImportRequest>
{
    public CancelImportRequestValidator() => RuleFor(x => x.Reason).MaximumLength(500);
}

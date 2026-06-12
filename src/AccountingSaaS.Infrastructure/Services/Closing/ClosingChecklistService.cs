using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Mapping;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class ClosingChecklistService(AppDbContext dbContext, ICurrentTenantService currentTenant, ICurrentUserService currentUser, IAuditLogService auditLog)
    : AccountingServiceBase(dbContext, currentTenant), IClosingChecklistService
{
    private static readonly string[] DefaultChecklistItems =
    [
        "Collect all invoices and receipts.",
        "Review bank statements.",
        "Match bank transactions.",
        "Review customer balances.",
        "Review supplier balances.",
        "Review expense accounts.",
        "Review revenue accounts.",
        "Post all draft journal entries.",
        "Generate trial balance.",
        "Review trial balance.",
        "Submit closing for approval.",
        "Archive closing documents."
    ];

    public async Task<BaseResponseDto<ClosingChecklistTemplateDto>> CreateTemplateAsync(CreateClosingChecklistTemplateRequest request, CancellationToken cancellationToken)
    {
        _ = TenantId;
        var template = new ClosingChecklistTemplate { Name = request.Name, Description = request.Description, IsDefault = request.IsDefault, IsActive = true };
        DbContext.ClosingChecklistTemplates.Add(template);
        await DbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync("Closing checklist template created", TenantId, currentUser.UserId, nameof(ClosingChecklistTemplate), template.Id.ToString(), newValues: template.Name, cancellationToken: cancellationToken);
        return BaseResponseDto<ClosingChecklistTemplateDto>.Ok(AccountingMapper.ToDto(template), "Template created.");
    }

    public async Task<BaseResponseDto<ClosingChecklistTemplateDto>> CreateDefaultTemplateAsync(CancellationToken cancellationToken)
    {
        _ = TenantId;
        if (await DbContext.ClosingChecklistTemplates.AnyAsync(x => x.IsDefault, cancellationToken))
            return BaseResponseDto<ClosingChecklistTemplateDto>.Fail("A default checklist template already exists.");

        var template = new ClosingChecklistTemplate { Name = "Default Closing Checklist", Description = "Default accounting period closing checklist.", IsDefault = true, IsActive = true };
        for (var i = 0; i < DefaultChecklistItems.Length; i++)
        {
            template.Items.Add(new ClosingChecklistTemplateItem { Title = DefaultChecklistItems[i], SortOrder = i + 1, IsRequired = true });
        }

        DbContext.ClosingChecklistTemplates.Add(template);
        await DbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync("Default closing checklist template created", TenantId, currentUser.UserId, nameof(ClosingChecklistTemplate), template.Id.ToString(), cancellationToken: cancellationToken);
        return BaseResponseDto<ClosingChecklistTemplateDto>.Ok(AccountingMapper.ToDto(template), "Default template created.");
    }

    public async Task<BaseResponseDto<ClosingChecklistTemplateDto>> UpdateTemplateAsync(Guid id, UpdateClosingChecklistTemplateRequest request, CancellationToken cancellationToken)
    {
        var template = await DbContext.ClosingChecklistTemplates.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (template is null) return BaseResponseDto<ClosingChecklistTemplateDto>.Fail("Template was not found.");
        template.Name = request.Name; template.Description = request.Description; template.IsDefault = request.IsDefault; template.IsActive = request.IsActive;
        await DbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync("Closing checklist template updated", TenantId, currentUser.UserId, nameof(ClosingChecklistTemplate), id.ToString(), cancellationToken: cancellationToken);
        return BaseResponseDto<ClosingChecklistTemplateDto>.Ok(AccountingMapper.ToDto(template), "Template updated.");
    }

    public async Task<BaseResponseDto<ClosingChecklistTemplateItemDto>> AddTemplateItemAsync(CreateClosingChecklistTemplateItemRequest request, CancellationToken cancellationToken)
    {
        if (!await DbContext.ClosingChecklistTemplates.AnyAsync(x => x.Id == request.TemplateId, cancellationToken)) return BaseResponseDto<ClosingChecklistTemplateItemDto>.Fail("Template was not found.");
        var item = new ClosingChecklistTemplateItem { TemplateId = request.TemplateId, Title = request.Title, Description = request.Description, SortOrder = request.SortOrder, IsRequired = request.IsRequired };
        DbContext.ClosingChecklistTemplateItems.Add(item);
        await DbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync("Closing checklist template item created", TenantId, currentUser.UserId, nameof(ClosingChecklistTemplateItem), item.Id.ToString(), newValues: item.Title, cancellationToken: cancellationToken);
        return BaseResponseDto<ClosingChecklistTemplateItemDto>.Ok(AccountingMapper.ToDto(item), "Template item created.");
    }

    public async Task<BaseResponseDto<ClosingChecklistTemplateItemDto>> UpdateTemplateItemAsync(Guid id, UpdateClosingChecklistTemplateItemRequest request, CancellationToken cancellationToken)
    {
        var item = await DbContext.ClosingChecklistTemplateItems.FindAsync([id], cancellationToken);
        if (item is null) return BaseResponseDto<ClosingChecklistTemplateItemDto>.Fail("Template item was not found.");
        var old = item.Title;
        item.Title = request.Title; item.Description = request.Description; item.SortOrder = request.SortOrder; item.IsRequired = request.IsRequired;
        await DbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync("Closing checklist template item updated", TenantId, currentUser.UserId, nameof(ClosingChecklistTemplateItem), id.ToString(), old, item.Title, cancellationToken: cancellationToken);
        return BaseResponseDto<ClosingChecklistTemplateItemDto>.Ok(AccountingMapper.ToDto(item), "Template item updated.");
    }

    public async Task<BaseResponseDto<IReadOnlyList<ClosingTaskDto>>> GenerateTasksForPeriodAsync(GenerateClosingTasksRequest request, CancellationToken cancellationToken)
    {
        _ = TenantId;
        var period = await DbContext.AccountingPeriods.FirstOrDefaultAsync(x => x.Id == request.AccountingPeriodId && x.FinancialYearId == request.FinancialYearId, cancellationToken);
        if (period is null) return BaseResponseDto<IReadOnlyList<ClosingTaskDto>>.Fail("Accounting period was not found.");
        if (period.Status == AccountingPeriodStatus.Closed) return BaseResponseDto<IReadOnlyList<ClosingTaskDto>>.Fail("Closing tasks cannot be generated for a closed period.");
        var items = await DbContext.ClosingChecklistTemplateItems.Where(x => x.TemplateId == request.TemplateId).OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            if (!await DbContext.ClosingTasks.AnyAsync(x => x.AccountingPeriodId == request.AccountingPeriodId && x.TemplateItemId == item.Id, cancellationToken))
                DbContext.ClosingTasks.Add(new ClosingTask { FinancialYearId = request.FinancialYearId, AccountingPeriodId = request.AccountingPeriodId, TemplateItemId = item.Id, Title = item.Title, Description = item.Description, SortOrder = item.SortOrder, IsRequired = item.IsRequired });
        }
        await DbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync("Closing tasks generated", TenantId, currentUser.UserId, nameof(ClosingTask), request.AccountingPeriodId.ToString(), cancellationToken: cancellationToken);
        var tasks = await DbContext.ClosingTasks.Where(x => x.AccountingPeriodId == request.AccountingPeriodId).OrderBy(x => x.SortOrder).Select(x => AccountingMapper.ToDto(x)).ToListAsync(cancellationToken);
        return BaseResponseDto<IReadOnlyList<ClosingTaskDto>>.Ok(tasks, "Closing tasks generated.");
    }
}

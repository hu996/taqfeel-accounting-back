using System.Globalization;
using System.Text.Json;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class NotificationService(
    AppDbContext db,
    ICurrentTenantService tenant,
    ICurrentUserService user) : AccountingServiceBase(db, tenant), INotificationService
{
    public async Task CreateAsync(CreateNotificationRequest request, CancellationToken ct)
    {
        DbContext.Notifications.Add(new Notification
        {
            UserId = request.UserId,
            TitleAr = request.TitleAr,
            TitleEn = request.TitleEn,
            MessageAr = request.MessageAr,
            MessageEn = request.MessageEn,
            EntityType = request.EntityType,
            EntityId = request.EntityId
        });
        await DbContext.SaveChangesAsync(ct);
    }

    public async Task CreateForRoleAsync(string role, string titleAr, string titleEn, string messageAr, string messageEn, string? entityType, Guid? entityId, CancellationToken ct)
    {
        var userIds = await DbContext.UserRoles
            .Where(x => DbContext.Roles.Any(r => r.Id == x.RoleId && r.Name == role))
            .Where(x => DbContext.Users.Any(u => u.Id == x.UserId && u.IsActive && !u.IsDeleted &&
                (u.TenantId == TenantId || DbContext.UserTenantAccesses.Any(a => a.UserId == u.Id && a.TenantId == TenantId))))
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync(ct);

        DbContext.Notifications.AddRange(userIds.Select(id => new Notification
        {
            UserId = id, TitleAr = titleAr, TitleEn = titleEn, MessageAr = messageAr,
            MessageEn = messageEn, EntityType = entityType, EntityId = entityId
        }));
        await DbContext.SaveChangesAsync(ct);
    }

    public async Task<BaseResponseDto<object>> MarkAsReadAsync(Guid id, CancellationToken ct)
    {
        var item = await DbContext.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.UserId, ct);
        if (item is null) return BaseResponseDto<object>.NotFound("Notification was not found.");
        item.IsRead = true;
        item.ReadAt = DateTimeOffset.UtcNow;
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<object>.Ok(null);
    }

    public async Task<BaseResponseDto<PaginatedResult<NotificationDto>>> GetMyNotificationsAsync(PaginationRequest request, CancellationToken ct)
    {
        var query = DbContext.Notifications.Where(x => x.UserId == user.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new NotificationDto(x.Id, x.TitleAr, x.TitleEn, x.MessageAr, x.MessageEn, x.EntityType, x.EntityId, x.IsRead, x.CreatedAt));
        return BaseResponseDto<PaginatedResult<NotificationDto>>.Ok(await ToPagedAsync(query, request, ct));
    }

    public async Task<BaseResponseDto<int>> GetUnreadCountAsync(CancellationToken ct) =>
        BaseResponseDto<int>.Ok(await DbContext.Notifications.CountAsync(x => x.UserId == user.UserId && !x.IsRead, ct));
}

public sealed class ActivityService(
    AppDbContext db,
    ICurrentTenantService tenant,
    ICurrentUserService user) : AccountingServiceBase(db, tenant), IActivityService
{
    public async Task CreateAsync(ActivityRequest request, CancellationToken ct)
    {
        if (user.UserId is not { } userId) return;
        DbContext.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId, Action = request.Action, EntityType = request.EntityType, EntityId = request.EntityId,
            TitleAr = request.TitleAr, TitleEn = request.TitleEn,
            DescriptionAr = request.DescriptionAr, DescriptionEn = request.DescriptionEn
        });
        await DbContext.SaveChangesAsync(ct);
    }

    public async Task<BaseResponseDto<IReadOnlyList<ActivityLogDto>>> GetLatestAsync(int take, CancellationToken ct)
    {
        var items = await DbContext.ActivityLogs.OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(take, 1, 100))
            .Select(x => new ActivityLogDto(x.Id, x.UserId, x.Action, x.EntityType, x.EntityId, x.TitleAr, x.TitleEn, x.DescriptionAr, x.DescriptionEn, x.CreatedAt))
            .ToListAsync(ct);
        return BaseResponseDto<IReadOnlyList<ActivityLogDto>>.Ok(items);
    }
}

public sealed class DynamicWorkflowService(
    AppDbContext db,
    ICurrentTenantService tenant,
    ICurrentUserService user) : AccountingServiceBase(db, tenant), IDynamicWorkflowService
{
    public async Task<BaseResponseDto<WorkflowDefinitionDto>> SaveDefinitionAsync(Guid? id, SaveWorkflowDefinitionRequest request, CancellationToken ct)
    {
        if (request.Steps.Count == 0 || request.Steps.Select(x => x.StepOrder).Distinct().Count() != request.Steps.Count)
            return BaseResponseDto<WorkflowDefinitionDto>.Fail("Workflow steps must contain unique orders.");

        var entity = id.HasValue
            ? await DbContext.WorkflowDefinitions.Include(x => x.Steps).FirstOrDefaultAsync(x => x.Id == id, ct)
            : null;
        if (id.HasValue && entity is null) return BaseResponseDto<WorkflowDefinitionDto>.NotFound("Workflow was not found.");
        entity ??= new WorkflowDefinition();
        if (!id.HasValue) DbContext.WorkflowDefinitions.Add(entity);
        entity.NameAr = request.NameAr;
        entity.NameEn = request.NameEn;
        entity.EntityType = request.EntityType.Trim();
        entity.IsActive = request.IsActive;
        DbContext.WorkflowSteps.RemoveRange(entity.Steps);
        entity.Steps = request.Steps.OrderBy(x => x.StepOrder).Select(x => new WorkflowStep
        {
            StepOrder = x.StepOrder, StepNameAr = x.StepNameAr, StepNameEn = x.StepNameEn,
            RequiredRoleId = x.RequiredRoleId, RequiredPermission = x.RequiredPermission,
            CanApprove = x.CanApprove, CanReject = x.CanReject, CanReturn = x.CanReturn,
            IsFinalApproval = x.IsFinalApproval
        }).ToList();
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<WorkflowDefinitionDto>.Ok(ToDto(entity));
    }

    public async Task<BaseResponseDto<IReadOnlyList<WorkflowDefinitionDto>>> GetDefinitionsAsync(CancellationToken ct)
    {
        var entities = await DbContext.WorkflowDefinitions.Include(x => x.Steps).OrderBy(x => x.EntityType).ToListAsync(ct);
        return BaseResponseDto<IReadOnlyList<WorkflowDefinitionDto>>.Ok(entities.Select(ToDto).ToList());
    }

    public Task<WorkflowStep?> GetFirstStepAsync(string entityType, CancellationToken ct) =>
        DbContext.WorkflowSteps.Include(x => x.WorkflowDefinition)
            .Where(x => x.WorkflowDefinition.EntityType == entityType && x.WorkflowDefinition.IsActive)
            .OrderBy(x => x.StepOrder).FirstOrDefaultAsync(ct);

    public Task<WorkflowStep?> GetNextStepAsync(Guid definitionId, int currentOrder, CancellationToken ct) =>
        DbContext.WorkflowSteps.Where(x => x.WorkflowDefinitionId == definitionId && x.StepOrder > currentOrder)
            .OrderBy(x => x.StepOrder).FirstOrDefaultAsync(ct);

    public async Task<bool> CanActAsync(WorkflowStep step, WorkflowActionType action, CancellationToken ct)
    {
        if (user.IsSuperAdmin) return true;
        if (step.RequiredPermission is { Length: > 0 } permission &&
            !user.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase)) return false;
        if (step.RequiredRoleId.HasValue)
        {
            var roleName = await DbContext.Roles.Where(x => x.Id == step.RequiredRoleId).Select(x => x.Name).FirstOrDefaultAsync(ct);
            if (roleName is null || !user.Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase)) return false;
        }
        return action switch
        {
            WorkflowActionType.Approve => step.CanApprove,
            WorkflowActionType.Reject => step.CanReject,
            WorkflowActionType.Return => step.CanReturn,
            _ => true
        };
    }

    public async Task RecordActionAsync(string entityType, Guid entityId, Guid definitionId, Guid stepId, string fromStatus, string toStatus, WorkflowActionType action, string? reason, string? notes, CancellationToken ct)
    {
        if (user.UserId is not { } userId) throw new InvalidOperationException("Authenticated user is required.");
        DbContext.WorkflowActions.Add(new WorkflowAction
        {
            EntityType = entityType, EntityId = entityId, WorkflowDefinitionId = definitionId,
            WorkflowStepId = stepId, FromStatus = fromStatus, ToStatus = toStatus, Action = action,
            ActionByUserId = userId, ActionDate = DateTimeOffset.UtcNow, Reason = reason, Notes = notes
        });
        await DbContext.SaveChangesAsync(ct);
    }

    private static WorkflowDefinitionDto ToDto(WorkflowDefinition x) => new(x.Id, x.NameAr, x.NameEn, x.EntityType, x.IsActive,
        x.Steps.OrderBy(s => s.StepOrder).Select(s => new WorkflowStepDto(s.Id, s.StepOrder, s.StepNameAr, s.StepNameEn, s.RequiredRoleId, s.RequiredPermission, s.CanApprove, s.CanReject, s.CanReturn, s.IsFinalApproval)).ToList());
}

public sealed class CommentService(
    AppDbContext db,
    ICurrentTenantService tenant,
    ICurrentUserService user) : AccountingServiceBase(db, tenant), ICommentService
{
    private static readonly HashSet<string> Allowed = ["JournalEntry", "ClosingSubmission", "ClosingTask", "PeriodCloseRequest"];

    public async Task<BaseResponseDto<CommentDto>> AddAsync(CreateCommentRequest request, CancellationToken ct)
    {
        if (!Allowed.Contains(request.EntityType) || string.IsNullOrWhiteSpace(request.CommentText))
            return BaseResponseDto<CommentDto>.Fail("Invalid comment target or text.");
        var item = new EntityComment { EntityType = request.EntityType, EntityId = request.EntityId, CommentText = request.CommentText.Trim(), IsInternal = request.IsInternal };
        DbContext.EntityComments.Add(item);
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<CommentDto>.Ok(ToDto(item));
    }

    public async Task<BaseResponseDto<IReadOnlyList<CommentDto>>> GetAsync(string entityType, Guid entityId, CancellationToken ct) =>
        BaseResponseDto<IReadOnlyList<CommentDto>>.Ok(await DbContext.EntityComments
            .Where(x => x.EntityType == entityType && x.EntityId == entityId).OrderBy(x => x.CreatedAt)
            .Select(x => new CommentDto(x.Id, x.EntityType, x.EntityId, x.CommentText, x.CreatedByUserId, x.CreatedAt, x.IsInternal)).ToListAsync(ct));

    public async Task<BaseResponseDto<object>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var item = await DbContext.EntityComments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return BaseResponseDto<object>.NotFound("Comment was not found.");
        if (item.CreatedByUserId != user.UserId && !user.IsSuperAdmin) return BaseResponseDto<object>.Fail("Only the author can delete this comment.");
        DbContext.EntityComments.Remove(item);
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<object>.Ok(null);
    }

    private static CommentDto ToDto(EntityComment x) => new(x.Id, x.EntityType, x.EntityId, x.CommentText, x.CreatedByUserId, x.CreatedAt, x.IsInternal);
}

public sealed class UniversalSearchService(
    AppDbContext db,
    ICurrentTenantService tenant) : AccountingServiceBase(db, tenant), IUniversalSearchService
{
    public async Task<BaseResponseDto<PaginatedResult<SearchResultDto>>> SearchAsync(UniversalSearchRequest request, CancellationToken ct)
    {
        var keyword = request.Keyword.Trim();
        if (keyword.Length < 2) return BaseResponseDto<PaginatedResult<SearchResultDto>>.Fail("Keyword must contain at least two characters.");
        var rows = new List<SearchResultDto>();
        rows.AddRange(await DbContext.JournalEntries.Where(x => x.EntryNumber.Contains(keyword) || x.Description.Contains(keyword))
            .Select(x => new SearchResultDto("JournalEntry", x.Id, x.EntryNumber, x.Description, x.EntryNumber, "journal-entry")).Take(100).ToListAsync(ct));
        rows.AddRange(await DbContext.Accounts.Where(x => x.Code.Contains(keyword) || x.NameAr.Contains(keyword) || x.NameEn.Contains(keyword))
            .Select(x => new SearchResultDto("Account", x.Id, x.NameAr, x.NameEn, x.Code, "account")).Take(100).ToListAsync(ct));
        rows.AddRange(await SearchParties(DbContext.Customers, "Customer", keyword, "customer", ct));
        rows.AddRange(await SearchParties(DbContext.Vendors, "Vendor", keyword, "vendor", ct));
        rows.AddRange(await SearchParties(DbContext.Employees, "Employee", keyword, "employee", ct));
        rows.AddRange(await DbContext.CostCenters.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword))
            .Select(x => new SearchResultDto("CostCenter", x.Id, x.Name, null, x.Code, "cost-center")).Take(100).ToListAsync(ct));
        var page = Math.Max(request.PageNumber, 1);
        var size = Math.Clamp(request.PageSize, 1, 100);
        return BaseResponseDto<PaginatedResult<SearchResultDto>>.Ok(new PaginatedResult<SearchResultDto>
        {
            Items = rows.OrderBy(x => x.EntityType).ThenBy(x => x.Title).Skip((page - 1) * size).Take(size).ToList(),
            TotalCount = rows.Count, PageNumber = page, PageSize = size
        });
    }

    private static Task<List<SearchResultDto>> SearchParties<T>(IQueryable<T> query, string type, string keyword, string url, CancellationToken ct) where T : BusinessParty =>
        query.Where(x => x.Code.Contains(keyword) || x.NameAr.Contains(keyword) || x.NameEn.Contains(keyword))
            .Select(x => new SearchResultDto(type, x.Id, x.NameAr, x.NameEn, x.Code, url)).Take(100).ToListAsync(ct);
}

public sealed class CustomFieldService(
    AppDbContext db,
    ICurrentTenantService tenant) : AccountingServiceBase(db, tenant), ICustomFieldService
{
    public async Task<BaseResponseDto<CustomFieldDefinitionDto>> SaveDefinitionAsync(Guid? id, SaveCustomFieldDefinitionRequest request, CancellationToken ct)
    {
        var entity = id.HasValue ? await DbContext.CustomFieldDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct) : null;
        if (id.HasValue && entity is null) return BaseResponseDto<CustomFieldDefinitionDto>.NotFound("Definition was not found.");
        entity ??= new CustomFieldDefinition();
        if (!id.HasValue) DbContext.CustomFieldDefinitions.Add(entity);
        entity.EntityType = request.EntityType.Trim();
        entity.FieldKey = request.FieldKey.Trim();
        entity.FieldNameAr = request.FieldNameAr.Trim();
        entity.FieldNameEn = request.FieldNameEn.Trim();
        entity.FieldType = request.FieldType;
        entity.IsRequired = request.IsRequired;
        entity.OptionsJson = request.OptionsJson;
        entity.IsActive = request.IsActive;
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<CustomFieldDefinitionDto>.Ok(ToDto(entity));
    }

    public async Task<BaseResponseDto<IReadOnlyList<CustomFieldDefinitionDto>>> GetDefinitionsAsync(string entityType, CancellationToken ct) =>
        BaseResponseDto<IReadOnlyList<CustomFieldDefinitionDto>>.Ok(await DbContext.CustomFieldDefinitions
            .Where(x => x.EntityType == entityType && x.IsActive).OrderBy(x => x.FieldKey)
            .Select(x => new CustomFieldDefinitionDto(x.Id, x.EntityType, x.FieldKey, x.FieldNameAr, x.FieldNameEn, x.FieldType, x.IsRequired, x.OptionsJson, x.IsActive)).ToListAsync(ct));

    public async Task<BaseResponseDto<object>> SaveValuesAsync(SaveCustomFieldValuesRequest request, CancellationToken ct)
    {
        var definitions = await DbContext.CustomFieldDefinitions.Where(x => x.EntityType == request.EntityType && x.IsActive).ToListAsync(ct);
        var supplied = request.Values.ToDictionary(x => x.CustomFieldDefinitionId);
        foreach (var definition in definitions)
        {
            supplied.TryGetValue(definition.Id, out var value);
            if (definition.IsRequired && string.IsNullOrWhiteSpace(value?.Value))
                return BaseResponseDto<object>.Fail($"Custom field '{definition.FieldKey}' is required.");
            if (value is not null && !ValidateValue(definition, value.Value))
                return BaseResponseDto<object>.Fail($"Custom field '{definition.FieldKey}' has an invalid value.");
        }
        var old = await DbContext.CustomFieldValues.Where(x => x.EntityType == request.EntityType && x.EntityId == request.EntityId).ToListAsync(ct);
        foreach (var value in request.Values)
        {
            var item = old.FirstOrDefault(x => x.CustomFieldDefinitionId == value.CustomFieldDefinitionId);
            if (item is null)
            {
                DbContext.CustomFieldValues.Add(new CustomFieldValue
                {
                    EntityType = request.EntityType, EntityId = request.EntityId,
                    CustomFieldDefinitionId = value.CustomFieldDefinitionId, Value = value.Value
                });
            }
            else
            {
                item.Value = value.Value;
            }
        }
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<object>.Ok(null);
    }

    private static bool ValidateValue(CustomFieldDefinition d, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return !d.IsRequired;
        return d.FieldType switch
        {
            CustomFieldType.Number => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            CustomFieldType.Date => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out _),
            CustomFieldType.Boolean => bool.TryParse(value, out _),
            CustomFieldType.Select => d.OptionsJson is not null && JsonSerializer.Deserialize<string[]>(d.OptionsJson)?.Contains(value) == true,
            _ => true
        };
    }

    private static CustomFieldDefinitionDto ToDto(CustomFieldDefinition x) => new(x.Id, x.EntityType, x.FieldKey, x.FieldNameAr, x.FieldNameEn, x.FieldType, x.IsRequired, x.OptionsJson, x.IsActive);
}

public sealed class DocumentNumberService(
    AppDbContext db,
    ICurrentTenantService tenant,
    INumberSequenceService sequences) : AccountingServiceBase(db, tenant), IDocumentNumberService
{
    public async Task<BaseResponseDto<DocumentNumberTemplateDto>> SaveTemplateAsync(Guid? id, SaveDocumentNumberTemplateRequest request, CancellationToken ct)
    {
        if (!request.Template.Contains("{SEQ}", StringComparison.OrdinalIgnoreCase))
            return BaseResponseDto<DocumentNumberTemplateDto>.Fail("Template must contain {SEQ}.");
        var item = id.HasValue ? await DbContext.DocumentNumberTemplates.FirstOrDefaultAsync(x => x.Id == id, ct) : null;
        if (id.HasValue && item is null) return BaseResponseDto<DocumentNumberTemplateDto>.NotFound("Template was not found.");
        item ??= new DocumentNumberTemplate();
        if (!id.HasValue) DbContext.DocumentNumberTemplates.Add(item);
        item.EntityType = request.EntityType.Trim();
        item.Template = request.Template.Trim();
        item.ResetPeriod = request.ResetPeriod;
        item.IsActive = request.IsActive;
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<DocumentNumberTemplateDto>.Ok(new(item.Id, item.EntityType, item.Template, item.ResetPeriod, item.IsActive));
    }

    public async Task<string> GenerateAsync(string entityType, DateOnly date, string? branch, CancellationToken ct)
    {
        var template = await DbContext.DocumentNumberTemplates.Where(x => x.EntityType == entityType && x.IsActive)
            .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        var pattern = template?.Template ?? $"{entityType.ToUpperInvariant()}-{{YEAR}}-{{SEQ}}";
        var reset = template?.ResetPeriod ?? ResetPeriod.Yearly;
        var scope = reset switch
        {
            ResetPeriod.Monthly => $"{date:yyyyMM}",
            ResetPeriod.Yearly => $"{date:yyyy}",
            _ => "ALL"
        };
        var seq = await sequences.NextAsync($"Document:{entityType}:{scope}", TenantId, ct);
        var tenantCode = TenantId.ToString("N")[..8].ToUpperInvariant();
        return pattern.Replace("{YEAR}", date.Year.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{MONTH}", date.Month.ToString("00", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{BRANCH}", string.IsNullOrWhiteSpace(branch) ? "MAIN" : branch.Trim(), StringComparison.OrdinalIgnoreCase)
            .Replace("{TENANT}", tenantCode, StringComparison.OrdinalIgnoreCase)
            .Replace("{SEQ}", seq.ToString("000000", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class DashboardService(
    AppDbContext db,
    ICurrentTenantService tenant,
    ICurrentUserService user) : AccountingServiceBase(db, tenant), IDashboardService
{
    public async Task<BaseResponseDto<DashboardDto>> GetMyDashboardAsync(CancellationToken ct)
    {
        var todayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var tomorrowStart = todayStart.AddDays(1);
        var query = DbContext.JournalEntries.AsQueryable();
        if (user.Roles.Contains("Accountant", StringComparer.OrdinalIgnoreCase) && user.UserId is { } userId)
            query = query.Where(x => x.CreatedByUserId == userId);
        var notifications = await DbContext.Notifications.Where(x => x.UserId == user.UserId).OrderByDescending(x => x.CreatedAt).Take(5)
            .Select(x => new NotificationDto(x.Id, x.TitleAr, x.TitleEn, x.MessageAr, x.MessageEn, x.EntityType, x.EntityId, x.IsRead, x.CreatedAt)).ToListAsync(ct);
        var activities = await DbContext.ActivityLogs.OrderByDescending(x => x.CreatedAt).Take(5)
            .Select(x => new ActivityLogDto(x.Id, x.UserId, x.Action, x.EntityType, x.EntityId, x.TitleAr, x.TitleEn, x.DescriptionAr, x.DescriptionEn, x.CreatedAt)).ToListAsync(ct);
        return BaseResponseDto<DashboardDto>.Ok(new DashboardDto(
            await query.CountAsync(x => x.WorkflowStatus == WorkflowStatus.Submitted || x.WorkflowStatus == WorkflowStatus.UnderReview, ct),
            await query.CountAsync(x => x.WorkflowStatus == WorkflowStatus.Rejected, ct),
            await query.CountAsync(x => x.WorkflowStatus == WorkflowStatus.Approved && x.UpdatedAt >= todayStart && x.UpdatedAt < tomorrowStart, ct),
            await query.CountAsync(x => x.WorkflowStatus == WorkflowStatus.Approved && x.Status != JournalEntryStatus.Posted, ct),
            await DbContext.AccountingPeriods.CountAsync(x => x.Status == AccountingPeriodStatus.Open, ct),
            await DbContext.ClosingTasks.CountAsync(x => x.AssignedToUserId == user.UserId && x.Status != ClosingTaskStatus.Approved, ct),
            await DbContext.Notifications.CountAsync(x => x.UserId == user.UserId && !x.IsRead, ct),
            notifications, activities));
    }
}

public sealed class ReportBuilderService(
    AppDbContext db,
    ICurrentTenantService tenant) : AccountingServiceBase(db, tenant), IReportBuilderService
{
    private static readonly Dictionary<string, HashSet<string>> AllowedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JournalEntry"] = ["EntryNumber", "EntryDate", "Description", "Status", "TotalDebit", "TotalCredit"],
        ["Account"] = ["Code", "NameAr", "NameEn", "AccountType", "IsActive"],
        ["CostCenter"] = ["Code", "Name", "IsActive"]
    };

    public async Task<BaseResponseDto<ReportDefinitionDto>> SaveAsync(Guid? id, SaveReportDefinitionRequest request, CancellationToken ct)
    {
        if (!AllowedColumns.TryGetValue(request.EntityType, out var allowed))
            return BaseResponseDto<ReportDefinitionDto>.Fail("Unsupported report entity.");
        string[] columns;
        try { columns = JsonSerializer.Deserialize<string[]>(request.ColumnsJson) ?? []; }
        catch (JsonException) { return BaseResponseDto<ReportDefinitionDto>.Fail("ColumnsJson is invalid."); }
        if (columns.Length == 0 || columns.Any(x => !allowed.Contains(x)))
            return BaseResponseDto<ReportDefinitionDto>.Fail("One or more columns are not allowed.");
        var item = id.HasValue ? await DbContext.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct) : null;
        if (id.HasValue && item is null) return BaseResponseDto<ReportDefinitionDto>.NotFound("Report was not found.");
        item ??= new ReportDefinition();
        if (!id.HasValue) DbContext.ReportDefinitions.Add(item);
        item.ReportName = request.ReportName.Trim();
        item.EntityType = request.EntityType;
        item.ColumnsJson = JsonSerializer.Serialize(columns);
        item.FiltersJson = request.FiltersJson;
        item.IsActive = request.IsActive;
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<ReportDefinitionDto>.Ok(ToDto(item));
    }

    public async Task<BaseResponseDto<IReadOnlyList<ReportDefinitionDto>>> GetAsync(CancellationToken ct) =>
        BaseResponseDto<IReadOnlyList<ReportDefinitionDto>>.Ok(await DbContext.ReportDefinitions.OrderBy(x => x.ReportName)
            .Select(x => new ReportDefinitionDto(x.Id, x.ReportName, x.EntityType, x.ColumnsJson, x.FiltersJson, x.IsActive)).ToListAsync(ct));

    public async Task<BaseResponseDto<object>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var item = await DbContext.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return BaseResponseDto<object>.NotFound("Report was not found.");
        DbContext.ReportDefinitions.Remove(item);
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<object>.Ok(null);
    }

    public async Task<BaseResponseDto<ReportRunResult>> RunAsync(Guid id, RunReportRequest request, CancellationToken ct)
    {
        var report = await DbContext.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (report is null) return BaseResponseDto<ReportRunResult>.NotFound("Report was not found.");
        var columns = JsonSerializer.Deserialize<string[]>(report.ColumnsJson) ?? [];
        var rows = new List<Dictionary<string, object?>>();
        if (report.EntityType == "JournalEntry")
        {
            var source = await DbContext.JournalEntries.OrderByDescending(x => x.EntryDate).Take(1000)
                .Select(x => new { x.EntryNumber, x.EntryDate, x.Description, x.Status, x.TotalDebit, x.TotalCredit }).ToListAsync(ct);
            rows.AddRange(source.Select(x => new Dictionary<string, object?>
            {
                ["EntryNumber"] = x.EntryNumber, ["EntryDate"] = x.EntryDate, ["Description"] = x.Description,
                ["Status"] = x.Status.ToString(), ["TotalDebit"] = x.TotalDebit, ["TotalCredit"] = x.TotalCredit
            }));
        }
        else if (report.EntityType == "Account")
        {
            var source = await DbContext.Accounts.OrderBy(x => x.Code).Take(1000)
                .Select(x => new { x.Code, x.NameAr, x.NameEn, x.AccountType, x.IsActive }).ToListAsync(ct);
            rows.AddRange(source.Select(x => new Dictionary<string, object?>
            {
                ["Code"] = x.Code, ["NameAr"] = x.NameAr, ["NameEn"] = x.NameEn,
                ["AccountType"] = x.AccountType.ToString(), ["IsActive"] = x.IsActive
            }));
        }
        else if (report.EntityType == "CostCenter")
        {
            var source = await DbContext.CostCenters.OrderBy(x => x.Code).Take(1000)
                .Select(x => new { x.Code, x.Name, x.IsActive }).ToListAsync(ct);
            rows.AddRange(source.Select(x => new Dictionary<string, object?>
            {
                ["Code"] = x.Code, ["Name"] = x.Name, ["IsActive"] = x.IsActive
            }));
        }
        var filtered = rows.Select(row => (IReadOnlyDictionary<string, object?>)columns.ToDictionary(c => c, c => row.GetValueOrDefault(c))).ToList();
        var page = Math.Max(request.PageNumber, 1);
        var size = Math.Clamp(request.PageSize, 1, 200);
        return BaseResponseDto<ReportRunResult>.Ok(new(columns, filtered.Skip((page - 1) * size).Take(size).ToList(), filtered.Count));
    }

    private static ReportDefinitionDto ToDto(ReportDefinition x) => new(x.Id, x.ReportName, x.EntityType, x.ColumnsJson, x.FiltersJson, x.IsActive);
}

public sealed class BusinessPartyService(
    AppDbContext db,
    ICurrentTenantService tenant) : AccountingServiceBase(db, tenant), IBusinessPartyService
{
    public Task<BaseResponseDto<BusinessPartyDto>> SaveAsync(string type, Guid? id, SaveBusinessPartyRequest request, CancellationToken ct) =>
        type.ToLowerInvariant() switch
        {
            "customer" => SaveParty(DbContext.Customers, id, request, ct),
            "vendor" => SaveParty(DbContext.Vendors, id, request, ct),
            "employee" => SaveParty(DbContext.Employees, id, request, ct),
            _ => Task.FromResult(BaseResponseDto<BusinessPartyDto>.Fail("Unsupported party type."))
        };

    public Task<BaseResponseDto<object>> DeleteAsync(string type, Guid id, CancellationToken ct) =>
        type.ToLowerInvariant() switch
        {
            "customer" => DeleteParty(DbContext.Customers, id, ct),
            "vendor" => DeleteParty(DbContext.Vendors, id, ct),
            "employee" => DeleteParty(DbContext.Employees, id, ct),
            _ => Task.FromResult(BaseResponseDto<object>.Fail("Unsupported party type."))
        };

    private async Task<BaseResponseDto<BusinessPartyDto>> SaveParty<T>(DbSet<T> set, Guid? id, SaveBusinessPartyRequest request, CancellationToken ct) where T : BusinessParty, new()
    {
        var item = id.HasValue ? await set.FirstOrDefaultAsync(x => x.Id == id, ct) : null;
        if (id.HasValue && item is null) return BaseResponseDto<BusinessPartyDto>.NotFound("Record was not found.");
        item ??= new T();
        if (!id.HasValue) set.Add(item);
        item.Code = request.Code.Trim();
        item.NameAr = request.NameAr.Trim();
        item.NameEn = request.NameEn.Trim();
        item.IsActive = request.IsActive;
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<BusinessPartyDto>.Ok(new(item.Id, item.Code, item.NameAr, item.NameEn, item.IsActive));
    }

    private async Task<BaseResponseDto<object>> DeleteParty<T>(DbSet<T> set, Guid id, CancellationToken ct) where T : BusinessParty
    {
        var item = await set.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return BaseResponseDto<object>.NotFound("Record was not found.");
        item.IsActive = false;
        DbContext.Remove(item);
        await DbContext.SaveChangesAsync(ct);
        return BaseResponseDto<object>.Ok(null);
    }
}

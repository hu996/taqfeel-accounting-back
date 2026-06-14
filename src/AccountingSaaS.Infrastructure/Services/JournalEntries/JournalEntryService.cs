using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Mapping;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class JournalEntryService(
    AppDbContext dbContext,
    ICurrentTenantService currentTenant,
    ICurrentUserService currentUser,
    IAuditLogService auditLog,
    INumberSequenceService numberSequence,
    IWorkflowAccessService workflowAccess,
    IDynamicWorkflowService dynamicWorkflow,
    INotificationService notifications,
    IActivityService activities,
    ICommentService comments,
    ICurrentSessionService currentSession)
    : AccountingServiceBase(dbContext, currentTenant), IJournalEntryService
{
    public async Task<BaseResponseDto<JournalEntryDto>> CreateDraftAsync(
        CreateJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        _ = TenantId;

        if (!currentSession.ActiveAccountingPeriodId.HasValue)
        {
            return BaseResponseDto<JournalEntryDto>.Fail(
                "No active accounting period is available for this session.");
        }

        var financialYearId = currentSession.ActiveFinancialYearId;
        var accountingPeriodId = currentSession.ActiveAccountingPeriodId.Value;
        var validation = await ValidateJournalAsync(
            financialYearId,
            accountingPeriodId,
            request.EntryDate,
            request.Lines,
            cancellationToken);

        if (!validation.Success)
        {
            return BaseResponseDto<JournalEntryDto>.Fail(validation.Message, validation.Errors);
        }

        var journalEntryNo = await numberSequence.NextAsync(
            "JournalEntryNo",
            TenantId,
            cancellationToken);
        var financialYear = await DbContext.FinancialYears
            .Where(x => x.Id == financialYearId)
            .Select(x => x.StartDate.Year)
            .FirstAsync(cancellationToken);

        var entry = new JournalEntry
        {
            JournalEntryNo = journalEntryNo,
            FinancialYearId = financialYearId,
            AccountingPeriodId = accountingPeriodId,
            EntryDate = request.EntryDate,
            Description = request.Description,
            EntryNumber = $"JE-{financialYear}-{journalEntryNo:000000}",
            TotalDebit = request.Lines.Sum(x => x.Debit),
            TotalCredit = request.Lines.Sum(x => x.Credit)
        };

        foreach (var line in request.Lines)
        {
            entry.Lines.Add(new JournalEntryLine
            {
                AccountId = line.AccountId,
                CostCenterId = line.CostCenterId,
                Debit = line.Debit,
                Credit = line.Credit,
                Description = line.Description
            });
        }

        DbContext.JournalEntries.Add(entry);

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Created",
            TenantId,
            currentUser.UserId,
            nameof(JournalEntry),
            entry.Id.ToString(),
            newValues: entry.EntryNumber,
            cancellationToken: cancellationToken);
        await activities.CreateAsync(new(
            "Created", nameof(JournalEntry), entry.Id,
            "إنشاء قيد يومية", "Journal entry created",
            entry.EntryNumber, entry.EntryNumber), cancellationToken);

        var dto = await LoadDtoAsync(entry.Id, cancellationToken);

        return BaseResponseDto<JournalEntryDto>.Ok(dto!, "تم إنشاء القيد كمسودة.");
    }

    public async Task<BaseResponseDto<JournalEntryDto>> UpdateDraftAsync(
        Guid id,
        UpdateJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await DbContext.JournalEntries
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entry is null)
        {
            return BaseResponseDto<JournalEntryDto>.NotFound("القيد غير موجود.");
        }

        var hasOverride = currentUser.Permissions.Contains(
            "JournalEntries.UpdatePostedJournalEntry",
            StringComparer.OrdinalIgnoreCase);

        if (!workflowAccess.CanEdit(entry.WorkflowStatus, hasOverride) ||
            entry.Status == JournalEntryStatus.Posted && !hasOverride)
        {
            return BaseResponseDto<JournalEntryDto>.Fail(
                "لا يمكن تعديل القيد في حالته الحالية. يسمح بالتعديل للمسودة أو المرفوض أو المعاد للتصحيح فقط.");
        }

        var validation = await ValidateJournalAsync(
            entry.FinancialYearId,
            entry.AccountingPeriodId,
            request.EntryDate,
            request.Lines,
            cancellationToken);

        if (!validation.Success)
        {
            return BaseResponseDto<JournalEntryDto>.Fail(validation.Message, validation.Errors);
        }

        if (entry.WorkflowStatus is WorkflowStatus.Rejected or WorkflowStatus.ReturnedForCorrection)
        {
            var versionNo = await DbContext.JournalEntryVersions
                .Where(x => x.JournalEntryId == id)
                .Select(x => (int?)x.VersionNo)
                .MaxAsync(cancellationToken) ?? 0;
            DbContext.JournalEntryVersions.Add(new JournalEntryVersion
            {
                JournalEntryId = id,
                VersionNo = versionNo + 1,
                SnapshotJson = JsonSerializer.Serialize(new
                {
                    Header = new
                    {
                        entry.EntryNumber, entry.EntryDate, entry.Description,
                        entry.TotalDebit, entry.TotalCredit, entry.Status, entry.WorkflowStatus
                    },
                    Lines = entry.Lines.Select(x => new
                    {
                        x.AccountId, x.CostCenterId, x.Debit, x.Credit, x.Description
                    })
                }),
                ChangeReason = entry.ReviewReason
            });
        }

        entry.EntryDate = request.EntryDate;
        entry.Description = request.Description;
        entry.TotalDebit = request.Lines.Sum(x => x.Debit);
        entry.TotalCredit = request.Lines.Sum(x => x.Credit);

        DbContext.JournalEntryLines.RemoveRange(entry.Lines);

        entry.Lines = request.Lines
            .Select(line => new JournalEntryLine
            {
                AccountId = line.AccountId,
                CostCenterId = line.CostCenterId,
                Debit = line.Debit,
                Credit = line.Credit,
                Description = line.Description
            })
            .ToList();

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Updated",
            TenantId,
            currentUser.UserId,
            nameof(JournalEntry),
            id.ToString(),
            cancellationToken: cancellationToken);
        await activities.CreateAsync(new(
            "Posted", nameof(JournalEntry), entry.Id,
            "ترحيل قيد يومية", "Journal entry posted",
            entry.EntryNumber, entry.EntryNumber), cancellationToken);
        if (entry.CreatedByUserId.HasValue)
        {
            await notifications.CreateAsync(new(
                entry.CreatedByUserId.Value,
                "تم ترحيل القيد", "Journal entry posted",
                entry.EntryNumber, entry.EntryNumber,
                nameof(JournalEntry), entry.Id), cancellationToken);
        }

        var dto = await LoadDtoAsync(id, cancellationToken);

        return BaseResponseDto<JournalEntryDto>.Ok(dto!, "تم تحديث القيد بنجاح.");
    }

    public async Task<BaseResponseDto<JournalEntryDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var dto = await LoadDtoAsync(id, cancellationToken);

        if (dto is null)
        {
            return BaseResponseDto<JournalEntryDto>.NotFound("القيد غير موجود.");
        }

        return BaseResponseDto<JournalEntryDto>.Ok(dto);
    }

    public async Task<BaseResponseDto<PaginatedResult<JournalEntryDto>>> GetPagedAsync(
      AccountingPagedRequest request,
      CancellationToken cancellationToken)
    {
        var query = DbContext.JournalEntries
            .Include(x => x.Lines)
            .ThenInclude(x => x.Account)
            .AsQueryable();

        if (request.FinancialYearId.HasValue)
        {
            query = query.Where(x => x.FinancialYearId == request.FinancialYearId.Value);
        }

        if (request.AccountingPeriodId.HasValue)
        {
            query = query.Where(x => x.AccountingPeriodId == request.AccountingPeriodId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        if (request.WorkflowStatus.HasValue)
        {
            query = query.Where(x => x.WorkflowStatus == request.WorkflowStatus.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(x =>
                x.EntryNumber.Contains(search) ||
                x.Description.Contains(search));
        }

        var result = await ToPagedAsync(
            query
                .OrderByDescending(x => x.EntryDate)
                .Select(x => AccountingMapper.ToDto(x)),
            request,
            cancellationToken);

        return BaseResponseDto<PaginatedResult<JournalEntryDto>>.Ok(result);
    }

    public async Task<BaseResponseDto<JournalEntryDto>> PostAsync(
        Guid id,
        PostJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await DbContext.JournalEntries
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entry is null)
        {
            return BaseResponseDto<JournalEntryDto>.NotFound("القيد غير موجود.");
        }

        if (entry.WorkflowStatus != WorkflowStatus.Approved)
        {
            return BaseResponseDto<JournalEntryDto>.Fail(
                "يجب اعتماد القيد من المراجع المالي قبل ترحيله.");
        }

        var periodAllowsChanges = await PeriodAllowsAccountingChangesAsync(
            entry.AccountingPeriodId,
            cancellationToken);

        if (!periodAllowsChanges)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("لا يمكن ترحيل قيد داخل فترة مقفلة أو مغلقة.");
        }

        if (entry.Lines.Count < 2 || entry.TotalDebit != entry.TotalCredit)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("القيد غير متوازن.");
        }

        entry.Status = JournalEntryStatus.Posted;
        entry.PostedAt = DateTimeOffset.UtcNow;
        entry.PostedByUserId = currentUser.UserId;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Approved",
            TenantId,
            currentUser.UserId,
            nameof(JournalEntry),
            id.ToString(),
            newValues: request.Notes,
            cancellationToken: cancellationToken);

        var dto = await LoadDtoAsync(id, cancellationToken);

        return BaseResponseDto<JournalEntryDto>.Ok(dto!, "تم ترحيل القيد بنجاح.");
    }

    public async Task<BaseResponseDto<JournalEntryDto>> ReverseAsync(
        Guid id,
        ReverseJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await DbContext.JournalEntries.FindAsync([id], cancellationToken);

        if (entry is null)
        {
            return BaseResponseDto<JournalEntryDto>.NotFound("القيد غير موجود.");
        }

        var periodAllowsChanges = await PeriodAllowsAccountingChangesAsync(
            entry.AccountingPeriodId,
            cancellationToken);

        if (!periodAllowsChanges)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("لا يمكن عكس قيد داخل فترة مقفلة أو مغلقة.");
        }

        entry.Status = JournalEntryStatus.Reversed;
        entry.ReversedAt = DateTimeOffset.UtcNow;
        entry.ReversedByUserId = currentUser.UserId;
        entry.ReversalReason = request.Reason;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Updated",
            TenantId,
            currentUser.UserId,
            nameof(JournalEntry),
            id.ToString(),
            newValues: request.Reason,
            cancellationToken: cancellationToken);

        var dto = await LoadDtoAsync(id, cancellationToken);

        return BaseResponseDto<JournalEntryDto>.Ok(dto!, "تم عكس القيد بنجاح.");
    }

    public async Task<BaseResponseDto<JournalEntryDto>> CancelAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entry = await DbContext.JournalEntries.FindAsync([id], cancellationToken);

        if (entry is null)
        {
            return BaseResponseDto<JournalEntryDto>.NotFound("القيد غير موجود.");
        }

        if (entry.Status != JournalEntryStatus.Draft)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("يمكن إلغاء القيود المسودة فقط.");
        }

        var periodAllowsChanges = await PeriodAllowsAccountingChangesAsync(
            entry.AccountingPeriodId,
            cancellationToken);

        if (!periodAllowsChanges)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("لا يمكن إلغاء قيد داخل فترة مقفلة أو مغلقة.");
        }

        entry.Status = JournalEntryStatus.Cancelled;
        entry.WorkflowStatus = WorkflowStatus.Cancelled;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Updated",
            TenantId,
            currentUser.UserId,
            nameof(JournalEntry),
            id.ToString(),
            cancellationToken: cancellationToken);

        var dto = await LoadDtoAsync(id, cancellationToken);

        return BaseResponseDto<JournalEntryDto>.Ok(dto!, "تم إلغاء القيد.");
    }

    public async Task<BaseResponseDto<JournalEntryDto>> SubmitForReviewAsync(
        Guid id,
        SubmitJournalEntryRequest request,
        CancellationToken cancellationToken)
    {
        var entry = await DbContext.JournalEntries
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entry is null)
        {
            return BaseResponseDto<JournalEntryDto>.NotFound("القيد غير موجود.");
        }

        if (!workflowAccess.CanEdit(entry.WorkflowStatus))
        {
            return BaseResponseDto<JournalEntryDto>.Fail("لا يمكن إرسال القيد للمراجعة من حالته الحالية.");
        }

        if (entry.Lines.Count < 2 || entry.TotalDebit <= 0 || entry.TotalDebit != entry.TotalCredit)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("لا يمكن إرسال قيد غير متوازن أو غير مكتمل للمراجعة.");
        }

        if (request.ReviewerUserId.HasValue)
        {
            var assigned = await DbContext.ReviewerTenantAssignments
                .AnyAsync(
                    x => x.ReviewerUserId == request.ReviewerUserId.Value &&
                         x.TenantId == TenantId &&
                         x.IsActive,
                    cancellationToken);

            if (!assigned)
            {
                return BaseResponseDto<JournalEntryDto>.Fail("المراجع المحدد غير مسند لهذه الشركة.");
            }
        }

        var firstStep = await dynamicWorkflow.GetFirstStepAsync(nameof(JournalEntry), cancellationToken);
        if (firstStep is null)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("No active workflow is configured for journal entries.");
        }

        entry.WorkflowStatus = WorkflowStatus.Submitted;
        entry.WorkflowDefinitionId = firstStep.WorkflowDefinitionId;
        entry.WorkflowStepId = firstStep.Id;
        entry.AssignedReviewerUserId = request.ReviewerUserId;
        entry.ReviewReason = null;
        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "Submitted",
            TenantId,
            currentUser.UserId,
            nameof(JournalEntry),
            entry.Id.ToString(),
            cancellationToken: cancellationToken);
        await dynamicWorkflow.RecordActionAsync(
            nameof(JournalEntry), entry.Id, firstStep.WorkflowDefinitionId, firstStep.Id,
            WorkflowStatus.Draft.ToString(), WorkflowStatus.Submitted.ToString(),
            WorkflowActionType.Submit, null, null, cancellationToken);
        await notifications.CreateForRoleAsync(
            "Reviewer", "قيد جديد للمراجعة", "Journal entry awaiting review",
            entry.EntryNumber, entry.EntryNumber, nameof(JournalEntry), entry.Id, cancellationToken);
        await activities.CreateAsync(new(
            "Submitted", nameof(JournalEntry), entry.Id,
            "إرسال قيد للمراجعة", "Journal entry submitted",
            entry.EntryNumber, entry.EntryNumber), cancellationToken);

        return BaseResponseDto<JournalEntryDto>.Ok(
            (await LoadDtoAsync(id, cancellationToken))!,
            "تم إرسال القيد للمراجعة.");
    }

    public Task<BaseResponseDto<JournalEntryDto>> StartReviewAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        ChangeReviewStatusAsync(
            id,
            WorkflowStatus.Submitted,
            WorkflowStatus.UnderReview,
            "ReviewStarted",
            null,
            cancellationToken);

    public Task<BaseResponseDto<JournalEntryDto>> ApproveAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        ChangeReviewStatusAsync(
            id,
            WorkflowStatus.UnderReview,
            WorkflowStatus.Approved,
            "Approved",
            null,
            cancellationToken);

    public Task<BaseResponseDto<JournalEntryDto>> RejectAsync(
        Guid id,
        ReviewJournalEntryRequest request,
        CancellationToken cancellationToken) =>
        ChangeReviewStatusAsync(
            id,
            WorkflowStatus.UnderReview,
            WorkflowStatus.Rejected,
            "Rejected",
            request.Reason,
            cancellationToken);

    public Task<BaseResponseDto<JournalEntryDto>> ReturnForCorrectionAsync(
        Guid id,
        ReviewJournalEntryRequest request,
        CancellationToken cancellationToken) =>
        ChangeReviewStatusAsync(
            id,
            WorkflowStatus.UnderReview,
            WorkflowStatus.ReturnedForCorrection,
            "ReturnedForCorrection",
            request.Reason,
            cancellationToken);

    public async Task<BaseResponseDto<PaginatedResult<JournalEntryDto>>> GetMyReviewQueueAsync(
        AccountingPagedRequest request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return BaseResponseDto<PaginatedResult<JournalEntryDto>>.Fail("المستخدم الحالي غير معروف.");
        }

        var tenantIds = await DbContext.ReviewerTenantAssignments
            .Where(x => x.ReviewerUserId == userId && x.IsActive)
            .Select(x => x.TenantId)
            .ToListAsync(cancellationToken);

        var query = DbContext.JournalEntries
            .IgnoreQueryFilters()
            .Where(x =>
                !x.IsDeleted &&
                (currentUser.IsSuperAdmin || tenantIds.Contains(x.TenantId)) &&
                (x.AssignedReviewerUserId == null || x.AssignedReviewerUserId == userId) &&
                (x.WorkflowStatus == WorkflowStatus.Submitted ||
                 x.WorkflowStatus == WorkflowStatus.UnderReview))
            .Include(x => x.Lines)
            .ThenInclude(x => x.Account)
            .AsQueryable();

        var result = await ToPagedAsync(
            query
                .OrderBy(x => x.CreatedAt)
                .Select(x => AccountingMapper.ToDto(x)),
            request,
            cancellationToken);

        return BaseResponseDto<PaginatedResult<JournalEntryDto>>.Ok(result);
    }

    public async Task<BaseResponseDto<IReadOnlyList<JournalEntryVersionDto>>> GetVersionsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!await DbContext.JournalEntries.AnyAsync(x => x.Id == id, cancellationToken))
        {
            return BaseResponseDto<IReadOnlyList<JournalEntryVersionDto>>.NotFound("Journal entry was not found.");
        }

        var versions = await DbContext.JournalEntryVersions
            .Where(x => x.JournalEntryId == id)
            .OrderByDescending(x => x.VersionNo)
            .Select(x => new JournalEntryVersionDto(
                x.Id,
                x.VersionNo,
                x.SnapshotJson,
                x.CreatedByUserId,
                x.CreatedAt,
                x.ChangeReason))
            .ToListAsync(cancellationToken);

        return BaseResponseDto<IReadOnlyList<JournalEntryVersionDto>>.Ok(versions);
    }

    private async Task<BaseResponseDto<JournalEntryDto>> ChangeReviewStatusAsync(
        Guid id,
        WorkflowStatus requiredStatus,
        WorkflowStatus targetStatus,
        string action,
        string? reason,
        CancellationToken cancellationToken)
    {
        var entry = await DbContext.JournalEntries
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entry is null)
        {
            return BaseResponseDto<JournalEntryDto>.NotFound("القيد غير موجود.");
        }

        if (!await workflowAccess.CanReviewTenantAsync(entry.TenantId, cancellationToken))
        {
            return BaseResponseDto<JournalEntryDto>.Fail("ليس لديك صلاحية مراجعة بيانات هذه الشركة.");
        }

        if (entry.AssignedReviewerUserId.HasValue &&
            entry.AssignedReviewerUserId != currentUser.UserId)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("القيد مسند إلى مراجع مالي آخر.");
        }

        if (entry.WorkflowStatus != requiredStatus)
        {
            return BaseResponseDto<JournalEntryDto>.Fail("لا يمكن تنفيذ الإجراء من حالة القيد الحالية.");
        }

        WorkflowStep? workflowStep = null;
        if (entry.WorkflowStepId.HasValue)
        {
            workflowStep = await DbContext.WorkflowSteps.FirstOrDefaultAsync(x => x.Id == entry.WorkflowStepId, cancellationToken);
        }
        var workflowAction = targetStatus switch
        {
            WorkflowStatus.Approved => WorkflowActionType.Approve,
            WorkflowStatus.Rejected => WorkflowActionType.Reject,
            WorkflowStatus.ReturnedForCorrection => WorkflowActionType.Return,
            _ => WorkflowActionType.Submit
        };
        if (workflowStep is not null && targetStatus != WorkflowStatus.UnderReview &&
            !await dynamicWorkflow.CanActAsync(workflowStep, workflowAction, cancellationToken))
        {
            return BaseResponseDto<JournalEntryDto>.Fail("The current workflow step does not allow this action for the current user.");
        }

        if (targetStatus is WorkflowStatus.Rejected or WorkflowStatus.ReturnedForCorrection &&
            string.IsNullOrWhiteSpace(reason))
        {
            return BaseResponseDto<JournalEntryDto>.Fail("يجب إدخال سبب واضح.");
        }

        var effectiveTarget = targetStatus;
        if (targetStatus == WorkflowStatus.Approved && workflowStep is not null && !workflowStep.IsFinalApproval)
        {
            var nextStep = await dynamicWorkflow.GetNextStepAsync(workflowStep.WorkflowDefinitionId, workflowStep.StepOrder, cancellationToken);
            if (nextStep is null)
            {
                return BaseResponseDto<JournalEntryDto>.Fail("Workflow is missing the next approval step.");
            }
            entry.WorkflowStepId = nextStep.Id;
            effectiveTarget = WorkflowStatus.UnderReview;
        }
        entry.WorkflowStatus = effectiveTarget;
        entry.AssignedReviewerUserId ??= currentUser.UserId;
        entry.ReviewReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            action,
            entry.TenantId,
            currentUser.UserId,
            nameof(JournalEntry),
            entry.Id.ToString(),
            newValues: entry.ReviewReason,
            cancellationToken: cancellationToken);
        if (workflowStep is not null && entry.WorkflowDefinitionId.HasValue)
        {
            await dynamicWorkflow.RecordActionAsync(
                nameof(JournalEntry), entry.Id, entry.WorkflowDefinitionId.Value, workflowStep.Id,
                requiredStatus.ToString(), effectiveTarget.ToString(), workflowAction,
                entry.ReviewReason, null, cancellationToken);
        }
        if (targetStatus is WorkflowStatus.Rejected or WorkflowStatus.ReturnedForCorrection && reason is not null)
        {
            await comments.AddAsync(new(nameof(JournalEntry), entry.Id, reason, true), cancellationToken);
        }
        if (entry.CreatedByUserId.HasValue && targetStatus != WorkflowStatus.UnderReview)
        {
            await notifications.CreateAsync(new(
                entry.CreatedByUserId.Value,
                targetStatus == WorkflowStatus.Approved ? "تم اعتماد القيد" : "تمت إعادة القيد",
                targetStatus == WorkflowStatus.Approved ? "Journal entry approved" : "Journal entry returned",
                entry.ReviewReason ?? entry.EntryNumber,
                entry.ReviewReason ?? entry.EntryNumber,
                nameof(JournalEntry), entry.Id), cancellationToken);
        }
        await activities.CreateAsync(new(
            action, nameof(JournalEntry), entry.Id,
            "تحديث دورة اعتماد القيد", "Journal workflow updated",
            entry.ReviewReason ?? entry.EntryNumber,
            entry.ReviewReason ?? entry.EntryNumber), cancellationToken);

        var message = targetStatus switch
        {
            WorkflowStatus.UnderReview => "تم بدء مراجعة القيد.",
            WorkflowStatus.Approved => "تم اعتماد القيد.",
            WorkflowStatus.Rejected => "تم رفض القيد وإعادته للمحاسب.",
            WorkflowStatus.ReturnedForCorrection => "تمت إعادة القيد للتصحيح.",
            _ => "تم تحديث حالة القيد."
        };

        return BaseResponseDto<JournalEntryDto>.Ok(
            (await LoadDtoAsync(id, cancellationToken))!,
            message);
    }

    private async Task<BaseResponseDto<object>> ValidateJournalAsync(
        Guid yearId,
        Guid periodId,
        DateOnly entryDate,
        IReadOnlyList<JournalEntryLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var period = await DbContext.AccountingPeriods
            .FirstOrDefaultAsync(
                x => x.Id == periodId && x.FinancialYearId == yearId,
                cancellationToken);

        if (period is null)
        {
            return BaseResponseDto<object>.NotFound("الفترة المحاسبية غير موجودة.");
        }

        if (period.Status is AccountingPeriodStatus.Closed or AccountingPeriodStatus.Locked)
        {
            return BaseResponseDto<object>.Fail("لا يمكن تعديل البيانات داخل فترة مقفلة أو مغلقة.");
        }

        if (entryDate < period.StartDate || entryDate > period.EndDate)
        {
            return BaseResponseDto<object>.Fail("يجب أن يقع تاريخ القيد داخل الفترة المحاسبية.");
        }

        if (lines.Count < 2)
        {
            return BaseResponseDto<object>.Fail("يجب أن يحتوي القيد على طرفين على الأقل.");
        }

        var totalDebit = lines.Sum(x => x.Debit);
        var totalCredit = lines.Sum(x => x.Credit);

        if (totalDebit != totalCredit)
        {
            return BaseResponseDto<object>.Fail("القيد غير متوازن.");
        }

        if (totalDebit <= 0)
        {
            return BaseResponseDto<object>.Fail("يجب أن يكون إجمالي القيد أكبر من صفر.");
        }

        var hasInvalidLine = lines.Any(x =>
            x.Debit < 0 ||
            x.Credit < 0 ||
            x.Debit > 0 && x.Credit > 0 ||
            x.Debit == 0 && x.Credit == 0);

        if (hasInvalidLine)
        {
            return BaseResponseDto<object>.Fail("يجب أن يحتوي كل سطر على مبلغ مدين أو دائن فقط.");
        }

        var accountIds = lines
            .Select(x => x.AccountId)
            .Distinct()
            .ToList();

        var validAccountsCount = await DbContext.Accounts
            .CountAsync(
                x => accountIds.Contains(x.Id) &&
                     x.IsActive &&
                     x.IsPostingAccount,
                cancellationToken);

        if (validAccountsCount != accountIds.Count)
        {
            return BaseResponseDto<object>.Fail("يجب أن تكون جميع الحسابات نشطة وتقبل الترحيل.");
        }

        var costCenterIds = lines
            .Where(x => x.CostCenterId.HasValue)
            .Select(x => x.CostCenterId!.Value)
            .Distinct()
            .ToList();

        if (costCenterIds.Count > 0)
        {
            var validCostCentersCount = await DbContext.CostCenters
                .CountAsync(
                    x => costCenterIds.Contains(x.Id) && x.IsActive,
                    cancellationToken);

            if (validCostCentersCount != costCenterIds.Count)
            {
                return BaseResponseDto<object>.Fail("يجب أن تكون جميع مراكز التكلفة نشطة.");
            }
        }

        return BaseResponseDto<object>.Ok(null);
    }

    private async Task<JournalEntryDto?> LoadDtoAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var dto = await DbContext.JournalEntries
            .Include(x => x.Lines)
            .ThenInclude(x => x.Account)
            .Include(x => x.Lines)
            .ThenInclude(x => x.CostCenter)
            .Where(x => x.Id == id)
            .Select(x => AccountingMapper.ToDto(x))
            .FirstOrDefaultAsync(cancellationToken);

        return dto;
    }
}

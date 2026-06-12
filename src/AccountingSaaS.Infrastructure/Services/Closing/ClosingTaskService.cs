using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Mapping;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class ClosingTaskService(
    AppDbContext dbContext,
    ICurrentTenantService currentTenant,
    ICurrentUserService currentUser,
    IAuditLogService auditLog)
    : AccountingServiceBase(dbContext, currentTenant), IClosingTaskService
{
    public async Task<BaseResponseDto<IReadOnlyList<ClosingTaskDto>>> GetTasksByPeriodAsync(
        Guid accountingPeriodId,
        CancellationToken cancellationToken)
    {
        var tasks = await DbContext.ClosingTasks
            .Where(x => x.AccountingPeriodId == accountingPeriodId)
            .OrderBy(x => x.SortOrder)
            .Select(x => AccountingMapper.ToDto(x))
            .ToListAsync(cancellationToken);

        return BaseResponseDto<IReadOnlyList<ClosingTaskDto>>.Ok(tasks);
    }

    public async Task<BaseResponseDto<ClosingTaskDto>> AssignTaskAsync(
        Guid id,
        AssignClosingTaskRequest request,
        CancellationToken cancellationToken)
    {
        var result = await UpdateTaskAsync(
            id,
            [
                ClosingTaskStatus.Pending,
                ClosingTaskStatus.InProgress,
                ClosingTaskStatus.Rejected
            ],
            task =>
            {
                task.AssignedToUserId = request.AssignedToUserId;
                task.DueDate = request.DueDate;
            },
            "Closing task assigned",
            cancellationToken);

        return result;
    }

    public async Task<BaseResponseDto<ClosingTaskDto>> StartTaskAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await UpdateTaskAsync(
            id,
            [
                ClosingTaskStatus.Pending,
                ClosingTaskStatus.Rejected
            ],
            task =>
            {
                task.Status = ClosingTaskStatus.InProgress;
                task.RejectionReason = null;
            },
            "Closing task started",
            cancellationToken);

        return result;
    }

    public async Task<BaseResponseDto<ClosingTaskDto>> SubmitTaskAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await UpdateTaskAsync(
            id,
            [ClosingTaskStatus.InProgress],
            task =>
            {
                task.Status = ClosingTaskStatus.Submitted;
                task.SubmittedAt = DateTimeOffset.UtcNow;
                task.SubmittedByUserId = currentUser.UserId;
            },
            "Closing task submitted",
            cancellationToken);

        return result;
    }

    public async Task<BaseResponseDto<ClosingTaskDto>> ApproveTaskAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await UpdateTaskAsync(
            id,
            [ClosingTaskStatus.Submitted],
            task =>
            {
                task.Status = ClosingTaskStatus.Approved;
                task.ApprovedAt = DateTimeOffset.UtcNow;
                task.ApprovedByUserId = currentUser.UserId;
            },
            "Closing task approved",
            cancellationToken);

        return result;
    }

    public async Task<BaseResponseDto<ClosingTaskDto>> MarkNotApplicableAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await UpdateTaskAsync(
            id,
            [
                ClosingTaskStatus.Pending,
                ClosingTaskStatus.InProgress,
                ClosingTaskStatus.Submitted
            ],
            task =>
            {
                task.Status = ClosingTaskStatus.NotApplicable;
            },
            "Closing task marked not applicable",
            cancellationToken);

        return result;
    }

    public async Task<BaseResponseDto<ClosingTaskDto>> RejectTaskAsync(
        Guid id,
        RejectClosingTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BaseResponseDto<ClosingTaskDto>.Fail("A rejection reason is required.");
        }

        var result = await UpdateTaskAsync(
            id,
            [ClosingTaskStatus.Submitted],
            task =>
            {
                task.Status = ClosingTaskStatus.Rejected;
                task.RejectedAt = DateTimeOffset.UtcNow;
                task.RejectedByUserId = currentUser.UserId;
                task.RejectionReason = request.Reason.Trim();
            },
            "Closing task rejected",
            cancellationToken);

        return result;
    }

    private async Task<BaseResponseDto<ClosingTaskDto>> UpdateTaskAsync(
        Guid id,
        IReadOnlyCollection<ClosingTaskStatus> allowedStatuses,
        Action<ClosingTask> update,
        string action,
        CancellationToken cancellationToken)
    {
        var task = await DbContext.ClosingTasks
            .FindAsync([id], cancellationToken);

        if (task is null)
        {
            return BaseResponseDto<ClosingTaskDto>.Fail("Closing task was not found.");
        }

        var isPeriodClosed = await PeriodIsClosedAsync(
            task.AccountingPeriodId,
            cancellationToken);

        if (isPeriodClosed)
        {
            return BaseResponseDto<ClosingTaskDto>.Fail(
                "Closing tasks cannot be modified in a closed period.");
        }

        if (!allowedStatuses.Contains(task.Status))
        {
            return BaseResponseDto<ClosingTaskDto>.Fail(
                $"Closing task cannot be changed from status {task.Status}.");
        }

        update(task);

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            action,
            TenantId,
            currentUser.UserId,
            nameof(ClosingTask),
            id.ToString(),
            cancellationToken: cancellationToken);

        var dto = AccountingMapper.ToDto(task);

        return BaseResponseDto<ClosingTaskDto>.Ok(dto, action);
    }
}
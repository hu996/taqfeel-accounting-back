using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class WorkflowAccessService(
    AppDbContext dbContext,
    ICurrentUserService currentUser) : IWorkflowAccessService
{
    public bool CanEdit(WorkflowStatus status, bool allowApprovedOverride = false) =>
        status is WorkflowStatus.Draft
            or WorkflowStatus.Rejected
            or WorkflowStatus.ReturnedForCorrection
        || allowApprovedOverride && status == WorkflowStatus.Approved;

    public async Task<bool> CanReviewTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsSuperAdmin || currentUser.IsAccountingOfficeAdmin)
        {
            return true;
        }

        if (currentUser.UserId is not { } userId)
        {
            return false;
        }

        return await dbContext.ReviewerTenantAssignments
            .AnyAsync(
                x => x.ReviewerUserId == userId &&
                     x.TenantId == tenantId &&
                     x.IsActive,
                cancellationToken);
    }
}

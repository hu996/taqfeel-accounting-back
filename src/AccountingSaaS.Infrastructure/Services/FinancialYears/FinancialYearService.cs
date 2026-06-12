using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Domain.Entities;
using AccountingSaaS.Domain.Enums;
using AccountingSaaS.Infrastructure.Mapping;
using AccountingSaaS.Infrastructure.Persistence;
using AccountingSaaS.Shared.Responses;
using Microsoft.EntityFrameworkCore;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class FinancialYearService(
    AppDbContext dbContext,
    ICurrentTenantService currentTenant,
    ICurrentUserService currentUser,
    IAuditLogService auditLog)
    : AccountingServiceBase(dbContext, currentTenant), IFinancialYearService
{
    public async Task<BaseResponseDto<FinancialYearDto>> CreateAsync(
        CreateFinancialYearRequest request,
        CancellationToken cancellationToken)
    {
        _ = TenantId;

        var isNameExists = await DbContext.FinancialYears.AnyAsync(
            x => x.YearName == request.YearName,
            cancellationToken);

        if (isNameExists)
        {
            return BaseResponseDto<FinancialYearDto>.Fail("اسم السنة المالية مستخدم من قبل.");
        }

        var isOverlapping = await DbContext.FinancialYears.AnyAsync(
            x => request.StartDate <= x.EndDate && request.EndDate >= x.StartDate,
            cancellationToken);

        if (isOverlapping)
        {
            return BaseResponseDto<FinancialYearDto>.Fail("توجد سنة مالية أخرى تتداخل مع نفس الفترة المحددة.");
        }

        var year = new FinancialYear
        {
            YearName = request.YearName,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        DbContext.FinancialYears.Add(year);

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "تم إنشاء سنة مالية",
            TenantId,
            currentUser.UserId,
            nameof(FinancialYear),
            year.Id.ToString(),
            newValues: year.YearName,
            cancellationToken: cancellationToken);

        return BaseResponseDto<FinancialYearDto>.Ok(
            AccountingMapper.ToDto(year),
            "تم إنشاء السنة المالية بنجاح.");
    }

    public async Task<BaseResponseDto<FinancialYearDto>> UpdateAsync(
        Guid id,
        UpdateFinancialYearRequest request,
        CancellationToken cancellationToken)
    {
        var year = await DbContext.FinancialYears.FindAsync(
            [id],
            cancellationToken);

        if (year is null)
        {
            return BaseResponseDto<FinancialYearDto>.Fail("السنة المالية غير موجودة.");
        }

        if (year.Status == FinancialYearStatus.Closed)
        {
            return BaseResponseDto<FinancialYearDto>.Fail("لا يمكن تعديل سنة مالية مغلقة.");
        }

        var isNameExists = await DbContext.FinancialYears.AnyAsync(
            x => x.Id != id && x.YearName == request.YearName,
            cancellationToken);

        if (isNameExists)
        {
            return BaseResponseDto<FinancialYearDto>.Fail("اسم السنة المالية مستخدم من قبل.");
        }

        var isOverlapping = await DbContext.FinancialYears.AnyAsync(
            x => x.Id != id
                 && request.StartDate <= x.EndDate
                 && request.EndDate >= x.StartDate,
            cancellationToken);

        if (isOverlapping)
        {
            return BaseResponseDto<FinancialYearDto>.Fail("توجد سنة مالية أخرى مفتوحة مع نفس الفترة المحددة.");
        }

        var old = year.YearName;

        year.YearName = request.YearName;
        year.StartDate = request.StartDate;
        year.EndDate = request.EndDate;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "تم تعديل السنة المالية",
            TenantId,
            currentUser.UserId,
            nameof(FinancialYear),
            id.ToString(),
            old,
            year.YearName,
            cancellationToken: cancellationToken);

        return BaseResponseDto<FinancialYearDto>.Ok(
            AccountingMapper.ToDto(year),
            "تم تعديل السنة المالية بنجاح.");
    }

    public async Task<BaseResponseDto<FinancialYearDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var year = await DbContext.FinancialYears.FindAsync(
            [id],
            cancellationToken);

        if (year is null)
        {
            return BaseResponseDto<FinancialYearDto>.Fail("السنة المالية غير موجودة.");
        }

        return BaseResponseDto<FinancialYearDto>.Ok(
            AccountingMapper.ToDto(year));
    }

    public async Task<BaseResponseDto<PaginatedResult<FinancialYearDto>>> GetPagedAsync(
        AccountingPagedRequest request,
        CancellationToken cancellationToken)
    {
        var query = DbContext.FinancialYears
            .OrderByDescending(x => x.StartDate)
            .Select(x => AccountingMapper.ToDto(x));

        var pagedResult = await ToPagedAsync(
            query,
            request,
            cancellationToken);

        return BaseResponseDto<PaginatedResult<FinancialYearDto>>.Ok(
            pagedResult,
            "تم تحميل السنوات المالية بنجاح.");
    }

    public async Task<BaseResponseDto<FinancialYearDto>> CloseYearAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var year = await DbContext.FinancialYears.FindAsync(
            [id],
            cancellationToken);

        if (year is null)
        {
            return BaseResponseDto<FinancialYearDto>.Fail("السنة المالية غير موجودة.");
        }

        var hasOpenedPeriods = await DbContext.AccountingPeriods.AnyAsync(
            x => x.FinancialYearId == id && x.Status != AccountingPeriodStatus.Closed,
            cancellationToken);

        if (hasOpenedPeriods)
        {
            return BaseResponseDto<FinancialYearDto>.Fail("لا يمكن إغلاق السنة المالية قبل إغلاق جميع الفترات المحاسبية التابعة لها.");
        }

        year.Status = FinancialYearStatus.Closed;
        year.ClosedAt = DateTimeOffset.UtcNow;
        year.ClosedByUserId = currentUser.UserId;

        await DbContext.SaveChangesAsync(cancellationToken);

        await auditLog.LogAsync(
            "تم إغلاق السنة المالية",
            TenantId,
            currentUser.UserId,
            nameof(FinancialYear),
            id.ToString(),
            cancellationToken: cancellationToken);

        return BaseResponseDto<FinancialYearDto>.Ok(
            AccountingMapper.ToDto(year),
            "تم إغلاق السنة المالية بنجاح.");
    }
}
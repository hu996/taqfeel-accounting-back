using AccountingSaaS.Application.Authorization;
using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AccountingReportsController : AccountingControllerBase
{
    private readonly IAccountingReportService _service;

    public AccountingReportsController(IAccountingReportService service)
    {
        _service = service;
    }

    [HttpGet("GetTrialBalance")]
    [HasPermission("AccountingReports.TrialBalance")]
    public async Task<IActionResult> TrialBalance(
        [FromQuery] ReportDateRangeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetTrialBalanceAsync(request, cancellationToken);
        return ApiResult(result);
    }

    [HttpGet("GetGeneralLedger")]
    [HasPermission("AccountingReports.GeneralLedger")]
    public async Task<IActionResult> GeneralLedger(
        [FromQuery] AccountReportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetGeneralLedgerAsync(request, cancellationToken);
        return ApiResult(result);
    }

    [HttpGet("GetAccountStatement")]
    [HasPermission("AccountingReports.AccountStatement")]
    public async Task<IActionResult> AccountStatement(
        [FromQuery] AccountReportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetAccountStatementAsync(request, cancellationToken);
        return ApiResult(result);
    }

    [HttpGet("GetClosingProgress/{accountingPeriodId:guid}")]
    [HasPermission("AccountingReports.ClosingProgress")]
    public async Task<IActionResult> ClosingProgress(
        Guid accountingPeriodId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetClosingProgressAsync(accountingPeriodId, cancellationToken);
        return ApiResult(result);
    }
}
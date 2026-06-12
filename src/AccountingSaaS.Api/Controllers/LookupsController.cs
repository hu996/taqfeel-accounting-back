using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Enums;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class LookupsController(ILookupService lookupService) : AccountingControllerBase
{
    [HttpGet("GetByType/{lookupType}")]
    public async Task<IActionResult> Get(
        LookupType lookupType,
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetLookupAsync(
            lookupType,
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetTenants")]
    public async Task<IActionResult> Tenants(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetTenantsAsync(
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetUsers")]
    public async Task<IActionResult> Users(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetUsersAsync(
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetRoles")]
    public async Task<IActionResult> Roles(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetRolesAsync(
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetPermissions")]
    public async Task<IActionResult> Permissions(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetPermissionsAsync(
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetFinancialYears")]
    public async Task<IActionResult> FinancialYears(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetFinancialYearsAsync(
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetAccountingPeriods")]
    public async Task<IActionResult> AccountingPeriods(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetAccountingPeriodsAsync(
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetChartOfAccounts")]
    public async Task<IActionResult> ChartOfAccounts(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetChartOfAccountsAsync(
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetPostingAccounts")]
    public async Task<IActionResult> PostingAccounts(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetPostingAccountsAsync(
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetParentAccounts")]
    public async Task<IActionResult> ParentAccounts(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetParentAccountsAsync(
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetCostCenters")]
    public async Task<IActionResult> CostCenters(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetCostCentersAsync(
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetAccountTypes")]
    public async Task<IActionResult> AccountTypes(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetLookupAsync(
            LookupType.AccountTypes,
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetNormalBalances")]
    public async Task<IActionResult> NormalBalances(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetLookupAsync(
            LookupType.NormalBalances,
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetDocumentTypes")]
    public async Task<IActionResult> DocumentTypes(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetLookupAsync(
            LookupType.DocumentTypes,
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetJournalEntryStatuses")]
    public async Task<IActionResult> JournalEntryStatuses(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetLookupAsync(
            LookupType.JournalEntryStatuses,
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetAccountingPeriodStatuses")]
    public async Task<IActionResult> AccountingPeriodStatuses(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetLookupAsync(
            LookupType.AccountingPeriodStatuses,
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetClosingTaskStatuses")]
    public async Task<IActionResult> ClosingTaskStatuses(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetLookupAsync(
            LookupType.ClosingTaskStatuses,
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetClosingSubmissionStatuses")]
    public async Task<IActionResult> ClosingSubmissionStatuses(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetLookupAsync(
            LookupType.ClosingSubmissionStatuses,
            request,
            cancellationToken);

        return ApiResult(result);
    }

    [HttpGet("GetImportTypes")]
    public async Task<IActionResult> ImportTypes(
        [FromQuery] LookupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await lookupService.GetLookupAsync(
            LookupType.ImportTypes,
            request,
            cancellationToken);

        return ApiResult(result);
    }
}
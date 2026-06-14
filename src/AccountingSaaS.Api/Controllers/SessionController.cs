using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/session")]
public sealed class SessionController : AccountingControllerBase
{
    private readonly ISessionService sessionService;

    public SessionController(ISessionService sessionService)
    {
        this.sessionService = sessionService;
    }

    [HttpGet("context")]
    public async Task<IActionResult> GetContext(
        CancellationToken cancellationToken)
    {
        var result = await sessionService.GetContextAsync(cancellationToken);
        return ApiResult(result);
    }

    [HttpPost("switch-active-financial-year")]
    public async Task<IActionResult> SwitchActiveFinancialYear(
        SwitchActiveFinancialYearRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sessionService.SwitchActiveFinancialYearAsync(
            request,
            cancellationToken);
        return ApiResult(result);
    }
}

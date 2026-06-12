using AccountingSaaS.Application.DTOs;
using AccountingSaaS.Application.Interfaces;
using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(IAuthService authService) : AccountingControllerBase
{
    [HttpPost("Login")]
    [AllowAnonymous]
    public Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken) =>
        ToResult(authService.LoginAsync(request, IpAddress(), UserAgent(), cancellationToken));

    [HttpPost("RefreshToken")]
    [AllowAnonymous]
    public Task<IActionResult> RefreshToken(RefreshTokenRequest request, CancellationToken cancellationToken) =>
        ToResult(authService.RefreshTokenAsync(request, IpAddress(), UserAgent(), cancellationToken));

    [HttpPost("Logout")]
    [Authorize]
    public Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken) =>
        ToResult(authService.LogoutAsync(request, IpAddress(), UserAgent(), cancellationToken));

    [HttpGet("GetCurrentUser")]
    [Authorize]
    public Task<IActionResult> Me(CancellationToken cancellationToken) => ToResult(authService.MeAsync(cancellationToken));

    private string? IpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? UserAgent() => Request.Headers.UserAgent.FirstOrDefault();

    private async Task<IActionResult> ToResult<T>(Task<BaseResponseDto<T>> task)
    {
        var result = await task;
        return ApiResult(result);
    }
}

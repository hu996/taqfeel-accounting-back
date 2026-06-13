using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

[Authorize]
public abstract class AccountingControllerBase : ControllerBase
{
    protected IActionResult ApiResult<T>(BaseResponseDto<T> response)
    {
        if (response.Success)
        {
            return Ok(response);
        }

        return response.HttpStatusCode == StatusCodes.Status404NotFound
            ? NotFound(response)
            : BadRequest(response);
    }
}

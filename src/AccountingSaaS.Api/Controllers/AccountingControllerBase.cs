using AccountingSaaS.Shared.Responses;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSaaS.Api.Controllers;

public abstract class AccountingControllerBase : ControllerBase
{
    protected IActionResult ApiResult<T>(BaseResponseDto<T> response) => Ok(response);
}

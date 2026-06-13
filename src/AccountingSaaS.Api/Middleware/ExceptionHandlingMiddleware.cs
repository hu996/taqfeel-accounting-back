using System.Net;
using AccountingSaaS.Shared.Responses;

namespace AccountingSaaS.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled API exception");
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                BaseResponseDto<object>.Fail("حدث خطأ غير متوقع في الخادم. يرجى المحاولة مرة أخرى."));
        }
    }
}

using System.Net;
using AccountingSaaS.Shared.Responses;

namespace AccountingSaaS.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
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
            var errors = environment.IsProduction() ? ["An unexpected error occurred."] : new[] { ex.Message };
            await context.Response.WriteAsJsonAsync(BaseResponseDto<object>.Fail("An error occurred.", errors));
        }
    }
}

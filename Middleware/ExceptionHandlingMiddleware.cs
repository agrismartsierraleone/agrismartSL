using System.Net;
using AgrismartAPI.Application.Common;

namespace AgrismartAPI.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var statusCode = exception switch
            {
                NotFoundException => HttpStatusCode.NotFound,
                ConflictException => HttpStatusCode.Conflict,
                ValidationException => HttpStatusCode.BadRequest,
                ForbiddenException => HttpStatusCode.Forbidden,
                ExternalServiceException => HttpStatusCode.BadGateway,
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,
                _ => HttpStatusCode.InternalServerError
            };

            if (statusCode == HttpStatusCode.InternalServerError)
            {
                logger.LogError(exception, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;
            if (exception is ValidationException { Errors: not null } validationException)
            {
                await context.Response.WriteAsJsonAsync(new { message = exception.Message, errors = validationException.Errors });
                return;
            }

            await context.Response.WriteAsJsonAsync(new { message = exception.Message });
        }
    }
}

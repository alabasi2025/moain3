using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Hrms.Api.Common;

public sealed class ProblemExceptionMiddleware(
    RequestDelegate next,
    ILogger<ProblemExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            int statusCode = GetStatusCode(exception);

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(
                    exception,
                    "Unhandled request exception. Trace identifier: {TraceIdentifier}",
                    context.TraceIdentifier);
            }

            var problem = new ProblemDetails
            {
                Type = "about:blank",
                Title = GetTitle(statusCode),
                Status = statusCode,
                Detail = statusCode == StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred."
                    : exception.Message,
                Instance = context.Request.PathBase.Add(context.Request.Path).Value
            };

            problem.Extensions["traceId"] = context.TraceIdentifier;

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                problem,
                SerializerOptions,
                context.RequestAborted);
        }
    }

    private static int GetStatusCode(Exception exception)
    {
        if (IsConcurrencyException(exception))
        {
            return StatusCodes.Status409Conflict;
        }

        return exception switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static bool IsConcurrencyException(Exception exception)
    {
        for (Type? type = exception.GetType(); type is not null; type = type.BaseType)
        {
            if (string.Equals(
                type.Name,
                "DbUpdateConcurrencyException",
                StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetTitle(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status400BadRequest => "Bad Request",
            StatusCodes.Status403Forbidden => "Forbidden",
            StatusCodes.Status404NotFound => "Not Found",
            StatusCodes.Status409Conflict => "Conflict",
            StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
            _ => "Internal Server Error"
        };
    }
}

public static class ProblemExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseProblemExceptions(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ProblemExceptionMiddleware>();
    }
}

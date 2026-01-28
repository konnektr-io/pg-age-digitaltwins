using AgeDigitalTwins.Exceptions;
using DTDLParser;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace AgeDigitalTwins.ApiService;

public class ExceptionHandler : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        if (exception is AgeDigitalTwinsException ageException)
        {
            httpContext.Response.StatusCode = (int)ageException.StatusCode;
        }
        else if (
            exception is NpgsqlException npgsqlEx
            && npgsqlEx.Message.Contains("No suitable host was found")
        )
        {
            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }
        else if (exception is ResolutionException or PostgresException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
        else
        {
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Title = "An error occurred",
                Detail = $"{exception.Message}\n{exception.StackTrace}",
                Type = exception.GetType().Name,
                Status = httpContext.Response.StatusCode,
            },
            cancellationToken: cancellationToken
        );

        return true;
    }
}

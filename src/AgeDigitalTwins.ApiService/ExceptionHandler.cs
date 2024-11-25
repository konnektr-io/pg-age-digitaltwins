using AgeDigitalTwins.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService;

public class ExceptionHandler : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = (exception is DigitalTwinNotFoundException)
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = "An error occurred",
            Detail = $"{exception.Message}\n{exception.InnerException?.Message}",
            Type = exception.GetType().Name,
            Status = httpContext.Response.StatusCode,
        }, cancellationToken: cancellationToken);

        return true;
    }


}

public class ExceptionResponses
{
    /* public Dictionary<Type, ProblemDetails> ExceptionResponsesMap { get; } = new Dictionary<Type, ProblemDetails>
        {
            { typeof(ModelNotFoundException), new ProblemDetails
                {
                    Title = "An error occurred",
                    Detail = exception.Message,
                    Type = exception.GetType().Name,
                    Status = StatusCodes.Status400BadRequest
                }



            Results.BadRequest("Model not found") },
            { typeof(DigitalTwinNotFoundException), Results.NotFound("Digital twin not found") },
            { typeof(ValidationFailedException), Results.BadRequest("Validation failed") },
            { typeof(InvalidAdtQueryException), Results.BadRequest("Invalid ADT query") }
        }; */
}

using AgeDigitalTwins.Exceptions;

namespace AgeDigitalTwins.ApiService
{
    public class ExceptionResponses
    {
        public Dictionary<Type, IResult> ExceptionResponsesMap { get; } = new Dictionary<Type, IResult>
        {
            { typeof(ModelNotFoundException), Results.BadRequest("Model not found") },
            { typeof(DigitalTwinNotFoundException), Results.NotFound("Digital twin not found") },
            { typeof(ValidationFailedException), Results.BadRequest("Validation failed") },
            { typeof(InvalidAdtQueryException), Results.BadRequest("Invalid ADT query") }
        };
    }
}
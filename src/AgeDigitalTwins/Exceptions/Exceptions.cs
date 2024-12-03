using System;
using System.Net;

namespace AgeDigitalTwins.Exceptions;

public class AgeDigitalTwinsException : Exception
{
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.InternalServerError;
    public AgeDigitalTwinsException(string message) : base(message)
    {
    }
}

public class PreconditionFailedException : AgeDigitalTwinsException
{
    public PreconditionFailedException(string message) : base(message)
    {
        StatusCode = HttpStatusCode.PreconditionFailed;
    }
}

public class ModelNotFoundException : AgeDigitalTwinsException
{
    public ModelNotFoundException(string message) : base(message)
    {
        StatusCode = HttpStatusCode.NotFound;
    }
}

public class DigitalTwinNotFoundException : AgeDigitalTwinsException
{
    public DigitalTwinNotFoundException(string message) : base(message)
    {
        StatusCode = HttpStatusCode.NotFound;
    }
}

public class ValidationFailedException : AgeDigitalTwinsException
{
    public ValidationFailedException(string message) : base(message)
    {
        StatusCode = HttpStatusCode.BadRequest;
    }
}

public class InvalidAdtQueryException : AgeDigitalTwinsException
{
    public InvalidAdtQueryException(string message) : base(message)
    {
        StatusCode = HttpStatusCode.BadRequest;
    }
}
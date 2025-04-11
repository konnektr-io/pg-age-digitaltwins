using System;
using System.Net;

namespace AgeDigitalTwins.Exceptions;

public class AgeDigitalTwinsException : Exception
{
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.InternalServerError;

    public AgeDigitalTwinsException(string message)
        : base(message) { }
}

public class PreconditionFailedException : AgeDigitalTwinsException
{
    public PreconditionFailedException(string message)
        : base(message)
    {
        StatusCode = HttpStatusCode.PreconditionFailed;
    }
}

public class ModelAlreadyExistsException : AgeDigitalTwinsException
{
    public ModelAlreadyExistsException()
        : base("The model provided already exists.")
    {
        StatusCode = HttpStatusCode.Conflict;
    }

    public ModelAlreadyExistsException(string message)
        : base(message)
    {
        StatusCode = HttpStatusCode.Conflict;
    }
}

public class ModelNotFoundException : AgeDigitalTwinsException
{
    public ModelNotFoundException(string message)
        : base(message)
    {
        StatusCode = HttpStatusCode.NotFound;
    }
}

public class ModelReferencesNotDeletedException : AgeDigitalTwinsException
{
    public ModelReferencesNotDeletedException()
        : base("The model refers to models that are not deleted.")
    {
        StatusCode = HttpStatusCode.Conflict;
    }
}

public class DTDLParserParsingException : AgeDigitalTwinsException
{
    public DTDLParserParsingException(DTDLParser.ParsingException exception)
        : base("The models provided are not valid DTDL.")
    {
        StatusCode = HttpStatusCode.BadRequest;
    }
}

public class NotSupportedException : AgeDigitalTwinsException
{
    public NotSupportedException(string message)
        : base(message)
    {
        StatusCode = HttpStatusCode.BadRequest;
    }
}

public class DigitalTwinNotFoundException : AgeDigitalTwinsException
{
    public DigitalTwinNotFoundException(string message)
        : base(message)
    {
        StatusCode = HttpStatusCode.NotFound;
    }
}

public class RelationshipNotFoundException : AgeDigitalTwinsException
{
    public RelationshipNotFoundException(string message)
        : base(message)
    {
        StatusCode = HttpStatusCode.NotFound;
    }
}

public class ValidationFailedException : AgeDigitalTwinsException
{
    public ValidationFailedException(string message)
        : base(message)
    {
        StatusCode = HttpStatusCode.BadRequest;
    }
}

public class InvalidAdtQueryException : AgeDigitalTwinsException
{
    public InvalidAdtQueryException(string message)
        : base(message)
    {
        StatusCode = HttpStatusCode.BadRequest;
    }
}

using System;
using System.Net;

namespace AgeDigitalTwins.Exceptions;

public class AgeDigitalTwinsException(string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.InternalServerError;
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
        : base($"The models provided could not be parsed:\n{exception}")
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

public class ComponentNotFoundException : AgeDigitalTwinsException
{
    public ComponentNotFoundException(string message)
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

public class DatabaseConnectivityException : Exception
{
    public DatabaseConnectivityException(string message)
        : base(message) { }

    public DatabaseConnectivityException(string message, Exception innerException)
        : base(message, innerException) { }
}

public class ModelExtendsChangedException : AgeDigitalTwinsException
{
    public ModelExtendsChangedException()
        : base("Changing what a model extends is not supported.")
    {
        StatusCode = HttpStatusCode.BadRequest;
    }

    public ModelExtendsChangedException(string message)
        : base(message)
    {
        StatusCode = HttpStatusCode.BadRequest;
    }
}

public class ModelUpdateValidationException : AgeDigitalTwinsException
{
    public ModelUpdateValidationException(string message)
        : base(message)
    {
        StatusCode = HttpStatusCode.BadRequest;
    }
}

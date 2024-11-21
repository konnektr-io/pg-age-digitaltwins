using System;

namespace AgeDigitalTwins.Exceptions;

public class ModelNotFoundException : Exception
{
    public ModelNotFoundException(string message) : base(message)
    {
    }
}

public class DigitalTwinNotFoundException : Exception
{
    public DigitalTwinNotFoundException(string message) : base(message)
    {
    }
}

public class ValidationFailedException : Exception
{
    public ValidationFailedException(string message) : base(message)
    {
    }
}

public class InvalidAdtQueryException : Exception
{
    public InvalidAdtQueryException(string message) : base(message)
    {
    }
}
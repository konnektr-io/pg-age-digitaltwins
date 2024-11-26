using System;

namespace AgeDigitalTwins.Exceptions;

public class AgeDigitalTwinsException : Exception
{
    public AgeDigitalTwinsException(string message) : base(message)
    {
    }
}

public class ModelNotFoundException : AgeDigitalTwinsException
{
    public ModelNotFoundException(string message) : base(message)
    {
    }
}

public class DigitalTwinNotFoundException : AgeDigitalTwinsException
{
    public DigitalTwinNotFoundException(string message) : base(message)
    {
    }
}

public class ValidationFailedException : AgeDigitalTwinsException
{
    public ValidationFailedException(string message) : base(message)
    {
    }
}

public class InvalidAdtQueryException : AgeDigitalTwinsException
{
    public InvalidAdtQueryException(string message) : base(message)
    {
    }
}
using System;

namespace AgeDigitalTwins.Exceptions
{
    public class InvalidArgumentException : Exception
    {
        public InvalidArgumentException(string message) : base(message) { }
    }

    public class JsonPatchInvalidException : Exception
    {
        public JsonPatchInvalidException(string message) : base(message) { }
    }

    public class ValidationFailedException : Exception
    {
        public ValidationFailedException(string message) : base(message) { }
    }

    public class DigitalTwinNotFoundException : Exception
    {
        public DigitalTwinNotFoundException(string message) : base(message) { }
    }

    public class PreconditionFailedException : Exception
    {
        public PreconditionFailedException(string message) : base(message) { }
    }
}
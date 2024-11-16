using System;

namespace AgeDigitalTwins.Exceptions
{
    public class InnerError
    {
        public string Code { get; set; }
        public InnerError Innererror { get; set; }
    }

    public class Error
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public InnerError Innererror { get; set; }
        public Error[] Details { get; set; }
    }

    public class ErrorResponse
    {
        public Error Error { get; set; }
    }
    
    public class CustomException : Exception
    {
        public Error Error { get; }

        public CustomException(string code, string message, InnerError innerError = null, Error[] details = null)
            : base(message)
        {
            Error = new Error
            {
                Code = code,
                Message = message,
                Innererror = innerError,
                Details = details
            };
        }
    }

    
    public class InvalidArgumentException : CustomException
    {
        private const string DefaultCode = "InvalidArgument";
        private const string DefaultMessage = "The digital twin id or payload is invalid.";

        public InvalidArgumentException(string additionalInfo = null)
            : base(DefaultCode, $"{DefaultMessage} {additionalInfo}") { }
    }

    public class JsonPatchInvalidException : CustomException
    {
        private const string DefaultCode = "JsonPatchInvalid";
        private const string DefaultMessage = "The JSON Patch provided is invalid.";

        public JsonPatchInvalidException(string additionalInfo = null)
            : base(DefaultCode, $"{DefaultMessage} {additionalInfo}") { }
    }

    public class ValidationFailedException : CustomException
    {
        private const string DefaultCode = "ValidationFailed";
        private const string DefaultMessage = "Applying the patch results in an invalid digital twin.";

        public ValidationFailedException(string additionalInfo = null)
            : base(DefaultCode, $"{DefaultMessage} {additionalInfo}") { }
    }

    public class DigitalTwinNotFoundException : CustomException
    {
        private const string DefaultCode = "DigitalTwinNotFound";
        private const string DefaultMessage = "The digital twin was not found.";

        public DigitalTwinNotFoundException(string additionalInfo = null)
            : base(DefaultCode, $"{DefaultMessage} {additionalInfo}") { }
    }

    public class PreconditionFailedException : CustomException
    {
        private const string DefaultCode = "PreconditionFailed";
        private const string DefaultMessage = "The precondition check (If-Match or If-None-Match) failed.";

        public PreconditionFailedException(string additionalInfo = null)
            : base(DefaultCode, $"{DefaultMessage} {additionalInfo}") { }
    }
}
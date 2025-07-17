namespace AgeDigitalTwins.Models;

/// <summary>
/// Represents the result of an individual digital twin operation within a batch.
/// </summary>
public class DigitalTwinOperationResult
{
    /// <summary>
    /// Gets the ID of the digital twin.
    /// </summary>
    public string DigitalTwinId { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful operation result.
    /// </summary>
    /// <param name="digitalTwinId">The ID of the digital twin.</param>
    /// <returns>A successful operation result.</returns>
    public static DigitalTwinOperationResult Success(string digitalTwinId)
    {
        return new DigitalTwinOperationResult { DigitalTwinId = digitalTwinId, IsSuccess = true };
    }

    /// <summary>
    /// Creates a failed operation result.
    /// </summary>
    /// <param name="digitalTwinId">The ID of the digital twin.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed operation result.</returns>
    public static DigitalTwinOperationResult Failure(string digitalTwinId, string errorMessage)
    {
        return new DigitalTwinOperationResult
        {
            DigitalTwinId = digitalTwinId,
            IsSuccess = false,
            ErrorMessage = errorMessage,
        };
    }
}

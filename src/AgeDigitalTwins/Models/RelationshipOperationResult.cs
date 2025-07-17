namespace AgeDigitalTwins.Models;

/// <summary>
/// Represents the result of an individual relationship operation within a batch.
/// </summary>
public class RelationshipOperationResult
{
    /// <summary>
    /// Gets the ID of the source digital twin.
    /// </summary>
    public string SourceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the ID of the relationship.
    /// </summary>
    public string RelationshipId { get; init; } = string.Empty;

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
    /// <param name="sourceId">The ID of the source digital twin.</param>
    /// <param name="relationshipId">The ID of the relationship.</param>
    /// <returns>A successful operation result.</returns>
    public static RelationshipOperationResult Success(string sourceId, string relationshipId)
    {
        return new RelationshipOperationResult
        {
            SourceId = sourceId,
            RelationshipId = relationshipId,
            IsSuccess = true,
        };
    }

    /// <summary>
    /// Creates a failed operation result.
    /// </summary>
    /// <param name="sourceId">The ID of the source digital twin.</param>
    /// <param name="relationshipId">The ID of the relationship.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed operation result.</returns>
    public static RelationshipOperationResult Failure(
        string sourceId,
        string relationshipId,
        string errorMessage
    )
    {
        return new RelationshipOperationResult
        {
            SourceId = sourceId,
            RelationshipId = relationshipId,
            IsSuccess = false,
            ErrorMessage = errorMessage,
        };
    }
}

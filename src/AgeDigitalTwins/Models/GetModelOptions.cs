namespace AgeDigitalTwins.Models;

public class GetModelOptions
{
    /// <summary>
    /// When true the contents of the base models will be included in the result.
    /// </summary>
    public bool IncludeBaseModelContents { get; set; } = false;
}

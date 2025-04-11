namespace AgeDigitalTwins.Models
{
    public class GetModelsOptions
    {
        /// <summary>
        /// If specified, only return the set of the specified models along with their dependencies. If omitted, all models are retrieved.
        /// </summary>
        public string[]? DependenciesFor { get; set; }

        /// <summary>
        /// When true the model definition will be returned as part of the result.
        /// </summary>
        public bool IncludeModelDefinition { get; set; } = false;
    }
}

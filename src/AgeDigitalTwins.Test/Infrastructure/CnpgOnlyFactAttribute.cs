namespace AgeDigitalTwins.Test;

public sealed class CnpgOnlyFactAttribute : FactAttribute
{
    public CnpgOnlyFactAttribute()
    {
        var cnpgTest = Environment.GetEnvironmentVariable("CNPG_TEST");
        if (string.IsNullOrEmpty(cnpgTest) || !cnpgTest.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            Skip = "Test requires CNPG AGE image with pgvector extension";
        }
    }
}

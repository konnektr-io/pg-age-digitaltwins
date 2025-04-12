namespace AgeDigitalTwins.Test;

public class VariableLengthEdgeTests
{
    [Theory]
    [InlineData("MATCH (a:Twin)-[*]->(b:Twin) RETURN a, b", true)]
    [InlineData("MATCH (a:Twin)-[*2..6]->(b:Twin) RETURN a, b", true)]
    [InlineData("MATCH (a:Twin)-[r:has]->(b:Twin) RETURN a, b", false)]
    [InlineData("MATCH (a:Twin)-[r:has*..3]->(b:Twin) RETURN a, b", true)]
    [InlineData("MATCH (a:Twin)-[:has*..3]->(b:Twin) RETURN a, b", true)]
    [InlineData("MATCH (a:Twin)-[r*..3]->(b:Twin) RETURN a, b", true)]
    [InlineData("MATCH (a:Twin)-[]->()-[]->(b:Twin) RETURN a, b", false)]
    [InlineData("MATCH (a:Twin)-[]->()-[*3..]->(b:Twin) RETURN a, b", true)]
    public void VariableLengthEdge_IsDetected(string cypher, bool expected)
    {
        var isVariableLengthEdgeQuery = AgeDigitalTwinsClient
            .VariableLengthEdgeRegex()
            .IsMatch(cypher);
        Assert.Equal(expected, isVariableLengthEdgeQuery);
    }
}

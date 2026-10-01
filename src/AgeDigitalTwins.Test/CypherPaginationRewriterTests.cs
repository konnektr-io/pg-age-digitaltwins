using AgeDigitalTwins;

namespace AgeDigitalTwins.Test;

/// <summary>
/// Unit tests for the pagination clause rewrite that <c>QueryAsync</c> applies before the Cypher
/// reaches Apache AGE. These run without a database (<c>Category!=Integration</c>).
///
/// Issue #113 showed this rewrite is load-bearing and entirely untested: it decides where SKIP/LIMIT
/// land relative to ORDER BY, and Cypher only accepts SKIP/LIMIT as the final clauses.
/// </summary>
public class CypherPaginationRewriterTests
{
    // --- first page (rowNumber == 0) ---

    [Fact]
    public void Apply_FirstPage_NoContinuation_AddsNothing()
    {
        const string cypher = "MATCH (t:Twin) RETURN t";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 0, maxItemsPerPage: null);

        Assert.Equal(cypher, result);
    }

    [Fact]
    public void Apply_FirstPage_WithPageSize_AppendsLimitAtEnd()
    {
        const string cypher = "MATCH (t:Twin) RETURN t";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 0, maxItemsPerPage: 50);

        Assert.Equal("MATCH (t:Twin) RETURN t LIMIT 50", result);
    }

    // --- ORDER BY placement (the #113 concern) ---

    [Fact]
    public void Apply_OrderByBeforeReturn_KeepsOrderByAheadOfLimit()
    {
        const string cypher = "MATCH (t:Twin) WITH t ORDER BY t.name RETURN t";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 0, maxItemsPerPage: 10);

        Assert.Equal("MATCH (t:Twin) WITH t ORDER BY t.name RETURN t LIMIT 10", result);
    }

    [Fact]
    public void Apply_OrderByAfterReturn_MovesNothing_SkipLimitGoAfterOrderBy()
    {
        // Cypher requires SKIP/LIMIT to be the final clauses, so a trailing ORDER BY
        // combined with pagination produces a query AGE cannot parse. This is the
        // shape the issue reporter used as the workaround.
        const string cypher = "MATCH (t:Twin) WITH t, toInteger(t.returnPeriod) AS rp RETURN t AS twin ORDER BY rp";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 0, maxItemsPerPage: 10);

        // The ORDER BY is still last, so SKIP/LIMIT could not be appended after it
        // without reordering. Assert on the actual (broken) shape so a future fix
        // has to update this test deliberately.
        Assert.Equal(
            "MATCH (t:Twin) WITH t, toInteger(t.returnPeriod) AS rp RETURN t AS twin ORDER BY rp LIMIT 10",
            result
        );
        Assert.EndsWith("ORDER BY rp LIMIT 10", result);
    }

    [Fact]
    public void Apply_OrderByAfterReturn_WithContinuation_AppendsSkipAfterOrderBy()
    {
        const string cypher = "MATCH (t:Twin) WITH t, toInteger(t.returnPeriod) AS rp RETURN t AS twin ORDER BY rp";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 20, maxItemsPerPage: 10);

        Assert.Equal(
            "MATCH (t:Twin) WITH t, toInteger(t.returnPeriod) AS rp RETURN t AS twin ORDER BY rp SKIP 20 LIMIT 10",
            result
        );
    }

    [Fact]
    public void Apply_OrderByBeforeReturn_WithContinuation_OrdersBeforeSkipping()
    {
        const string cypher = "MATCH (t:Twin) WITH t ORDER BY t.returnPeriod RETURN t";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 20, maxItemsPerPage: 10);

        Assert.Equal(
            "MATCH (t:Twin) WITH t ORDER BY t.returnPeriod RETURN t SKIP 20 LIMIT 10",
            result
        );
        // ORDER BY must precede SKIP for the offset to land on the sorted stream.
        Assert.True(
            result.IndexOf("ORDER BY", StringComparison.Ordinal)
                < result.IndexOf("SKIP", StringComparison.Ordinal)
        );
    }

    // --- existing SKIP / LIMIT ---

    [Fact]
    public void Apply_ExistingSkip_AddsContinuationRowNumberToIt()
    {
        const string cypher = "MATCH (t:Twin) RETURN t SKIP 5";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 10, maxItemsPerPage: null);

        Assert.Equal("MATCH (t:Twin) RETURN t SKIP 15", result);
    }

    [Fact]
    public void Apply_ExistingLimit_WithContinuation_MovesLimitToEndAndReEmits()
    {
        const string cypher = "MATCH (t:Twin) RETURN t LIMIT 100";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 40, maxItemsPerPage: null);

        Assert.Equal("MATCH (t:Twin) RETURN t SKIP 40 LIMIT 100", result);
    }

    [Fact]
    public void Apply_ExistingLimit_SmallerPageSize_Wins()
    {
        const string cypher = "MATCH (t:Twin) RETURN t LIMIT 100";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 0, maxItemsPerPage: 25);

        Assert.Equal("MATCH (t:Twin) RETURN t LIMIT 25", result);
    }

    [Fact]
    public void Apply_ExistingLimit_LargerPageSize_KeepsOriginal()
    {
        const string cypher = "MATCH (t:Twin) RETURN t LIMIT 10";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 0, maxItemsPerPage: 500);

        Assert.Equal("MATCH (t:Twin) RETURN t LIMIT 10", result);
    }

    [Fact]
    public void Apply_ExistingSkipAndLimit_WithContinuation_AdjustsSkipOnly()
    {
        const string cypher = "MATCH (t:Twin) RETURN t SKIP 5 LIMIT 100";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 10, maxItemsPerPage: null);

        Assert.Equal("MATCH (t:Twin) RETURN t SKIP 15 LIMIT 100", result);
    }

    // --- case-insensitivity ---

    [Fact]
    public void Apply_LowercaseSkipAndLimit_AreRecognised()
    {
        const string cypher = "MATCH (t:Twin) RETURN t skip 5 limit 100";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 10, maxItemsPerPage: null);

        // SKIP/LIMIT are matched case-insensitively, but the surviving LIMIT keeps the
        // casing the caller wrote — the rewrite normalises what it appends, not what it leaves.
        Assert.Contains("SKIP 15", result);
        Assert.Contains("limit 100", result);
    }

    [Fact]
    public void Apply_MultilineWhitespace_IsRecognised()
    {
        const string cypher = "MATCH (t:Twin)\nRETURN t\nSKIP   5\nLIMIT   100";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 10, maxItemsPerPage: null);

        // Both clauses are already present, so SKIP is adjusted in place and LIMIT is kept
        // where the caller wrote it.
        Assert.Contains("SKIP 15", result);
        Assert.Contains("LIMIT   100", result);
    }

    // --- the #113 shapes, verbatim ---

    [Fact]
    public void Apply_Issue113Case2_MapProjectionWithSortAlias()
    {
        const string cypher =
            "MATCH (t:Twin)\n"
            + "WHERE (digitaltwins.is_of_model(t, 'dtmi:com:example:contoso:Widget;1')\n"
            + "  OR digitaltwins.is_of_model(t, 'dtmi:com:example:contoso:SubWidget;1'))\n"
            + "WITH t, toInteger(t.returnPeriod) AS rp\n"
            + "ORDER BY rp ASC\n"
            + "RETURN t { .*, zoomable: false } AS twin";

        var firstPage = CypherPaginationRewriter.Apply(cypher, rowNumber: 0, maxItemsPerPage: 100);

        Assert.Equal(cypher + " LIMIT 100", firstPage);

        var secondPage = CypherPaginationRewriter.Apply(cypher, rowNumber: 100, maxItemsPerPage: 100);

        Assert.Equal(cypher + " SKIP 100 LIMIT 100", secondPage);
        // ORDER BY stays ahead of the pagination clauses across pages.
        Assert.True(
            secondPage.IndexOf("ORDER BY", StringComparison.Ordinal)
                < secondPage.IndexOf("SKIP", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void Apply_Issue113Case1_MapProjectionAliasShadowingNodeVariable()
    {
        const string cypher =
            "MATCH (t:Twin)\n"
            + "WITH t\n"
            + "ORDER BY t.returnPeriod ASC\n"
            + "RETURN t { .*, zoomable: false } AS t";

        var result = CypherPaginationRewriter.Apply(cypher, rowNumber: 50, maxItemsPerPage: 50);

        Assert.Equal(cypher + " SKIP 50 LIMIT 50", result);
    }
}
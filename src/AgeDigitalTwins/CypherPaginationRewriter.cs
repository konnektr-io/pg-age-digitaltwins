using System;
using System.Text.RegularExpressions;

namespace AgeDigitalTwins;

/// <summary>
/// Pure rewriting of a Cypher query for keyset-free offset pagination.
///
/// This logic used to live inline in <see cref="AgeDigitalTwinsClient.QueryAsync{T}"/>, where it
/// could only be exercised against a live AGE database. It is extracted here so the rewrite is
/// unit-testable: the clauses this produces are what Apache AGE actually receives, and the
/// ordering of <c>ORDER BY</c> / <c>SKIP</c> / <c>LIMIT</c> is significant (issue #113).
/// </summary>
internal static partial class CypherPaginationRewriter
{
    /// <summary>
    /// Applies the pagination clause to <paramref name="cypher"/>.
    /// </summary>
    /// <param name="cypher">The Cypher query to rewrite.</param>
    /// <param name="rowNumber">
    /// The number of rows already consumed, taken from the continuation token. 0 for the first page.
    /// </param>
    /// <param name="maxItemsPerPage">The page size hint, or null to leave the query unbounded.</param>
    /// <returns>The rewritten Cypher query.</returns>
    public static string Apply(string cypher, int rowNumber, int? maxItemsPerPage)
    {
        ArgumentNullException.ThrowIfNull(cypher);

        var limitMatch = LimitRegex().Match(cypher);
        var skipMatch = SkipRegex().Match(cypher);

        if (skipMatch.Success)
        {
            // An explicit SKIP in the query acts as the base offset; the continuation
            // token's row count is added on top of it.
            int existingSkip = int.Parse(skipMatch.Groups[1].Value);
            int newSkip = existingSkip + rowNumber;
            cypher = SkipRegex().Replace(cypher, $"SKIP {newSkip}");
        }
        else if (limitMatch.Success && rowNumber > 0)
        {
            // The query already carries a LIMIT and we are fetching a later page: drop the
            // original clause (it may sit anywhere in the text) and re-emit SKIP + LIMIT at
            // the very end, which is the only position Cypher accepts them in.
            cypher = LimitRegex().Replace(cypher, "").TrimEnd();

            cypher += $" SKIP {rowNumber} LIMIT {limitMatch.Groups[1].Value}";
        }
        else if (rowNumber > 0)
        {
            cypher += $" SKIP {rowNumber}";
        }

        if (limitMatch.Success)
        {
            int existingLimit = int.Parse(limitMatch.Groups[1].Value);
            if (maxItemsPerPage.HasValue && maxItemsPerPage.Value < existingLimit)
            {
                cypher = LimitRegex().Replace(cypher, $"LIMIT {maxItemsPerPage.Value}");
            }
        }
        else if (maxItemsPerPage.HasValue)
        {
            cypher += $" LIMIT {maxItemsPerPage.Value}";
        }

        return cypher;
    }

    [GeneratedRegex(
        @"SKIP\s+(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex SkipRegex();

    [GeneratedRegex(
        @"LIMIT\s+(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex LimitRegex();
}
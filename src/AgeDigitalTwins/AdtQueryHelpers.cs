using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AgeDigitalTwins.Exceptions;

namespace AgeDigitalTwins;

public static partial class AdtQueryHelpers
{
    public static string ConvertAdtQueryToCypher(string adtQuery, string graphName)
    {
        // Clean up the query from line breaks and extra spaces
        adtQuery = WhitespaceTrimmerRegex().Replace(adtQuery, " ").Trim();

        // Prepare RETURN and LIMIT clauses
        string returnClause;
        var selectMatch = SelectRegex().Match(adtQuery);
        string limitClause;
        bool usesWildcard = false;
        if (selectMatch.Success)
        {
            limitClause = selectMatch.Groups["limit"].Success
                ? "LIMIT " + selectMatch.Groups["limit"].Value
                : string.Empty;
            returnClause = ProcessPropertyAccessors(
                selectMatch.Groups["projections"].Value,
                graphName
            );
            if (returnClause.Contains("COUNT()", StringComparison.OrdinalIgnoreCase))
            {
                returnClause = "COUNT(*)";
            }
            if (returnClause == "*")
            {
                usesWildcard = true;
            }
        }
        else
            throw new InvalidAdtQueryException("Invalid query format.");

        // Prepare MATCH clause
        string matchClause;
        // MultiLabel Edge WHERE clause
        List<string> multiLabelEdgeWhereClauses = new();
        if (adtQuery.Contains("FROM RELATIONSHIPS", StringComparison.OrdinalIgnoreCase))
        {
            // Handle RELATIONSHIPS source
            var match = GetRelationshipPattern().Match(adtQuery);
            if (match.Success)
            {
                var relationshipAlias = match.Groups[1].Value;
                matchClause = $"(:Twin)-[{relationshipAlias}]->(:Twin)";
            }
            else if (
                string.Equals(returnClause, "*")
                || string.Equals(returnClause, "COUNT(*)", StringComparison.OrdinalIgnoreCase)
            )
            {
                matchClause = "(:Twin)-[R]->(:Twin)";
            }
            else
                throw new InvalidAdtQueryException("Invalid query format.");
        }
        else if (adtQuery.Contains("FROM DIGITALTWINS", StringComparison.OrdinalIgnoreCase))
        {
            if (adtQuery.Contains("MATCH", StringComparison.OrdinalIgnoreCase))
            {
                // Handle MATCH clause
                var match = GetDigitalTwinsMatchRegex().Match(adtQuery);
                if (match.Success)
                {
                    var adtMatchClause = match.Groups[1].Value;

                    // Add :Twin to all round brackets in the MATCH clause
                    matchClause = Regex.Replace(adtMatchClause, @"\((\w+)\)", "($1:Twin)");

                    // AGE currently doesn't support the pipe operator
                    // See https://github.com/apache/age/issues/1714
                    // There is an open PR to support this syntax https://github.com/apache/age/pull/2082
                    // Until then we need to use a workaround and generate something like this
                    if (matchClause.Contains('|'))
                    {
                        // In the MATCH clause every multi-label edge definition should be removed and converted to a WHERE clause
                        // [R:hasBlob|hasModel] -> label(R) = 'hasBlob' OR label(R) = 'hasModel'
                        // [R:hasBlob|hasModel|has] -> (label(R) = 'hasBlob' OR label(R) = 'hasModel' OR label(R) = 'has')
                        // (n1:Twin)-[r1:hasBlob|hasModel|has]->(n2)-[r2:contains|includes]->(n3) -> clause1: (label(r1) = 'hasBlob' OR label(r1) = 'hasModel' OR label(r1) = 'has') , clause2: (label(r2) = 'contains' OR label(r2) = 'includes')
                        // This also has to work for multiple matches in the same query
                        var multiLabelEdgeMatches = MultiLabelRegex().Matches(matchClause);
                        foreach (Match multiLabelEdgeMatch in multiLabelEdgeMatches)
                        {
                            var relationshipAlias = multiLabelEdgeMatch.Groups[1].Value;
                            var labels = multiLabelEdgeMatch.Groups[2].Value.Split('|').ToList();
                            var labelConditions = labels.Select(label =>
                                $"label({relationshipAlias}) = '{label}'"
                            );
                            multiLabelEdgeWhereClauses.Add(
                                $"({string.Join(" OR ", labelConditions)})"
                            );

                            // Remove the multi-label edge definition from the match clause
                            matchClause = matchClause.Replace(
                                multiLabelEdgeMatch.Value,
                                $"[{relationshipAlias}]"
                            );
                        }
                    }
                }
                else
                    throw new InvalidAdtQueryException("Invalid query format.");
            }
            else if (adtQuery.Contains("JOIN", StringComparison.OrdinalIgnoreCase))
            {
                var joinMatches = GetJoinRelatedRegex().Matches(adtQuery);
                List<string> matchClauses = new();

                foreach (Match joinMatch in joinMatches)
                {
                    var targetTwinAlias = joinMatch.Groups[1].Value;
                    var twinAlias = joinMatch.Groups[2].Value;
                    var relationshipName = joinMatch.Groups[3].Value;
                    var relationshipAlias = joinMatch.Groups[4].Success
                        ? joinMatch.Groups[4].Value
                        : string.Empty;

                    if (string.IsNullOrEmpty(relationshipAlias))
                    {
                        matchClauses.Add(
                            $"({twinAlias}:Twin)-[:{relationshipName}]->({targetTwinAlias}:Twin)"
                        );
                    }
                    else
                    {
                        matchClauses.Add(
                            $"({twinAlias}:Twin)-[{relationshipAlias}:{relationshipName}]->({targetTwinAlias}:Twin)"
                        );
                    }
                }

                if (matchClauses.Count == 0)
                    throw new InvalidAdtQueryException("Invalid query format.");

                matchClause = string.Join(",", matchClauses);
            }
            else
            {
                var match = ExtractDigitalTwinNameRegex().Match(adtQuery);
                if (match.Success)
                {
                    var twinAlias = match.Groups[1].Value;
                    matchClause = $"({twinAlias}:Twin)";
                }
                else if (
                    string.Equals(returnClause, "*")
                    || string.Equals(returnClause, "COUNT(*)", StringComparison.OrdinalIgnoreCase)
                )
                {
                    matchClause = "(T:Twin)";
                }
                else
                    throw new InvalidAdtQueryException("Invalid query format.");
            }
        }
        else
        {
            throw new InvalidAdtQueryException("Invalid query format.");
        }

        // Prepare WHERE clause
        string whereClause = string.Empty;
        if (adtQuery.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
        {
            var match = CreateWhereClauseRegex().Match(adtQuery);
            if (match.Success)
            {
                var adtWhereClause = match.Groups[1].Value;

                // Process WHERE clause
                whereClause = ProcessPropertyAccessors(
                    adtWhereClause,
                    graphName,
                    usesWildcard
                        && adtQuery.Contains(
                            "FROM RELATIONSHIPS",
                            StringComparison.OrdinalIgnoreCase
                        )
                            ? "R"
                        : usesWildcard ? "T"
                        : null
                );
            }
            else
                throw new InvalidAdtQueryException("Invalid query format.");
        }

        // Join everything together to form the final Cypher query
        string cypher = "MATCH " + matchClause;
        if (!string.IsNullOrEmpty(whereClause))
        {
            if (multiLabelEdgeWhereClauses.Count > 0)
            {
                cypher +=
                    " WHERE "
                    + string.Join(" AND ", multiLabelEdgeWhereClauses)
                    + " AND ("
                    + whereClause
                    + ")";
                ;
            }
            else
            {
                cypher += " WHERE " + whereClause;
            }
        }
        else if (multiLabelEdgeWhereClauses.Count > 0)
        {
            cypher += " WHERE " + string.Join(" AND ", multiLabelEdgeWhereClauses);
        }
        cypher += " RETURN " + returnClause;
        if (!string.IsNullOrEmpty(limitClause))
        {
            cypher += " " + limitClause;
        }
        return cypher;
    }

    internal static string ProcessPropertyAccessors(
        string whereClause,
        string graphName,
        string? prependAlias = null
    )
    {
        if (!string.IsNullOrEmpty(prependAlias))
        {
            // Handle function calls without prepending the alias to the function name
            whereClause = PropertyAccessRegex()
                .Replace(
                    whereClause,
                    m =>
                    {
                        var functionName = m.Groups[1].Value;
                        var functionArgs = m.Groups[2].Value;

                        // Prepend alias to properties within the function arguments
                        functionArgs = FunctionArgsRegex()
                            .Replace(
                                functionArgs,
                                n =>
                                {
                                    return $"{prependAlias}.{n.Value}";
                                }
                            );

                        return $"{functionName}({functionArgs})";
                    }
                );

            // Prepend alias to properties outside of function calls
            whereClause = PropertyAccessWhereClauseRegex()
                .Replace(
                    whereClause,
                    m =>
                    {
                        return $"{prependAlias}.{m.Value}";
                    }
                );

            // Process IS_OF_MODEL function
            whereClause = IsOfModelRegex()
                .Replace(
                    whereClause,
                    m =>
                    {
                        return $"{graphName}.is_of_model({prependAlias},{m.Groups[1].Value})";
                    }
                );
        }
        else
        {
            // Process IS_OF_MODEL function without prepend alias
            whereClause = IsOfModelRegex2()
                .Replace(
                    whereClause,
                    m =>
                    {
                        return $"{graphName}.is_of_model({m.Groups[1].Value})";
                    }
                );
        }

        // Process string function STARTSWITH
        whereClause = StartsWithFunctionRegex()
            .Replace(
                whereClause,
                m =>
                {
                    return $"{m.Groups[1].Value} STARTS WITH '{m.Groups[2].Value}'";
                }
            );

        // Process string function ENDSWITH
        whereClause = EndsWithFunctionRegex()
            .Replace(
                whereClause,
                m =>
                {
                    return $"{m.Groups[1].Value} ENDS WITH '{m.Groups[2].Value}'";
                }
            );

        // Process string function CONTAINS
        whereClause = ContainsFunctionRegex()
            .Replace(
                whereClause,
                m =>
                {
                    return $"{m.Groups[1].Value} CONTAINS '{m.Groups[2].Value}'";
                }
            );

        // Process IS_NULL function
        whereClause = IsNullFunctionRegex()
            .Replace(
                whereClause,
                m =>
                {
                    return $"{m.Groups[1].Value} IS NULL";
                }
            );

        // Process IS_DEFINED function
        whereClause = IsNotNullRegex()
            .Replace(
                whereClause,
                m =>
                {
                    return $"{m.Groups[1].Value} IS NOT NULL";
                }
            );

        // TODO: Other type checks

        // Replace property access with $ character
        whereClause = DollarSignPropertyRegex().Replace(whereClause, m => $"['{m.Value[1..]}']");

        return whereClause;
    }

    [GeneratedRegex(
        @"SELECT (?:TOP\((?<limit>\d+)\) )?(?<projections>.+) FROM",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex SelectRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceTrimmerRegex();

    [GeneratedRegex(
        @"FROM RELATIONSHIPS (\w+)?(?=\s+WHERE|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex GetRelationshipPattern();

    [GeneratedRegex(
        @"FROM DIGITALTWINS MATCH (.+?)(?=\s+WHERE|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex GetDigitalTwinsMatchRegex();

    [GeneratedRegex(@"\[(\w+):([\w\|]+)\]")]
    private static partial Regex MultiLabelRegex();

    [GeneratedRegex(
        @"JOIN (\w+) RELATED (\w+)\.(\w+)(?: (\w+))?(?=\s+JOIN|\s+WHERE|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex GetJoinRelatedRegex();

    [GeneratedRegex(
        @"FROM DIGITALTWINS (\w+)?(?=\s+WHERE|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex ExtractDigitalTwinNameRegex();

    [GeneratedRegex(@"WHERE (.+)")]
    private static partial Regex CreateWhereClauseRegex();

    [GeneratedRegex(@"(\w+)\(([^)]+)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PropertyAccessRegex();

    [GeneratedRegex(
        @"(?<=\s|\[|^)(?!\d+|'[^']*'|""[^""]*"")[^\[\]""\s=<>!]+(?=\s*=\s*'|\s|$|\])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex FunctionArgsRegex();

    [GeneratedRegex(
        @"(?<=\s|\[|^)(?!AND\b|OR\b|\d+|'[^']*'|""[^""]*"")[^\[\]""\s=<>!()]+(?=\s*=\s*'|\s|$|\])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex PropertyAccessWhereClauseRegex();

    [GeneratedRegex(
        @"IS_OF_MODEL\(([^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex IsOfModelRegex();

    [GeneratedRegex(
        @"IS_OF_MODEL\(([^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex IsOfModelRegex2();

    [GeneratedRegex(
        @"STARTSWITH\(([^,]+),\s*'([^']+)'\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex StartsWithFunctionRegex();

    [GeneratedRegex(
        @"ENDSWITH\(([^,]+),\s*'([^']+)'\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex EndsWithFunctionRegex();

    [GeneratedRegex(
        @"CONTAINS\(([^,]+),\s*'([^']+)'\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex ContainsFunctionRegex();

    [GeneratedRegex(@"IS_NULL\(([^)]+)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IsNullFunctionRegex();

    [GeneratedRegex(
        @"IS_DEFINED\(([^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex IsNotNullRegex();

    [GeneratedRegex(@"(\.\$[\w]+)")]
    private static partial Regex DollarSignPropertyRegex();
}

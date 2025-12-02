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
            // Trim projections and treat empty as '*'
            var projections = selectMatch.Groups["projections"].Value.Trim();
            if (string.IsNullOrEmpty(projections))
            {
                projections = "*";
            }
            returnClause = ProcessWhereClause(projections, graphName);
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
        // Handle relationships
        if (adtQuery.Contains("FROM RELATIONSHIPS", StringComparison.OrdinalIgnoreCase))
        {
            // Find relationship collection/alias
            var match = GetRelationshipsCollectionRegex().Match(adtQuery);
            if (match.Success)
            {
                var relationshipAlias = match.Groups[1].Success ? match.Groups[1].Value : "R";
                matchClause = $"(:Twin)-[{relationshipAlias}]->(:Twin)";
            }
            else
            {
                usesWildcard = true;
                matchClause = $"(:Twin)-[R]->(:Twin)";
                // Process RETURN clause to add alias (in case return does not contain *)
                if (!returnClause.Contains('*'))
                {
                    returnClause = PropertyAccessWhereClauseRegex()
                        .Replace(
                            returnClause,
                            m =>
                            {
                                return $"R.{m.Value}";
                            }
                        );
                    returnClause = DollarSignPropertyRegex()
                        .Replace(returnClause, m => $"['{m.Value[1..]}']");
                }
            }
        }
        // Handle digital twins
        else if (adtQuery.Contains("FROM DIGITALTWINS", StringComparison.OrdinalIgnoreCase))
        {
            if (adtQuery.Contains("MATCH", StringComparison.OrdinalIgnoreCase))
            {
                // Find twin collection/alias
                var match = GetDigitalTwinsMatchClauseRegex().Match(adtQuery);
                if (match.Success)
                {
                    var adtMatchClause = match.Groups[1].Value;

                    // Add :Twin to all round brackets in the MATCH clause
                    matchClause = ParenthesesTwinRegex().Replace(adtMatchClause, "($1:Twin)");

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
                    throw new InvalidAdtQueryException($"Invalid query format: {adtQuery}");
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
                    throw new InvalidAdtQueryException($"Invalid query format: {adtQuery}");

                matchClause = string.Join(",", matchClauses);
            }
            else
            {
                // Find digitaltwins collection/alias
                var match = ExtractDigitalTwinNameRegex().Match(adtQuery);
                if (match.Success)
                {
                    matchClause = $"({match.Groups[1].Value}:Twin)";
                }
                else
                {
                    usesWildcard = true;
                    matchClause = $"(T:Twin)";
                    // Process RETURN clause to add alias (in case return does not contain *)
                    if (!returnClause.Contains('*'))
                    {
                        returnClause = PropertyAccessWhereClauseRegex()
                            .Replace(
                                returnClause,
                                m =>
                                {
                                    return $"T.{m.Value}";
                                }
                            );
                        returnClause = DollarSignPropertyRegex()
                            .Replace(returnClause, m => $"['{m.Value[1..]}']");
                    }
                }
            }
        }
        else
        {
            throw new InvalidAdtQueryException("Invalid query format: {adtQuery}");
        }

        // Prepare WHERE clause and extract IS_OF_MODEL calls for optimization
        string whereClause = string.Empty;
        List<(string modelId, string modelAlias, string idsAlias)> isOfModelClauses = new();

        if (adtQuery.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
        {
            var match = WhereClauseRegex().Match(adtQuery);
            if (match.Success)
            {
                var adtWhereClause = match.Groups[1].Value;

                // Extract all IS_OF_MODEL calls for optimization
                var isOfModelMatches = IsOfModelRegex().Matches(adtWhereClause);
                int modelIndex = 1;
                foreach (Match isOfModelMatch in isOfModelMatches)
                {
                    var args = isOfModelMatch.Groups[1].Value.Split(',');
                    string modelId;

                    // Determine if this is IS_OF_MODEL('model') or IS_OF_MODEL(T, 'model')
                    if (
                        args.Length == 1
                        || (
                            args.Length == 2
                            && args[1].Trim().Equals("exact", StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        // IS_OF_MODEL('model') or IS_OF_MODEL('model', exact)
                        modelId = args[0].Trim();
                    }
                    else
                    {
                        // IS_OF_MODEL(T, 'model') or IS_OF_MODEL(T, 'model', exact)
                        modelId = args[1].Trim();
                    }

                    // Only add if not already present
                    if (!isOfModelClauses.Any(x => x.modelId == modelId))
                    {
                        isOfModelClauses.Add((modelId, $"m{modelIndex}", $"model_ids{modelIndex}"));
                        modelIndex++;
                    }
                }

                // Process WHERE clause with IS_OF_MODEL extraction context
                whereClause = ProcessWhereClauseWithIsOfModel(
                    adtWhereClause,
                    graphName,
                    isOfModelClauses,
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
                throw new InvalidAdtQueryException("Invalid query format: {adtQuery}");
        }

        // Join everything together to form the final Cypher query
        // Add Model matches if IS_OF_MODEL is used
        if (isOfModelClauses.Count > 0)
        {
            var modelMatches = string.Join(
                ",",
                isOfModelClauses.Select(m => $"({m.modelAlias}:Model {{id: {m.modelId}}})")
            );
            matchClause = modelMatches + "," + matchClause;
        }

        string cypher = "MATCH " + matchClause;

        // Add WITH clause if IS_OF_MODEL is used
        if (isOfModelClauses.Count > 0)
        {
            var withItems = isOfModelClauses
                .Select(m => $"{m.modelAlias}.descendants as {m.idsAlias}")
                .ToList();
            // Determine twin alias from matchClause
            var twinAlias = usesWildcard ? "T" : ExtractTwinAliasFromMatchClause(matchClause);
            withItems.Add(twinAlias);
            cypher += " WITH " + string.Join(", ", withItems);
        }

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

    private static string ExtractTwinAliasFromMatchClause(string matchClause)
    {
        // Extract twin alias from patterns like (T:Twin) or (twin:Twin)
        var match = System.Text.RegularExpressions.Regex.Match(matchClause, @"\((\w+):Twin\)");
        return match.Success ? match.Groups[1].Value : "T";
    }

    internal static string ProcessWhereClauseWithIsOfModel(
        string whereClause,
        string graphName,
        List<(string modelId, string modelAlias, string idsAlias)> isOfModelClauses,
        string? prependAlias = null
    )
    {
        // Create placeholders for model_ids variables to protect them from property prepending
        // Use brackets to make them look like array access which won't be matched by PropertyAccessWhereClauseRegex
        Dictionary<string, string> idsPlaceholders = new();
        int placeholderIndex = 0;
        foreach (var modelInfo in isOfModelClauses)
        {
            var placeholder = $"['__MODELIDS_{placeholderIndex}__']";
            idsPlaceholders[placeholder] = modelInfo.idsAlias;
            placeholderIndex++;
        }

        // First process IS_OF_MODEL calls with optimization using placeholders
        int currentPlaceholder = 0;
        whereClause = IsOfModelRegex()
            .Replace(
                whereClause,
                m =>
                {
                    var args = m.Groups[1].Value.Split(',');
                    string modelId;
                    string twinAlias = prependAlias ?? "T";

                    // Determine if this is IS_OF_MODEL('model') or IS_OF_MODEL(T, 'model')
                    if (
                        args.Length == 1
                        || (
                            args.Length == 2
                            && args[1].Trim().Equals("exact", StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    {
                        // IS_OF_MODEL('model') or IS_OF_MODEL('model', exact)
                        modelId = args[0].Trim();
                    }
                    else
                    {
                        // IS_OF_MODEL(T, 'model') or IS_OF_MODEL(T, 'model', exact)
                        twinAlias = args[0].Trim();
                        modelId = args[1].Trim();
                    }

                    // Find the corresponding model alias and placeholder
                    var modelInfoIndex = isOfModelClauses.FindIndex(x => x.modelId == modelId);
                    if (modelInfoIndex >= 0)
                    {
                        var placeholder = $"['__MODELIDS_{modelInfoIndex}__']";
                        // Generate: (T['$metadata']['$model'] = 'modelId' OR T['$metadata']['$model'] IN placeholder)
                        return $"({twinAlias}['$metadata']['$model'] = {modelId} OR {twinAlias}['$metadata']['$model'] IN {placeholder})";
                    }

                    // Fallback (should not happen if extraction worked correctly)
                    return $"{graphName}.is_of_model({twinAlias},{modelId})";
                }
            );

        // Now process the rest of the WHERE clause using the original logic
        whereClause = ProcessWhereClause(whereClause, graphName, prependAlias);

        // Replace placeholders back with actual model_ids variable names
        foreach (var kvp in idsPlaceholders)
        {
            whereClause = whereClause.Replace(kvp.Key, kvp.Value);
        }

        return whereClause;
    }

    internal static string ProcessWhereClause(
        string whereClause,
        string graphName,
        string? prependAlias = null
    )
    {
        if (!string.IsNullOrEmpty(prependAlias))
        {
            // Handle function calls without prepending the alias to the function name
            whereClause = FunctionCallRegex()
                .Replace(
                    whereClause,
                    m =>
                    {
                        var functionName = m.Groups[1].Value;
                        var functionArgs = m.Groups[2].Value;

                        return $"{functionName}({functionArgs})";
                    }
                );

            // Prepend alias to properties
            whereClause = PropertyAccessWhereClauseRegex()
                .Replace(
                    whereClause,
                    m =>
                    {
                        return $"{prependAlias}.{m.Value}";
                    }
                );
        }

        // Process ARRAY_CONTAINS function
        whereClause = ArrayContainsFunctionRegex()
            .Replace(
                whereClause,
                m =>
                {
                    return $"{m.Groups[2].Value} IN {m.Groups[1].Value}";
                }
            );

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
        whereClause = IsDefinedFunctionRegex()
            .Replace(
                whereClause,
                m =>
                {
                    return $"{m.Groups[1].Value} IS NOT NULL";
                }
            );

        // Process IS_BOOL function
        whereClause = IsBoolFunctionRegex()
            .Replace(
                whereClause,
                m =>
                {
                    var property = m.Groups[1].Value;
                    return $"({property} = true OR {property} = false)";
                }
            );

        // Process IS_NUMBER function
        whereClause = IsNumberFunctionRegex()
            .Replace(whereClause, m => $"{graphName}.is_number({m.Groups[1].Value})");

        // Process IS_OBJECT function
        whereClause = IsObjectFunctionRegex()
            .Replace(whereClause, m => $"{graphName}.is_object({m.Groups[1].Value})");

        // Process IS_PRIMITIVE function
        whereClause = IsPrimitiveFunctionRegex()
            .Replace(whereClause, m => $"{graphName}.is_primitive({m.Groups[1].Value})");

        // Process IS_STRING function
        whereClause = IsStringFunctionRegex()
            .Replace(whereClause, m => $"{graphName}.is_string({m.Groups[1].Value})");

        // Replace property access with $ character
        whereClause = DollarSignPropertyRegex().Replace(whereClause, m => $"['{m.Value[1..]}']");

        // Process != operator
        whereClause = InequalityOperatorRegex()
            .Replace(
                whereClause,
                m =>
                {
                    var operand1 = m.Groups["operand1"].Value;
                    var operand2 = m.Groups["operand2"].Value;
                    return $"NOT ({operand1} = {operand2})";
                }
            );

        return whereClause;
    }

    [GeneratedRegex(
        @"SELECT\s*(?:TOP\s*\(\s*(?<limit>\d+)\s*\)\s*)?(?<projections>.*?)\s+FROM",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex SelectRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceTrimmerRegex();

    [GeneratedRegex(
        @"FROM RELATIONSHIPS (\w+)?(?=\s+WHERE|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex GetRelationshipsCollectionRegex();

    [GeneratedRegex(
        @"FROM DIGITALTWINS MATCH (.+?)(?=\s+WHERE|\s*$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex GetDigitalTwinsMatchClauseRegex();

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
    private static partial Regex WhereClauseRegex();

    [GeneratedRegex(@"(\w+)\(([^)]+)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FunctionCallRegex();

    [GeneratedRegex(
        @"(?<=\s|\[|\(|^)(?!AND\b|OR\b|IN\b|NOT\b|\d+|'[^']*'|""[^""]*"")[^\[\]""\s=<>!()]+(?=\s*=\s*'|\s|\)|$|\])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex PropertyAccessWhereClauseRegex();

    [GeneratedRegex(
        @"IS_OF_MODEL\(([^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex IsOfModelRegex();

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

    [GeneratedRegex(
        @"ARRAY_CONTAINS\(([^,]+),\s*([^\)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex ArrayContainsFunctionRegex();

    [GeneratedRegex(@"IS_NULL\(([^)]+)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IsNullFunctionRegex();

    [GeneratedRegex(
        @"IS_DEFINED\(([^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex IsDefinedFunctionRegex();

    [GeneratedRegex(
        @"IS_NUMBER\(([^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex IsNumberFunctionRegex();

    [GeneratedRegex(@"IS_BOOL\(([^)]+)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IsBoolFunctionRegex();

    [GeneratedRegex(@"(\.\$[\w]+)")]
    private static partial Regex DollarSignPropertyRegex();

    [GeneratedRegex(@"\((\w+)\)")]
    private static partial Regex ParenthesesTwinRegex();

    [GeneratedRegex("(?<operand1>[^\\s]+)\\s*!=\\s*(?<operand2>[^\\s]+)")]
    private static partial Regex InequalityOperatorRegex();

    [GeneratedRegex(
        @"IS_PRIMITIVE\(([^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex IsPrimitiveFunctionRegex();

    [GeneratedRegex(
        @"IS_STRING\(([^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex IsStringFunctionRegex();

    [GeneratedRegex(
        @"IS_OBJECT\(([^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex IsObjectFunctionRegex();
}

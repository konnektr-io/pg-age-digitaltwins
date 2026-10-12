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

        // Extract a trailing ORDER BY clause before anything else. The WHERE clause
        // regex is greedy, so it would otherwise swallow the clause into the predicate
        // ("... ORDER BY order" became "T.ORDER T.BY T.order"), and ADT writes ORDER BY
        // after the projection while Cypher requires it after RETURN.
        string? adtOrderByClause = null;
        var orderByMatch = OrderByRegex().Match(adtQuery);
        if (orderByMatch.Success)
        {
            adtOrderByClause = orderByMatch.Groups["orderby"].Value.Trim();
            adtQuery = adtQuery[..orderByMatch.Index].TrimEnd();
        }

        // Prepare RETURN and LIMIT clauses
        string returnClause;
        var selectMatch = SelectRegex().Match(adtQuery);
        string limitClause;
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
            var countAsAliasMatch = CountAsAliasRegex().Match(returnClause);
            if (countAsAliasMatch.Success)
            {
                returnClause = $"COUNT(*) AS {countAsAliasMatch.Groups[1].Value}";
            }
            else if (CountCallRegex().IsMatch(returnClause))
            {
                returnClause = "COUNT(*)";
            }
        }
        else
            throw new InvalidAdtQueryException("Invalid query format.");

        // Prepare MATCH clause
        string matchClause;
        // The twin/relationship variable the WHERE clause and ORDER BY refer to, or null
        // when the query names its own variable(s) in a MATCH/JOIN and the projection is
        // explicit (then the caller qualifies every property itself).
        string? implicitAlias = null;
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
                implicitAlias = relationshipAlias;
                matchClause = $"(:Twin)-[{relationshipAlias}]->(:Twin)";
            }
            else
            {
                implicitAlias = "R";
                matchClause = $"(:Twin)-[R]->(:Twin)";
                // Process RETURN clause to add alias (in case return does not contain *)
                if (!returnClause.Contains('*'))
                {
                    returnClause = PrependAliasToProjections(returnClause, "R");
                }
            }
        }
        // Handle digital twins
        else if (adtQuery.Contains("FROM DIGITALTWINS", StringComparison.OrdinalIgnoreCase))
        {
            if (adtQuery.Contains("MATCH", StringComparison.OrdinalIgnoreCase))
            {
                // With an explicit MATCH the query names its own twin variables, so only a
                // wildcard projection leaves a single implicit variable to qualify against.
                implicitAlias = returnClause == "*" ? "T" : null;
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
                implicitAlias = returnClause == "*" ? "T" : null;
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
                var twinAlias = match.Groups[1].Success ? match.Groups[1].Value : string.Empty;
                if (!string.IsNullOrEmpty(twinAlias))
                {
                    // The query names its twin variable, so the WHERE clause and ORDER BY
                    // refer to that variable (and not to the fallback "T").
                    implicitAlias = twinAlias;
                    matchClause = $"({twinAlias}:Twin)";
                    // Process RETURN clause to add the (user-supplied) alias so
                    // bare $-property projections like $metadata.$model are
                    // correctly prefixed (e.g. T['$metadata']['$model']).
                    if (!returnClause.Contains('*'))
                    {
                        returnClause = PrependAliasToProjections(returnClause, twinAlias);
                    }
                }
                else
                {
                    implicitAlias = "T";
                    matchClause = $"(T:Twin)";
                    // Process RETURN clause to add alias (in case return does not contain *)
                    if (!returnClause.Contains('*'))
                    {
                        returnClause = PrependAliasToProjections(returnClause, "T");
                    }
                }
            }
        }
        else
        {
            throw new InvalidAdtQueryException("Invalid query format: {adtQuery}");
        }

        // Prepare WHERE clause
        string whereClause = string.Empty;
        if (adtQuery.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
        {
            var match = WhereClauseRegex().Match(adtQuery);
            if (match.Success)
            {
                var adtWhereClause = match.Groups[1].Value;

                // Process WHERE clause
                whereClause = ProcessWhereClause(adtWhereClause, graphName, implicitAlias);
            }
            else
                throw new InvalidAdtQueryException("Invalid query format: {adtQuery}");
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
        if (!string.IsNullOrEmpty(adtOrderByClause))
        {
            cypher +=
                " ORDER BY " + TranslateOrderByClause(adtOrderByClause, graphName, implicitAlias);
        }
        if (!string.IsNullOrEmpty(limitClause))
        {
            cypher += " " + limitClause;
        }
        return cypher;
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
                        // Leave accesses that already name their own variable alone
                        // (T.$dtId, DT.name, current.$dtId): the caller wrote them
                        // explicitly, and prefixing again yields T.T['$dtId'].
                        if (QualifiedPropertyAccessRegex().IsMatch(m.Value))
                        {
                            return m.Value;
                        }
                        return $"{prependAlias}.{m.Value}";
                    }
                );
        }

        // Process IS_OF_MODEL. This runs after the alias-prepend pass above, which may have
        // qualified the call's arguments (a bare `exact` becomes T.exact, a twin collection
        // reference becomes T.T); TranslateIsOfModelCall undoes that, so every ADT form maps
        // onto the graph function:
        //   IS_OF_MODEL('model')           -> is_of_model(<twin>,'model')
        //   IS_OF_MODEL('model', exact)    -> is_of_model(<twin>,'model',true)
        //   IS_OF_MODEL(T, 'model')        -> is_of_model(T,'model')      [issue #118]
        //   IS_OF_MODEL(T, 'model', exact) -> is_of_model(T,'model',true)
        whereClause = IsOfModelRegex()
            .Replace(whereClause, m => TranslateIsOfModelCall(m, graphName, prependAlias));

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
        whereClause = DollarSignPropertyRegex()
            .Replace(
                whereClause,
                m =>
                    m.Value.StartsWith('.')
                        ? $"['{m.Value[1..]}']"
                        : $"['{m.Value}']"
            );

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

    /// <summary>
    /// Translates an ADT <c>IS_OF_MODEL</c> call into the graph's <c>is_of_model</c> SQL function.
    /// </summary>
    /// <remarks>
    /// ADT accepts <c>IS_OF_MODEL([twin-collection,] '&lt;model-ID&gt;' [, exact])</c> — every
    /// argument is optional except the model id. The twin collection only disambiguates which twin
    /// to test when the query has more than one (a JOIN/MATCH), so when the query leaves the twin
    /// variable implicit <paramref name="prependAlias"/> wins.
    /// </remarks>
    private static string TranslateIsOfModelCall(
        Match match,
        string graphName,
        string? prependAlias
    )
    {
        var rawArgs = match
            .Groups[1]
            .Value.Split(',')
            // The alias-prepend pass qualifies bare references (T -> T.T); undo that so the
            // arguments are parsed exactly as the caller wrote them.
            .Select(arg => arg.Trim())
            .Select(arg =>
                prependAlias != null
                && arg.StartsWith(prependAlias + ".", StringComparison.Ordinal)
                    ? arg[(prependAlias.Length + 1)..]
                    : arg
            )
            .ToArray();
        var exact = rawArgs.Any(arg => IsExactArgument(arg));
        var operands = rawArgs.Where(arg => arg.Length > 0 && !IsExactArgument(arg)).ToList();

        string? modelId;
        string? twinRef;
        var quoted = operands.Where(operand => operand.StartsWith('\'')).ToList();
        if (quoted.Count > 0)
        {
            // Model ids are always quoted string literals; anything unquoted next to one is
            // the twin collection reference.
            modelId = quoted[0];
            twinRef = operands.FirstOrDefault(operand => !operand.StartsWith('\''));
        }
        else if (operands.Count > 1)
        {
            // Legacy/unquoted form: <twin-collection>, <model-id>
            twinRef = operands[0];
            modelId = operands[^1];
        }
        else
        {
            modelId = operands.FirstOrDefault();
            twinRef = null;
        }

        if (modelId == null)
        {
            // Nothing to translate — leave the call as it is and let the graph report it.
            return match.Value;
        }

        // An explicit twin collection argument wins: that is the whole point of ADT's optional
        // first argument (it disambiguates which twin to test in a JOIN/MATCH).
        var twin = twinRef ?? prependAlias;
        if (twin == null)
        {
            throw new InvalidAdtQueryException(
                "IS_OF_MODEL requires a twin collection when the query names its own twin "
                    + "variable(s), e.g. IS_OF_MODEL(T, 'dtmi:com:example:Thing;1')."
            );
        }

        return exact
            ? $"{graphName}.is_of_model({twin},{modelId},true)"
            : $"{graphName}.is_of_model({twin},{modelId})";
    }

    /// <summary>
    /// True when an <c>IS_OF_MODEL</c> argument is the ADT <c>exact</c> keyword.
    /// </summary>
    /// <remarks>
    /// The alias-prepend pass may already have qualified a bare <c>exact</c> into <c>T.exact</c>
    /// (that is what the translation used to work around), so both shapes are accepted.
    /// </remarks>
    private static bool IsExactArgument(string argument) =>
        argument.Equals("exact", StringComparison.OrdinalIgnoreCase)
        || argument.EndsWith(".exact", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Translates an ADT <c>ORDER BY</c> expression (e.g. <c>name DESC, $dtId</c>) into Cypher.
    /// </summary>
    /// <remarks>
    /// Each term is translated separately, with the <c>ASC</c>/<c>DESC</c> direction keyword split
    /// off first so it is not treated as a property, and joined back with commas.
    /// </remarks>
    private static string TranslateOrderByClause(
        string orderByClause,
        string graphName,
        string? prependAlias
    )
    {
        var terms = orderByClause.Split(',');
        for (int i = 0; i < terms.Length; i++)
        {
            var term = terms[i].Trim();
            var direction = string.Empty;
            var directionMatch = OrderByDirectionRegex().Match(term);
            if (directionMatch.Success)
            {
                term = directionMatch.Groups["expression"].Value.Trim();
                direction = " " + directionMatch.Groups["direction"].Value.ToUpperInvariant();
            }

            terms[i] = TranslateOrderByTerm(term, graphName, prependAlias) + direction;
        }

        return string.Join(", ", terms);
    }

    /// <summary>
    /// Translates one ORDER BY term, using bracket notation for the property (<c>T['order']</c>).
    /// </summary>
    /// <remarks>
    /// Bracket notation is not cosmetic here. AGE's parser reads a dotted property whose name is a
    /// Cypher keyword — a twin property called <c>order</c>, for instance — as the start of the
    /// clause itself and then fails on the following token: <c>ORDER BY T.order</c> is a syntax
    /// error, while <c>ORDER BY T['order']</c> sorts. It is the same escape the converter already
    /// uses for <c>$</c>-properties.
    /// </remarks>
    private static string TranslateOrderByTerm(string term, string graphName, string? prependAlias)
    {
        var simple = SimplePropertyAccessRegex().Match(term);
        if (simple.Success)
        {
            var alias = simple.Groups["alias"].Success
                ? simple.Groups["alias"].Value
                : prependAlias;
            if (alias != null)
            {
                return $"{alias}['{simple.Groups["property"].Value}']";
            }
        }

        // $-properties and anything else go through the regular translation.
        return ProcessWhereClause(term, graphName, prependAlias);
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
        @"(?<=\s|\[|\(|^)(?!AND\b|OR\b|IN\b|NOT\b|true\b|false\b|-?\d+|'[^']*'|""[^""]*"")[^\[\]""\s=<>!()]+(?=\s*=\s*'|\s|\)|$|\])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex PropertyAccessWhereClauseRegex();

    /// <summary>
    /// A property access that already names its own variable (<c>T.$dtId</c>, <c>DT.name</c>,
    /// <c>current.$dtId</c>) and therefore must not be prefixed with the implicit alias again.
    /// </summary>
    [GeneratedRegex(@"^\w+[.\[]", RegexOptions.CultureInvariant)]
    private static partial Regex QualifiedPropertyAccessRegex();

    /// <summary>A trailing ADT <c>ORDER BY</c> clause, captured without the leading keyword.</summary>
    [GeneratedRegex(
        @"\bORDER\s+BY\s+(?<orderby>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex OrderByRegex();

    /// <summary>A single ORDER BY term with an explicit sort direction.</summary>
    [GeneratedRegex(
        @"^(?<expression>.+?)\s+(?<direction>ASC|DESC)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex OrderByDirectionRegex();

    /// <summary>
    /// An ORDER BY term that is a single property access: <c>name</c>, <c>T.name</c> (not
    /// <c>$</c>-properties, which are translated by the regular property path).
    /// </summary>
    [GeneratedRegex(
        @"^(?:(?<alias>\w+)\.)?(?<property>[A-Za-z_]\w*)$",
        RegexOptions.CultureInvariant
    )]
    private static partial Regex SimplePropertyAccessRegex();

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

    [GeneratedRegex(
        @"(\.\$[\w]+|(?<![\w.\['])\$[\w]+)",
        RegexOptions.CultureInvariant
    )]
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

    private static string PrependAliasToProjections(string projections, string alias)
    {
        if (string.IsNullOrEmpty(projections))
            return projections;

        var parts = projections.Split(',');
        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            if (string.IsNullOrEmpty(part))
                continue;

            string? columnAlias = null;
            var asMatch = AsAliasRegex().Match(part);
            if (asMatch.Success)
            {
                part = asMatch.Groups[1].Value.Trim();
                columnAlias = asMatch.Groups[2].Value;
            }

            // The projection is exactly the alias (e.g. SELECT T FROM DIGITALTWINS T)
            // — it refers to the whole node/edge, so leave it untouched.
            if (part.Equals(alias, StringComparison.OrdinalIgnoreCase))
            {
                parts[i] = columnAlias != null ? $"{part} AS {columnAlias}" : part;
                continue;
            }

            // The projection is already alias-qualified (e.g. SELECT T.name ...)
            // — do not prepend the alias again.
            if (
                part.StartsWith(alias + ".", StringComparison.OrdinalIgnoreCase)
                || part.StartsWith(alias + "[", StringComparison.OrdinalIgnoreCase)
            )
            {
                parts[i] = columnAlias != null ? $"{part} AS {columnAlias}" : part;
                continue;
            }

            part = PropertyAccessWhereClauseRegex().Replace(part, m => $"{alias}.{m.Value}");
            part = DollarSignPropertyRegex()
                .Replace(
                    part,
                    m =>
                        m.Value.StartsWith('.')
                            ? $"['{m.Value[1..]}']"
                            : $"['{m.Value}']"
                );

            // A leading $-property that was already converted to bracket
            // notation (e.g. ['$metadata']['$model']) still needs the
            // collection alias prefix (the same alias passed in).
            if (part.StartsWith("[", StringComparison.Ordinal))
            {
                part = $"{alias}{part}";
            }

            parts[i] = columnAlias != null ? $"{part} AS {columnAlias}" : part;
        }
        return string.Join(", ", parts);
    }

    [GeneratedRegex(
        @"^(.+?)\s+AS\s+(\w+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex AsAliasRegex();

    [GeneratedRegex(
        @"COUNT\s*\(\s*\)\s+AS\s+(\w+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex CountAsAliasRegex();

    [GeneratedRegex(
        @"COUNT\s*\(\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex CountCallRegex();
}

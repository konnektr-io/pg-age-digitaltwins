using System;
using System.Text.RegularExpressions;
using AgeDigitalTwins.Exceptions;

namespace AgeDigitalTwins;

public static class AdtQueryHelpers
{
    internal static string ConvertAdtQueryToCypher(string adtQuery)
    {

        // Prepare RETURN and LIMIT clauses
        string returnClause;
        var selectMatch = Regex.Match(adtQuery, @"SELECT (?:TOP\((?<limit>\d+)\) )?(?<projections>.+) FROM", RegexOptions.IgnoreCase);
        string limitClause;
        if (selectMatch.Success)
        {
            limitClause = selectMatch.Groups["limit"].Success ? "LIMIT " + selectMatch.Groups["limit"].Value : string.Empty;
            returnClause = ProcessPropertyAccessors(selectMatch.Groups["projections"].Value);
            if (returnClause.Contains("COUNT()", StringComparison.OrdinalIgnoreCase))
            {
                returnClause = "COUNT(*)";
            }
        }
        else throw new InvalidAdtQueryException("Invalid query format.");

        // Prepare MATCH clause
        string matchClause;
        if (adtQuery.Contains("FROM RELATIONSHIPS", StringComparison.OrdinalIgnoreCase))
        {
            // Handle RELATIONSHIPS source
            var match = Regex.Match(adtQuery, @"FROM RELATIONSHIPS (.+?)(?=\s+WHERE|\s*$)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var relationshipAlias = match.Groups[1].Value;
                matchClause = $"(:Twin)-[{relationshipAlias}]->(:Twin)";
            }
            else throw new InvalidAdtQueryException("Invalid query format.");
        }
        else if (adtQuery.Contains("FROM DIGITALTWINS", StringComparison.OrdinalIgnoreCase))
        {
            if (adtQuery.Contains("MATCH", StringComparison.OrdinalIgnoreCase))
            {
                // Handle MATCH clause
                var match = Regex.Match(adtQuery, @"FROM DIGITALTWINS MATCH (.+?)(?=\s+WHERE|\s*$)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var adtMatchClause = match.Groups[1].Value;

                    // Add :Twin to all round brackets in the MATCH clause
                    matchClause = Regex.Replace(adtMatchClause, @"\((\w+)\)", "($1:Twin)");
                }
                else throw new InvalidAdtQueryException("Invalid query format.");
            }
            // TODO: JOIN RELATED
            else
            {
                var match = Regex.Match(adtQuery, @"FROM DIGITALTWINS (.+?)(?=\s+WHERE|\s*$)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var twinAlias = match.Groups[1].Value;
                    matchClause = $"({twinAlias}:Twin)";
                }
                // TODO: Support SELECT * FROM DIGITALTWINS queries
                /* else if (string.Equals(returnClause, "*"))
                {
                    matchClause = "(return:Twin)";
                } */
                else throw new InvalidAdtQueryException("Invalid query format.");
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
            var match = Regex.Match(adtQuery, @"WHERE (.+)");
            if (match.Success)
            {
                var adtWhereClause = match.Groups[1].Value;

                // Process WHERE clause
                whereClause = ProcessPropertyAccessors(adtWhereClause);
            }
            else throw new InvalidAdtQueryException("Invalid query format.");
        }

        string cypher = "MATCH " + matchClause;
        if (!string.IsNullOrEmpty(whereClause))
        {
            cypher += " WHERE " + whereClause;
        }
        cypher += " RETURN " + returnClause;
        if (!string.IsNullOrEmpty(limitClause))
        {
            cypher += " " + limitClause;
        }
        return cypher;
    }

    internal static string ProcessPropertyAccessors(string whereClause)
    {
        // Replace property access with $ character
        return Regex.Replace(whereClause, @"(\.\$[\w]+)", m => $"['{m.Value[1..]}']");
    }
}
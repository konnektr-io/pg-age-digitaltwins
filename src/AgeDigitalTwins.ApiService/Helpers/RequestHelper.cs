using System.Text.Json;
using Microsoft.Extensions.Primitives;

namespace AgeDigitalTwins.ApiService.Helpers;

public static class RequestHelper
{
    private const int DefaultMaxItemsPerPage = 2000;

    /// <summary>
    /// Parses the max-items-per-page value from HTTP headers.
    /// </summary>
    /// <param name="httpContext">The HTTP context containing headers.</param>
    /// <returns>The max items per page value, or the default if not specified.</returns>
    public static int? ParseMaxItemsPerPage(HttpContext httpContext)
    {
        if (httpContext.Request.Headers.TryGetValue("max-items-per-page", out var maxItemsHeader))
        {
            if (int.TryParse(maxItemsHeader, out var maxItems))
            {
                return maxItems;
            }
        }
        return DefaultMaxItemsPerPage;
    }

    /// <summary>
    /// Parses pagination parameters from HTTP headers and query string.
    /// </summary>
    /// <param name="httpContext">The HTTP context containing headers and query parameters.</param>
    /// <returns>A tuple containing maxItemsPerPage and continuationToken.</returns>
    public static (int? maxItemsPerPage, string? continuationToken) ParsePagination(
        HttpContext httpContext
    )
    {
        int? maxItemsPerPage = ParseMaxItemsPerPage(httpContext);

        string? continuationToken = null;
        if (
            httpContext.Request.Query.TryGetValue(
                "continuationToken",
                out var continuationTokenStringValues
            )
        )
        {
            continuationToken = continuationTokenStringValues.ToString();
        }

        return (maxItemsPerPage, continuationToken);
    }

    /// <summary>
    /// Parses pagination parameters for query endpoints where continuation token comes from request body.
    /// </summary>
    /// <param name="httpContext">The HTTP context containing headers.</param>
    /// <param name="requestBody">The JSON request body containing the continuation token.</param>
    /// <returns>A tuple containing maxItemsPerPage and continuationToken.</returns>
    public static (int? maxItemsPerPage, string? continuationToken) ParsePaginationFromBody(
        HttpContext httpContext,
        JsonElement requestBody
    )
    {
        int? maxItemsPerPage = ParseMaxItemsPerPage(httpContext);

        string? continuationToken = null;
        if (
            requestBody.TryGetProperty(
                "continuationToken",
                out JsonElement continuationTokenElement
            )
            && continuationTokenElement.ValueKind == JsonValueKind.String
        )
        {
            continuationToken = continuationTokenElement.GetString();
        }

        return (maxItemsPerPage, continuationToken);
    }

    /// <summary>
    /// Parses ETag from HTTP headers.
    /// </summary>
    /// <param name="httpContext">The HTTP context containing headers.</param>
    /// <param name="headerName">The name of the ETag header (e.g., "If-Match", "If-None-Match").</param>
    /// <returns>The ETag value or null if not found.</returns>
    public static string? ParseETag(HttpContext httpContext, string headerName)
    {
        if (
            httpContext.Request.Headers.TryGetValue(headerName, out StringValues etagValues)
            && etagValues.Count > 0
        )
        {
            return etagValues[0];
        }
        return null;
    }
}

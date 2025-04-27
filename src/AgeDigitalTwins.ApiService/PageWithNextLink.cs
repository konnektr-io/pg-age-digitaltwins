using System;
using System.Collections.Generic;
using AgeDigitalTwins.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace AgeDigitalTwins.ApiService.Models;

public class PageWithNextLink<T> : Page<T>
{
    public Uri? NextLink { get; set; }

    public PageWithNextLink(Page<T> page, HttpRequest request)
    {
        Values = page.Values;
        ContinuationToken = page.ContinuationToken;

        if (page.ContinuationToken == null)
        {
            return;
        }

        // Create a new URI with the continuation token as a query parameter
        var uriBuilder = new UriBuilder(
            request.Scheme,
            request.Host.Host,
            request.Host.Port ?? -1,
            request.Path
        );
        var query = QueryHelpers.ParseQuery(request.QueryString.ToString());
        query["continuationToken"] = page.ContinuationToken.ToString();
        uriBuilder.Query = string.Join(
            "&",
            request.Query.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value!)}")
        );

        NextLink = uriBuilder.Uri;
    }
}

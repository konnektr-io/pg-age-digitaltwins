using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AgeDigitalTwins.Models;

/// <summary>
/// A custom implementation of IAsyncEnumerable to support pagination.
/// </summary>
/// <typeparam name="T">The type of the values.</typeparam>
public class AsyncPageable<T>(
    Func<
        ContinuationToken?,
        int?,
        CancellationToken,
        Task<(IEnumerable<T> Items, ContinuationToken? ContinuationToken)>
    > fetchPage
) : IAsyncEnumerable<T>
{
    private readonly Func<
        ContinuationToken?,
        int?,
        CancellationToken,
        Task<(IEnumerable<T> Items, ContinuationToken? ContinuationToken)>
    > _fetchPage = fetchPage;

    private const int DefaultPageSize = 2000;

    public async IAsyncEnumerable<Page<T>> AsPages(
        string? continuationToken = null,
        int? pageSizeHint = DefaultPageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        ContinuationToken? token =
            continuationToken != null ? ContinuationToken.Deserialize(continuationToken) : null;

        do
        {
            var (items, nextContinuationToken) = await _fetchPage(
                token,
                pageSizeHint,
                cancellationToken
            );

            yield return new Page<T>
            {
                ContinuationToken = nextContinuationToken?.ToString(),
                Value = items,
            };

            token = nextContinuationToken;
        } while (continuationToken != null);
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(
        CancellationToken cancellationToken = default
    )
    {
        await foreach (Page<T> page in AsPages(cancellationToken: cancellationToken))
        {
            foreach (T item in page.Value)
            {
                yield return item;
            }
        }
    }
}

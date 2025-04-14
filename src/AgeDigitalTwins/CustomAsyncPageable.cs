using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AgeDigitalTwins;

public class Page<T>
{
    public string? ContinuationToken { get; set; }
    public IEnumerable<T> Values { get; set; } = [];
}

/// <summary>
/// A custom implementation of IAsyncEnumerable to support pagination.
/// </summary>
/// <typeparam name="T">The type of the values.</typeparam>
public class CustomAsyncPageable<T> : IAsyncEnumerable<T>
{
    private readonly Func<
        string?,
        int?,
        CancellationToken,
        Task<(IEnumerable<T> Items, string? ContinuationToken)>
    > _fetchPage;

    public CustomAsyncPageable(
        Func<
            string?,
            int?,
            CancellationToken,
            Task<(IEnumerable<T> Items, string? ContinuationToken)>
        > fetchPage
    )
    {
        _fetchPage = fetchPage;
    }

    public async IAsyncEnumerable<Page<T>> AsPages(
        string? continuationToken = default,
        int? pageSizeHint = default,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        do
        {
            var (items, nextContinuationToken) = await _fetchPage(
                continuationToken,
                pageSizeHint,
                cancellationToken
            );

            yield return new Page<T> { ContinuationToken = nextContinuationToken, Values = items };

            continuationToken = nextContinuationToken;
        } while (continuationToken != null);
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(
        CancellationToken cancellationToken = default
    )
    {
        await foreach (Page<T> page in AsPages(cancellationToken: cancellationToken))
        {
            foreach (T value in page.Values)
            {
                yield return value;
            }
        }
    }
}

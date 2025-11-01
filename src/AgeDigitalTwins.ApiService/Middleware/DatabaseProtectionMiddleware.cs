using System.Collections.Concurrent;
using System.Diagnostics;

namespace AgeDigitalTwins.ApiService.Middleware;

/// <summary>
/// Middleware to protect the database from overload by monitoring connection usage and request patterns.
/// </summary>
public class DatabaseProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DatabaseProtectionMiddleware> _logger;
    private readonly DatabaseProtectionOptions _options;
    private readonly ConcurrentDictionary<string, RequestMetrics> _userMetrics = new();
    private readonly Timer _cleanupTimer;

    public DatabaseProtectionMiddleware(
        RequestDelegate next,
        ILogger<DatabaseProtectionMiddleware> logger,
        IConfiguration configuration
    )
    {
        _next = next;
        _logger = logger;
        _options =
            configuration.GetSection("Parameters").Get<DatabaseProtectionOptions>()
            ?? new DatabaseProtectionOptions();

        // Clean up old metrics every minute
        _cleanupTimer = new Timer(
            CleanupOldMetrics,
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1)
        );
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = GetUserId(context);
        var metrics = _userMetrics.GetOrAdd(userId, _ => new RequestMetrics());

        // Check if user has too many concurrent requests
        if (metrics.ConcurrentRequests >= _options.MaxConcurrentRequestsPerUser)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    error = "Too many concurrent requests",
                    message = $"Maximum {_options.MaxConcurrentRequestsPerUser} concurrent requests allowed per user",
                }
            );
            return;
        }

        // Check if user has exceeded query complexity in the time window
        if (
            IsQueryEndpoint(context)
            && metrics.QueryComplexityScore > _options.MaxQueryComplexityPerWindow
        )
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(
                new
                {
                    error = "Query Units limit exceeded",
                    message = "Please reduce the complexity of your queries or wait before making new requests",
                    hint = $"Limit is {_options.MaxQueryComplexityPerWindow} Query Units per {_options.QueryComplexityWindowMinutes} minutes",
                }
            );
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            Interlocked.Increment(ref metrics.ConcurrentRequests);

            // Add complexity score for query operations
            if (IsQueryEndpoint(context))
            {
                metrics.AddQueryComplexity(_options.BaseQueryComplexity);
            }

            await _next(context);
        }
        finally
        {
            Interlocked.Decrement(ref metrics.ConcurrentRequests);
            stopwatch.Stop();

            // Log slow operations
            if (stopwatch.ElapsedMilliseconds > _options.SlowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "Slow database operation detected: {Path} took {ElapsedMs}ms for user {UserId}",
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds,
                    userId
                );
            }
        }
    }

    private static string GetUserId(HttpContext context)
    {
        return context.User?.Identity?.Name
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
    }

    private static bool IsQueryEndpoint(HttpContext context)
    {
        return context.Request.Path.StartsWithSegments("/query");
    }

    private void CleanupOldMetrics(object? state)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-_options.MetricsRetentionMinutes);
        var keysToRemove = new List<string>();

        foreach (var kvp in _userMetrics)
        {
            kvp.Value.CleanupOldQueries(cutoff);

            // Remove metrics for users who haven't made requests recently
            if (kvp.Value.LastRequestTime < cutoff)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _userMetrics.TryRemove(key, out _);
        }
    }
}

/// <summary>
/// Configuration options for database protection middleware.
/// </summary>
public class DatabaseProtectionOptions
{
    public int MaxConcurrentRequestsPerUser { get; set; } = 20;
    public int MaxQueryComplexityPerWindow { get; set; } = 1000;
    public int BaseQueryComplexity { get; set; } = 10;
    public int SlowRequestThresholdMs { get; set; } = 5000;
    public int MetricsRetentionMinutes { get; set; } = 10;
    public int QueryComplexityWindowMinutes { get; set; } = 1;
}

/// <summary>
/// Tracks request metrics for a user/IP address.
/// </summary>
public class RequestMetrics
{
    private readonly Lock _lock = new();
    private readonly List<DateTime> _queryTimes = [];

    public int ConcurrentRequests;
    public DateTime LastRequestTime = DateTime.UtcNow;

    public int QueryComplexityScore
    {
        get
        {
            lock (_lock)
            {
                return _queryTimes.Count;
            }
        }
    }

    public void AddQueryComplexity(int complexity)
    {
        lock (_lock)
        {
            LastRequestTime = DateTime.UtcNow;
            for (int i = 0; i < complexity; i++)
            {
                _queryTimes.Add(DateTime.UtcNow);
            }
        }
    }

    public void CleanupOldQueries(DateTime cutoff)
    {
        lock (_lock)
        {
            _queryTimes.RemoveAll(time => time < cutoff);
        }
    }
}

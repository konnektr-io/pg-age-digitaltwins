using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AgeDigitalTwins.ApiService.Configuration;

/// <summary>
/// Configuration for rate limiting policies to protect the API and database from overload.
/// </summary>
public static class RateLimitingConfiguration
{
    /// <summary>
    /// Configures all rate limiting policies for the application.
    /// </summary>
    /// <param name="options">The rate limiter options to configure</param>
    /// <param name="configuration">Application configuration for reading rate limit settings</param>
    public static void ConfigureRateLimiting(
        this RateLimiterOptions options,
        IConfiguration configuration
    )
    {
        // Global rate limit - applies to all endpoints
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var userId = GetUserIdentifier(context);

            return RateLimitPartition.GetFixedWindowLimiter(
                userId,
                partition => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue<int>(
                        "Parameters:GlobalPermitLimit",
                        10000
                    ),
                    Window = TimeSpan.FromMinutes(
                        configuration.GetValue<int>("Parameters:GlobalWindowMinutes", 1)
                    ),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue<int>("Parameters:GlobalQueueLimit", 2000),
                }
            );
        });

        // Jobs API Policy
        options.AddPolicy("JobsApi", CreateJobsApiPolicy(configuration));

        // Models API Policy
        options.AddPolicy("ModelsApi", CreateModelsApiPolicy(configuration));

        // Digital Twins API Read Policy
        options.AddPolicy("DigitalTwinsApiRead", CreateDigitalTwinsApiReadPolicy(configuration));

        // Digital Twins API Write Policy
        options.AddPolicy("DigitalTwinsApiWrite", CreateDigitalTwinsApiWritePolicy(configuration));

        // Digital Twins API Create/Delete Policy
        options.AddPolicy(
            "DigitalTwinsApiCreateDelete",
            CreateDigitalTwinsApiCreateDeletePolicy(configuration)
        );

        // Digital Twins API Single Twin Policy
        options.AddPolicy(
            "DigitalTwinsApiSingleTwin",
            CreateDigitalTwinsApiSingleTwinPolicy(configuration)
        );

        // Query API Policy
        options.AddPolicy("QueryApi", CreateQueryApiPolicy(configuration));

        // Custom rejection response
        options.OnRejected = CreateRejectionHandler();
    }

    /// <summary>
    /// Gets a consistent user identifier for rate limiting partitioning.
    /// Uses authenticated user name if available, otherwise falls back to IP address.
    /// </summary>
    private static string GetUserIdentifier(HttpContext context)
    {
        return context.User?.Identity?.Name
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
    }

    /// <summary>
    /// Creates the Jobs API rate limiting policy.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> CreateJobsApiPolicy(
        IConfiguration configuration
    )
    {
        return context =>
        {
            var userId = GetUserIdentifier(context);

            return RateLimitPartition.GetFixedWindowLimiter(
                userId,
                partition => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue<int>("Parameters:JobsApiPermitLimit", 10),
                    Window = TimeSpan.FromSeconds(
                        configuration.GetValue<int>("Parameters:JobsApiWindowSeconds", 1)
                    ),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue<int>("Parameters:JobsApiQueueLimit", 20),
                }
            );
        };
    }

    /// <summary>
    /// Creates the Models API rate limiting policy.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> CreateModelsApiPolicy(
        IConfiguration configuration
    )
    {
        return context =>
        {
            var userId = GetUserIdentifier(context);

            return RateLimitPartition.GetFixedWindowLimiter(
                userId,
                partition => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue<int>(
                        "Parameters:ModelsApiPermitLimit",
                        500
                    ),
                    Window = TimeSpan.FromSeconds(
                        configuration.GetValue<int>("Parameters:ModelsApiWindowSeconds", 1)
                    ),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue<int>("Parameters:ModelsApiQueueLimit", 100),
                }
            );
        };
    }

    /// <summary>
    /// Creates the Digital Twins API Read policy.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> CreateDigitalTwinsApiReadPolicy(
        IConfiguration configuration
    )
    {
        return context =>
        {
            var userId = GetUserIdentifier(context);

            return RateLimitPartition.GetFixedWindowLimiter(
                userId,
                partition => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue<int>(
                        "Parameters:DigitalTwinsApiReadPermitLimit",
                        5000
                    ),
                    Window = TimeSpan.FromSeconds(
                        configuration.GetValue<int>(
                            "Parameters:DigitalTwinsApiReadWindowSeconds",
                            1
                        )
                    ),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue<int>(
                        "Parameters:DigitalTwinsApiReadQueueLimit",
                        500
                    ),
                }
            );
        };
    }

    /// <summary>
    /// Creates the Digital Twins API Write policy.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> CreateDigitalTwinsApiWritePolicy(
        IConfiguration configuration
    )
    {
        return context =>
        {
            var userId = GetUserIdentifier(context);

            return RateLimitPartition.GetFixedWindowLimiter(
                userId,
                partition => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue<int>(
                        "Parameters:DigitalTwinsApiWritePermitLimit",
                        5000
                    ),
                    Window = TimeSpan.FromSeconds(
                        configuration.GetValue<int>(
                            "Parameters:DigitalTwinsApiWriteWindowSeconds",
                            1
                        )
                    ),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue<int>(
                        "Parameters:DigitalTwinsApiWriteQueueLimit",
                        500
                    ),
                }
            );
        };
    }

    /// <summary>
    /// Creates the Digital Twins API Create/Delete policy.
    /// </summary>
    private static Func<
        HttpContext,
        RateLimitPartition<string>
    > CreateDigitalTwinsApiCreateDeletePolicy(IConfiguration configuration)
    {
        return context =>
        {
            var userId = GetUserIdentifier(context);

            return RateLimitPartition.GetFixedWindowLimiter(
                userId,
                partition => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue<int>(
                        "Parameters:DigitalTwinsApiCreateDeletePermitLimit",
                        2500
                    ),
                    Window = TimeSpan.FromSeconds(
                        configuration.GetValue<int>(
                            "Parameters:DigitalTwinsApiCreateDeleteWindowSeconds",
                            1
                        )
                    ),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue<int>(
                        "Parameters:DigitalTwinsApiCreateDeleteQueueLimit",
                        250
                    ),
                }
            );
        };
    }

    /// <summary>
    /// Creates the Digital Twins API Single Twin policy.
    /// This policy uses a combination of user ID and twin ID for partitioning.
    /// </summary>
    private static Func<
        HttpContext,
        RateLimitPartition<string>
    > CreateDigitalTwinsApiSingleTwinPolicy(IConfiguration configuration)
    {
        return context =>
        {
            // Extract twin ID from route for per-twin limiting
            var twinId = context.Request.RouteValues["id"]?.ToString() ?? "unknown";
            var userId = GetUserIdentifier(context);
            var partitionKey = $"{userId}:{twinId}";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                partition => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue<int>(
                        "Parameters:DigitalTwinsApiSingleTwinPermitLimit",
                        50
                    ),
                    Window = TimeSpan.FromSeconds(
                        configuration.GetValue<int>(
                            "Parameters:DigitalTwinsApiSingleTwinWindowSeconds",
                            1
                        )
                    ),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue<int>(
                        "Parameters:DigitalTwinsApiSingleTwinQueueLimit",
                        25
                    ),
                }
            );
        };
    }

    /// <summary>
    /// Creates the Query API rate limiting policy.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> CreateQueryApiPolicy(
        IConfiguration configuration
    )
    {
        return context =>
        {
            var userId = GetUserIdentifier(context);

            return RateLimitPartition.GetFixedWindowLimiter(
                userId,
                partition => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue<int>(
                        "Parameters:QueryApiPermitLimit",
                        2500
                    ),
                    Window = TimeSpan.FromSeconds(
                        configuration.GetValue<int>("Parameters:QueryApiWindowSeconds", 1)
                    ),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue<int>("Parameters:QueryApiQueueLimit", 500),
                }
            );
        };
    }

    /// <summary>
    /// Creates the custom rejection handler for rate limit violations.
    /// Returns HTTP 429 with detailed error information and retry-after timing.
    /// </summary>
    private static Func<OnRejectedContext, CancellationToken, ValueTask> CreateRejectionHandler()
    {
        return async (context, _) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            TimeSpan? retryAfter = null;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue))
            {
                retryAfter = retryAfterValue;
            }

            await context.HttpContext.Response.WriteAsJsonAsync(
                new
                {
                    error = "Rate limit exceeded",
                    message = "Too many requests. Please try again later.",
                    retryAfterSeconds = retryAfter?.TotalSeconds,
                }
            );
        };
    }
}

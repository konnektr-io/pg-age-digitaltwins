using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AgeDigitalTwins.ApiService.Configuration;

/// <summary>
/// Configuration for rate limiting policies to protect the API and database from overload.
/// Policies are designed based on operation intensity and resource usage.
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
        // Global rate limit - applies to all endpoints as a safety net
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var userId = GetUserIdentifier(context);

            return RateLimitPartition.GetFixedWindowLimiter(
                userId,
                partition => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = configuration.GetValue("Parameters:GlobalPermitLimit", 1000),
                    Window = TimeSpan.FromSeconds(
                        configuration.GetValue("Parameters:GlobalWindowSeconds", 1)
                    ),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue("Parameters:GlobalQueueLimit", 200),
                }
            );
        });

        // Light Operations - Read-only operations with low resource impact
        options.AddPolicy("LightOperations", CreateLightOperationsPolicy(configuration));

        // Medium Operations - Queries and batch operations with moderate resource impact
        options.AddPolicy("MediumOperations", CreateMediumOperationsPolicy(configuration));

        // Heavy Operations - Create/Update/Delete operations with high resource impact
        options.AddPolicy("HeavyOperations", CreateHeavyOperationsPolicy(configuration));

        // Admin Operations - Administrative operations like model management and jobs
        options.AddPolicy("AdminOperations", CreateAdminOperationsPolicy(configuration));

        // Weighted Query Policy - uses query charge for weighted rate limiting
        options.AddPolicy("WeightedQueryPolicy", CreateWeightedQueryPolicy(configuration));

        // Custom rejection response
        options.OnRejected = CreateRejectionHandler();
    }

    /// <summary>
    /// Creates a weighted query rate limiting policy using TokenBucketRateLimiter.
    /// Each request consumes tokens equal to the query charge (from HttpContext.Items["QueryCharge"]).
    /// To use this policy:
    ///   - Set HttpContext.Items["QueryCharge"] in your endpoint (see QueryEndpoints.cs).
    ///   - Use RequireRateLimiting("WeightedQueryPolicy") on the endpoint.
    ///   - Register WeightedQueryRateLimitingMiddleware before UseRateLimiter in Program.cs.
    /// The middleware will set the TokenCount for TokenBucketRateLimiterRequest based on QueryCharge.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> CreateWeightedQueryPolicy(
        IConfiguration configuration
    )
    {
        return context =>
        {
            var userId = GetUserIdentifier(context);
            return RateLimitPartition.GetTokenBucketLimiter(
                userId,
                partition => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = configuration.GetValue("Parameters:WeightedQueryTokenLimit", 1000),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue("Parameters:WeightedQueryQueueLimit", 100),
                    ReplenishmentPeriod = TimeSpan.FromSeconds(
                        configuration.GetValue("Parameters:WeightedQueryReplenishSeconds", 1)
                    ),
                    TokensPerPeriod = configuration.GetValue(
                        "Parameters:WeightedQueryTokensPerPeriod",
                        100
                    ),
                    AutoReplenishment = true,
                }
            /* context =>
            {

                if (context.Items.TryGetValue("QueryCharge", out var chargeObj))
                {
                    if (chargeObj is int chargeInt && chargeInt > 0)
                        return chargeInt;
                    if (chargeObj is string chargeStr && int.TryParse(chargeStr, out var chargeParsed) && chargeParsed > 0)
                        return chargeParsed;
                }
                // Default to 1 if not set
                return 1;

            } */
            );
        };
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
    /// Creates the Light Operations rate limiting policy.
    /// Used for read-only operations with low resource impact (GET single items, list operations).
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> CreateLightOperationsPolicy(
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
                    PermitLimit = configuration.GetValue(
                        "Parameters:LightOperationsPermitLimit",
                        100
                    ),
                    Window = TimeSpan.FromSeconds(
                        configuration.GetValue("Parameters:LightOperationsWindowSeconds", 1)
                    ),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue("Parameters:LightOperationsQueueLimit", 50),
                }
            );
        };
    }

    /// <summary>
    /// Creates the Medium Operations rate limiting policy.
    /// Used for queries and operations with moderate resource impact.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> CreateMediumOperationsPolicy(
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
                    PermitLimit = configuration.GetValue(
                        "Parameters:MediumOperationsPermitLimit",
                        50
                    ),
                    Window = TimeSpan.FromSeconds(
                        configuration.GetValue("Parameters:MediumOperationsWindowSeconds", 1)
                    ),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue(
                        "Parameters:MediumOperationsQueueLimit",
                        25
                    ),
                }
            );
        };
    }

    /// <summary>
    /// Creates the Heavy Operations rate limiting policy.
    /// Used for create/update/delete operations with high resource impact.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> CreateHeavyOperationsPolicy(
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
                    PermitLimit = configuration.GetValue(
                        "Parameters:HeavyOperationsPermitLimit",
                        20
                    ),
                    Window = TimeSpan.FromSeconds(
                        configuration.GetValue("Parameters:HeavyOperationsWindowSeconds", 1)
                    ),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue("Parameters:HeavyOperationsQueueLimit", 10),
                }
            );
        };
    }

    /// <summary>
    /// Creates the Admin Operations rate limiting policy.
    /// Used for administrative operations like model management and job operations.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> CreateAdminOperationsPolicy(
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
                    PermitLimit = configuration.GetValue(
                        "Parameters:AdminOperationsPermitLimit",
                        50
                    ),
                    Window = TimeSpan.FromMinutes(
                        configuration.GetValue("Parameters:AdminOperationsWindowMinutes", 1)
                    ),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = configuration.GetValue("Parameters:AdminOperationsQueueLimit", 25),
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
        return async (context, cancellationToken) =>
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
                },
                cancellationToken: cancellationToken
            );
        };
    }
}

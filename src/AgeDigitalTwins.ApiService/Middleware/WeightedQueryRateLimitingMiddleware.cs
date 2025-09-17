namespace AgeDigitalTwins.ApiService.Middleware
{
    /// <summary>
    /// Attribute to mark endpoints that use WeightedQueryPolicy for rate limiting.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class WeightedQueryPolicyAttribute : Attribute { }

    /// <summary>
    /// Middleware to set the TokenBucketRateLimiterRequest's TokenCount based on HttpContext.Items["QueryCharge"].
    /// This enables weighted rate limiting for queries.
    /// </summary>
    public class WeightedQueryRateLimitingMiddleware
    {
        private readonly RequestDelegate _next;

        public WeightedQueryRateLimitingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only set TokenCount if endpoint has WeightedQueryPolicyAttribute
            if (
                context.GetEndpoint()?.Metadata?.GetMetadata<WeightedQueryPolicyAttribute>() != null
            )
            {
                int tokenCount = 1;
                if (context.Items.TryGetValue("QueryCharge", out var chargeObj))
                {
                    if (chargeObj is int chargeInt && chargeInt > 0)
                        tokenCount = chargeInt;
                    else if (
                        chargeObj is string chargeStr
                        && int.TryParse(chargeStr, out var chargeParsed)
                        && chargeParsed > 0
                    )
                        tokenCount = chargeParsed;
                }
                // Set the token count for the rate limiter
                context.Features.Set(new TokenBucketRateLimiterRequest(tokenCount));
            }
            await _next(context);
        }
    }

    /// <summary>
    /// Feature to pass TokenBucketRateLimiterRequest to the rate limiting middleware.
    /// </summary>
    public class TokenBucketRateLimiterRequest
    {
        public int TokenCount { get; }

        public TokenBucketRateLimiterRequest(int tokenCount)
        {
            TokenCount = tokenCount;
        }
    }
}

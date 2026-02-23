using AgeDigitalTwins.Events.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgeDigitalTwins.Events.Core.Services;

/// <summary>
/// Health check that monitors the status of all registered event sinks (Kafka, MQTT, Kusto, etc.).
/// </summary>
public class EventSinksHealthCheck(IEnumerable<IEventSink> eventSinks) : IHealthCheck
{
    private readonly IEnumerable<IEventSink> _eventSinks = eventSinks;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        var unhealthySinks = _eventSinks.Where(s => !s.IsHealthy).ToList();

        if (unhealthySinks.Count == 0)
        {
            var data = new Dictionary<string, object>
            {
                ["totalSinks"] = _eventSinks.Count(),
                ["healthySinks"] = _eventSinks.Count(),
            };

            return Task.FromResult(
                HealthCheckResult.Healthy(
                    $"All {_eventSinks.Count()} event sink(s) are healthy",
                    data
                )
            );
        }
        else
        {
            var data = new Dictionary<string, object>
            {
                ["totalSinks"] = _eventSinks.Count(),
                ["healthySinks"] = _eventSinks.Count() - unhealthySinks.Count,
                ["unhealthySinks"] = unhealthySinks.Count,
                ["unhealthySinkNames"] = string.Join(", ", unhealthySinks.Select(s => s.Name)),
            };

            return Task.FromResult(
                HealthCheckResult.Unhealthy(
                    $"{unhealthySinks.Count} of {_eventSinks.Count()} event sink(s) are unhealthy: {string.Join(", ", unhealthySinks.Select(s => s.Name))}",
                    data: data
                )
            );
        }
    }
}

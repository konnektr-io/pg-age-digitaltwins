using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AgeDigitalTwins.Events;

/// <summary>
/// Health check that monitors the status of the PostgreSQL logical replication connection.
/// </summary>
public class ReplicationHealthCheck : IHealthCheck
{
    private readonly AgeDigitalTwinsReplication _replication;

    public ReplicationHealthCheck(AgeDigitalTwinsReplication replication)
    {
        _replication = replication;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        if (_replication.IsHealthy)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Replication connection is active"));
        }
        else
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Replication connection is not active")
            );
        }
    }
}

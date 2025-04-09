// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original code was adapted to support multihost connections.

using Aspire.Npgsql;
using HealthChecks.NpgSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using OpenTelemetry.Metrics;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for connecting PostgreSQL database with Npgsql client
/// </summary>
public static class AspirePostgreSqlNpgsqlMultihostExtensions
{
    private const string DefaultConfigSectionName = "Aspire:Npgsql";

    /// <summary>
    /// Registers <see cref="NpgsqlMultihostDataSource"/> service for connecting PostgreSQL database (multihost) with Npgsql client.
    /// Configures health check, logging and telemetry for the Npgsql client.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder" /> to read config from and add services to.</param>
    /// <param name="connectionName">A name used to retrieve the connection string from the ConnectionStrings configuration section.</param>
    /// <param name="configureSettings">An optional delegate that can be used for customizing options. It's invoked after the settings are read from the configuration.</param>
    /// <param name="configureDataSourceBuilder">An optional delegate that can be used for customizing the <see cref="NpgsqlDataSourceBuilder"/>.</param>
    /// <remarks>Reads the configuration from "Aspire:Npgsql" section.</remarks>
    /// <exception cref="ArgumentNullException">Thrown if mandatory <paramref name="builder"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when mandatory <see cref="NpgsqlSettings.ConnectionString"/> is not provided.</exception>
    public static void AddNpgsqlMultihostDataSource(
        this IHostApplicationBuilder builder,
        string connectionName,
        Action<NpgsqlSettings>? configureSettings = null,
        Action<NpgsqlDataSourceBuilder>? configureDataSourceBuilder = null
    ) =>
        AddNpgsqlMultihostDataSource(
            builder,
            configureSettings,
            connectionName,
            serviceKey: null,
            configureDataSourceBuilder: configureDataSourceBuilder
        );

    private static void AddNpgsqlMultihostDataSource(
        IHostApplicationBuilder builder,
        Action<NpgsqlSettings>? configureSettings,
        string connectionName,
        object? serviceKey,
        Action<NpgsqlDataSourceBuilder>? configureDataSourceBuilder
    )
    {
        ArgumentNullException.ThrowIfNull(builder);

        NpgsqlSettings settings = new();
        var configSection = builder.Configuration.GetSection(DefaultConfigSectionName);
        var namedConfigSection = configSection.GetSection(connectionName);
        configSection.Bind(settings);
        namedConfigSection.Bind(settings);

        if (builder.Configuration.GetConnectionString(connectionName) is string connectionString)
        {
            settings.ConnectionString = connectionString;
        }

        configureSettings?.Invoke(settings);

        builder.RegisterNpgsqlMultihostServices(
            settings,
            connectionName,
            serviceKey,
            configureDataSourceBuilder
        );

        // Same as SqlClient connection pooling is on by default and can be handled with connection string
        // https://www.npgsql.org/doc/connection-string-parameters.html#pooling
        if (!settings.DisableHealthChecks)
        {
            builder.TryAddHealthCheck(
                new HealthCheckRegistration(
                    serviceKey is null ? "PostgreSql" : $"PostgreSql_{connectionName}",
                    sp => new NpgSqlHealthCheck(
                        new NpgSqlHealthCheckOptions(
                            serviceKey is null
                                ? sp.GetRequiredService<NpgsqlDataSource>()
                                : sp.GetRequiredKeyedService<NpgsqlDataSource>(serviceKey)
                        )
                    ),
                    failureStatus: default,
                    tags: default,
                    timeout: default
                )
            );
        }

        if (!settings.DisableTracing)
        {
            builder
                .Services.AddOpenTelemetry()
                .WithTracing(tracerProviderBuilder =>
                {
                    tracerProviderBuilder.AddNpgsql();
                });
        }

        if (!settings.DisableMetrics)
        {
            builder.Services.AddOpenTelemetry().WithMetrics(NpgsqlCommon.AddNpgsqlMetrics);
        }
    }

    private static void RegisterNpgsqlMultihostServices(
        this IHostApplicationBuilder builder,
        NpgsqlSettings settings,
        string connectionName,
        object? serviceKey,
        Action<NpgsqlDataSourceBuilder>? configureDataSourceBuilder
    )
    {
        builder.Services.AddMultiHostNpgsqlDataSource(
            settings.ConnectionString ?? string.Empty,
            dataSourceBuilder =>
            {
                // delay validating the ConnectionString until the DataSource is requested. This ensures an exception doesn't happen until a Logger is established.
                ConnectionStringValidation.ValidateConnectionString(
                    settings.ConnectionString,
                    connectionName,
                    DefaultConfigSectionName
                );

                configureDataSourceBuilder?.Invoke(dataSourceBuilder);
            },
            serviceKey: serviceKey
        );
    }
}

internal static class ConnectionStringValidation
{
    public static void ValidateConnectionString(
        string? connectionString,
        string connectionName,
        string defaultConfigSectionName,
        string? typeSpecificSectionName = null,
        bool isEfDesignTime = false
    )
    {
        if (string.IsNullOrWhiteSpace(connectionString) && !isEfDesignTime)
        {
            var errorMessage =
                (!string.IsNullOrEmpty(typeSpecificSectionName))
                    ? $"ConnectionString is missing. It should be provided in 'ConnectionStrings:{connectionName}' or under the 'ConnectionString' key in '{defaultConfigSectionName}' or '{typeSpecificSectionName}' configuration section."
                    : $"ConnectionString is missing. It should be provided in 'ConnectionStrings:{connectionName}' or under the 'ConnectionString' key in '{defaultConfigSectionName}' configuration section.";

            throw new InvalidOperationException(errorMessage);
        }
    }
}

internal static class HealthChecksExtensions
{
    /// <summary>
    /// Adds a HealthCheckRegistration if one hasn't already been added to the builder.
    /// </summary>
    public static void TryAddHealthCheck(
        this IHostApplicationBuilder builder,
        HealthCheckRegistration healthCheckRegistration
    )
    {
        builder.TryAddHealthCheck(
            healthCheckRegistration.Name,
            hcBuilder => hcBuilder.Add(healthCheckRegistration)
        );
    }

    /// <summary>
    /// Invokes the <paramref name="addHealthCheck"/> action if the given <paramref name="name"/> hasn't already been added to the builder.
    /// </summary>
    public static void TryAddHealthCheck(
        this IHostApplicationBuilder builder,
        string name,
        Action<IHealthChecksBuilder> addHealthCheck
    )
    {
        var healthCheckKey = $"Aspire.HealthChecks.{name}";
        if (!builder.Properties.ContainsKey(healthCheckKey))
        {
            builder.Properties[healthCheckKey] = true;
            addHealthCheck(builder.Services.AddHealthChecks());
        }
    }
}

internal static class NpgsqlCommon
{
    public static void AddNpgsqlMetrics(MeterProviderBuilder meterProviderBuilder)
    {
        double[] secondsBuckets =
        [
            0,
            0.005,
            0.01,
            0.025,
            0.05,
            0.075,
            0.1,
            0.25,
            0.5,
            0.75,
            1,
            2.5,
            5,
            7.5,
            10,
        ];

        // https://github.com/npgsql/npgsql/blob/4c9921de2dfb48fb5a488787fc7422add3553f50/src/Npgsql/MetricsReporter.cs#L48
        meterProviderBuilder
            .AddMeter("Npgsql")
            // Npgsql's histograms are in seconds, not milliseconds.
            .AddView(
                "db.client.commands.duration",
                new ExplicitBucketHistogramConfiguration { Boundaries = secondsBuckets }
            )
            .AddView(
                "db.client.connections.create_time",
                new ExplicitBucketHistogramConfiguration { Boundaries = secondsBuckets }
            );
    }
}

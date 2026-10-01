using System.Globalization;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AgeDigitalTwins.Test;

/// <summary>
/// The Apache AGE extension version reported by the database under test.
/// </summary>
public static class AgeVersion
{
    private static readonly Lazy<Version?> _version = new(Probe);

    /// <summary>
    /// The installed AGE version, or null when it cannot be determined.
    /// </summary>
    public static Version? Current => _version.Value;

    /// <summary>
    /// True when the installed AGE version is at least <paramref name="required"/>.
    /// </summary>
    public static bool AtLeast(Version required) =>
        Current is { } current && current >= required;

    /// <summary>
    /// AGE release that fixed ORDER BY resolution against a WITH-defined alias
    /// (apache/age#2269). Before it, such an ORDER BY fails with "could not find rte".
    /// </summary>
    public static readonly Version OrderByAliasFixedIn = new(1, 7, 0);

    /// <summary>
    /// Human-readable version for skip messages, e.g. "1.6.0".
    /// </summary>
    public static string Display => Current?.ToString() ?? "unknown";

    private static Version? Probe()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.Development.json")
                .Build();
            string connectionString =
                configuration.GetConnectionString("agedb")
                ?? throw new InvalidOperationException("agedb connection string missing");

            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = "ag_catalog, \"$user\", public",
            };

            using var connection = new NpgsqlConnection(builder.ConnectionString);
            connection.Open();

            using var command = new NpgsqlCommand(
                "SELECT extversion FROM pg_extension WHERE extname = 'age'",
                connection
            );
            var raw = (string?)command.ExecuteScalar();
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            return Version.TryParse(raw.Split('-')[0], out var parsed) ? parsed : null;
        }
        catch
        {
            // A probe failure must never mask the real test outcome; report it as unknown.
            return null;
        }
    }
}

/// <summary>
/// A fact that only runs when the database under test hosts Apache AGE 1.7.0 or newer.
/// ORDER BY on a WITH-defined alias fails before that (apache/age#2269).
/// </summary>
public sealed class OrderByAliasFactAttribute : FactAttribute
{
    public OrderByAliasFactAttribute()
    {
        if (!AgeVersion.AtLeast(AgeVersion.OrderByAliasFixedIn))
        {
            Skip = string.Create(
                CultureInfo.InvariantCulture,
                $"ORDER BY on a WITH alias requires Apache AGE >= {AgeVersion.OrderByAliasFixedIn}; found {AgeVersion.Display}"
            );
        }
    }
}
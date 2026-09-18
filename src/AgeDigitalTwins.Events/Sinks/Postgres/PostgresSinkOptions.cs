using AgeDigitalTwins.Events.Abstractions;

namespace AgeDigitalTwins.Events.Sinks.Postgres;

/// <summary>
/// Options for the PostgreSQL / TimescaleDB data history sink.
/// </summary>
public class PostgresSinkOptions : SinkOptions
{
    /// <summary>
    /// Connection string of the target database. When omitted, the sink writes to the
    /// same database as the graph (the events service <c>ConnectionStrings:agedb</c> value).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Schema that holds the data history tables. Defaults to <c>public</c>.
    /// </summary>
    public string? Schema { get; set; }

    /// <summary>
    /// Table for property events. Defaults to <c>adt_property_events</c>.
    /// </summary>
    public string? PropertyEventsTable { get; set; }

    /// <summary>
    /// Table for twin lifecycle events. Defaults to <c>adt_twin_lifecycle_events</c>.
    /// </summary>
    public string? TwinLifeCycleEventsTable { get; set; }

    /// <summary>
    /// Table for relationship lifecycle events. Defaults to <c>adt_relationship_lifecycle_events</c>.
    /// </summary>
    public string? RelationshipLifeCycleEventsTable { get; set; }

    /// <summary>
    /// When enabled, the <c>updated_by</c> column is created and populated with the
    /// <c>updatedBy</c> value carried by the event. Mirrors the Kusto sink behaviour; the
    /// flag is set from <c>Parameters:TrackLastUpdatedBy</c> of the events service.
    /// </summary>
    public bool TrackLastUpdatedBy { get; set; }
}

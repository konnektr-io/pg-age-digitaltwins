using AgeDigitalTwins.Events.Sinks.Kusto;
using Kusto.Data.Common;

namespace AgeDigitalTwins.Events.Test;

[Trait("Category", "Integration")]
public class KustoEventSinkTests
{
    [Fact]
    public void BuildPropertyEventMappings_WhenTrackLastUpdatedByTrue_IncludesUpdatedBy()
    {
        var mappings = KustoEventSink.BuildPropertyEventMappings(trackLastUpdatedBy: true);

        var updatedByMapping = mappings.FirstOrDefault(m => GetColumnName(m) == "UpdatedBy");
        Assert.NotNull(updatedByMapping);
    }

    [Fact]
    public void BuildPropertyEventMappings_WhenTrackLastUpdatedByFalse_OmitsUpdatedBy()
    {
        var mappings = KustoEventSink.BuildPropertyEventMappings(trackLastUpdatedBy: false);

        Assert.DoesNotContain(mappings, m => GetColumnName(m) == "UpdatedBy");
    }

    [Fact]
    public void BuildTwinLifecycleEventMappings_WhenTrackLastUpdatedByTrue_IncludesUpdatedBy()
    {
        var mappings = KustoEventSink.BuildTwinLifecycleEventMappings(trackLastUpdatedBy: true);

        Assert.Contains(mappings, m => GetColumnName(m) == "TimeStamp");
        Assert.Contains(mappings, m => GetColumnName(m) == "ServiceId");
        Assert.Contains(mappings, m => GetColumnName(m) == "TwinId");
        Assert.Contains(mappings, m => GetColumnName(m) == "Action");
        Assert.Contains(mappings, m => GetColumnName(m) == "ModelId");

        Assert.Contains(mappings, m => GetColumnName(m) == "UpdatedBy");
    }

    [Fact]
    public void BuildTwinLifecycleEventMappings_WhenTrackLastUpdatedByFalse_OmitsUpdatedBy()
    {
        var mappings = KustoEventSink.BuildTwinLifecycleEventMappings(trackLastUpdatedBy: false);

        Assert.DoesNotContain(mappings, m => GetColumnName(m) == "UpdatedBy");
    }

    [Fact]
    public void BuildRelationshipLifecycleEventMappings_WhenTrackLastUpdatedByTrue_IncludesUpdatedBy()
    {
        var mappings = KustoEventSink.BuildRelationshipLifecycleEventMappings(trackLastUpdatedBy: true);

        Assert.Contains(mappings, m => GetColumnName(m) == "TimeStamp");
        Assert.Contains(mappings, m => GetColumnName(m) == "ServiceId");
        Assert.Contains(mappings, m => GetColumnName(m) == "RelationshipId");
        Assert.Contains(mappings, m => GetColumnName(m) == "Action");
        Assert.Contains(mappings, m => GetColumnName(m) == "Name");
        Assert.Contains(mappings, m => GetColumnName(m) == "Source");
        Assert.Contains(mappings, m => GetColumnName(m) == "Target");

        Assert.Contains(mappings, m => GetColumnName(m) == "UpdatedBy");
    }

    [Fact]
    public void BuildRelationshipLifecycleEventMappings_WhenTrackLastUpdatedByFalse_OmitsUpdatedBy()
    {
        var mappings = KustoEventSink.BuildRelationshipLifecycleEventMappings(trackLastUpdatedBy: false);

        Assert.DoesNotContain(mappings, m => GetColumnName(m) == "UpdatedBy");
    }

    private static string GetColumnName(ColumnMapping mapping)
    {
        return mapping.GetType().GetProperty("Column")?.GetValue(mapping) as string
            ?? mapping.GetType().GetProperty("ColumnName")?.GetValue(mapping) as string
            ?? mapping.GetType().GetProperty("Name")?.GetValue(mapping) as string
            ?? "";
    }
}

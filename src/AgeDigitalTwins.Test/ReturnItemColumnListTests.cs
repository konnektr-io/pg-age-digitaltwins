using System.Text.Json;

namespace AgeDigitalTwins.Test;

/// <summary>
/// Regression coverage for issue #115: the AGE driver emitted an EMPTY column-definition list
/// for certain RETURN items, producing <c>... $$) as ( agtype)</c> which PostgreSQL rejects with
/// <c>42601: syntax error at or near ")"</c>.
///
/// Fixed in Konnektr.Npgsql.Age 2.1.0, so these assertions are unconditional. If a future driver
/// release regresses the column-name derivation, they fail here rather than surfacing as a
/// mystifying 42601 from generated SQL.
///
/// Affected shapes (both failed on 2.0.0, with or without ORDER BY, with or without a page size):
/// <list type="bullet">
///   <item>a bare scalar return item — <c>RETURN t.returnPeriod AS period</c></item>
///   <item>a map projection WITHOUT <c>.*</c> — <c>RETURN t { .returnPeriod } AS twin</c></item>
/// </list>
/// </summary>
[Trait("Category", "Integration")]
public class ReturnItemColumnListTests : TestBase
{
    private const string WidgetModel = "dtmi:com:example:contoso:Widget;1";

    private static readonly string[] WidgetModels =
    [
        $$"""
          {
            "@id": "{{WidgetModel}}",
            "@type": "Interface",
            "@context": ["dtmi:dtdl:context;3"],
            "displayName": "Widget",
            "contents": [
              { "@type": "Property", "name": "returnPeriod", "schema": "string" },
              { "@type": "Property", "name": "label", "schema": "string" }
            ]
          }
          """,
    ];

    internal async Task IntializeAsync()
    {
        await Client.InitializeAsync();
        await Client.CreateModelsAsync(WidgetModels);
    }

    private async Task SeedAsync()
    {
        await Client.CreateOrReplaceDigitalTwinAsync(
            "widgetA",
            $$"""
              {"$dtId": "widgetA", "$metadata": {"$model": "{{WidgetModel}}"},
               "returnPeriod": "1990", "label": "first"}
              """
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "widgetB",
            $$"""
              {"$dtId": "widgetB", "$metadata": {"$model": "{{WidgetModel}}"},
               "returnPeriod": "2020", "label": "second"}
              """
        );
    }

    /// <summary>
    /// A bare scalar return item, unpaginated. This is also the shape the ADT query language
    /// produces for <c>SELECT T.name FROM DIGITALTWINS</c>, so it must round-trip.
    /// </summary>
    [Fact]
    public async Task QueryAsync_ScalarReturnItem_Unpaginated()
    {
        await IntializeAsync();
        await SeedAsync();

        var page = await Client
            .QueryAsync<JsonDocument>(
                """
                MATCH (t:Twin)
                RETURN t.returnPeriod AS period
                """
            )
            .AsPages()
            .FirstAsync();

        Assert.NotNull(page);
        Assert.NotNull(page.Value);
        Assert.Equal(2, page.Value.Count());
    }

    /// <summary>
    /// The same scalar item WITH an ORDER BY on the alias and a page size, so both the driver's
    /// column derivation and the pagination rewrite are in play.
    /// </summary>
    [Fact]
    public async Task QueryAsync_ScalarReturnItem_OrderByAlias_Paginated()
    {
        await IntializeAsync();
        await SeedAsync();

        var periods = new List<string>();
        await foreach (
            var page in Client
                .QueryAsync<JsonDocument>(
                    """
                    MATCH (t:Twin)
                    RETURN t.returnPeriod AS period
                    ORDER BY period DESC
                    """
                )
                .AsPages(pageSizeHint: 1)
        )
        {
            Assert.NotNull(page.Value);
            periods.AddRange(
                page.Value.Select(d => d!.RootElement.GetProperty("period").GetString()!)
            );
        }

        // Two single-item pages prove the alias sort survived the SKIP injection.
        Assert.Equal(new[] { "2020", "1990" }, periods);
    }

    /// <summary>
    /// A map projection that does NOT include <c>.*</c>.
    /// </summary>
    [Fact]
    public async Task QueryAsync_MapProjectionWithoutStar_Unpaginated()
    {
        await IntializeAsync();
        await SeedAsync();

        var page = await Client
            .QueryAsync<JsonDocument>(
                """
                MATCH (t:Twin)
                RETURN t { .returnPeriod } AS twin
                """
            )
            .AsPages()
            .FirstAsync();

        Assert.NotNull(page);
        Assert.NotNull(page.Value);
        Assert.Equal(2, page.Value.Count());
        // The projection carries only the requested property — no $dtId, no $metadata.
        var first = page.Value.First()!.RootElement.GetProperty("twin");
        Assert.Equal("1990", first.GetProperty("returnPeriod").GetString());
        Assert.False(first.TryGetProperty("$dtId", out _));
    }

    /// <summary>
    /// The projection-without-star shape with a page size, sorting by a node property rather than
    /// by the projection alias.
    ///
    /// NOTE: <c>ORDER BY twin.returnPeriod</c> (sorting on the projected MAP's property) is NOT
    /// asserted here — that is upstream apache/age#2388, a still-open AGE limitation unrelated to
    /// the driver. It fails with <c>42703 could not find rte for twin</c> on every AGE version
    /// tested, and is a different defect from the empty column list this file covers. Sorting on
    /// the projection alias is version-gated by <see cref="OrderByAliasFactAttribute"/> instead.
    /// </summary>
    [Fact]
    public async Task QueryAsync_MapProjectionWithoutStar_Paginated()
    {
        await IntializeAsync();
        await SeedAsync();

        var periods = new List<string>();
        await foreach (
            var page in Client
                .QueryAsync<JsonDocument>(
                    """
                    MATCH (t:Twin)
                    WITH t
                    ORDER BY t.returnPeriod ASC
                    RETURN t { .returnPeriod } AS twin
                    """
                )
                .AsPages(pageSizeHint: 1)
        )
        {
            Assert.NotNull(page.Value);
            periods.AddRange(
                page.Value.Select(d =>
                    d!.RootElement.GetProperty("twin").GetProperty("returnPeriod").GetString()!
                )
            );
        }

        Assert.Equal(new[] { "1990", "2020" }, periods);
    }

    /// <summary>
    /// Several scalar properties at once — the multi-column form of the same fix.
    /// </summary>
    [Fact]
    public async Task QueryAsync_MultipleScalarReturnItems()
    {
        await IntializeAsync();
        await SeedAsync();

        var page = await Client
            .QueryAsync<JsonDocument>(
                """
                MATCH (t:Twin)
                RETURN t.returnPeriod AS period, t.label AS label
                """
            )
            .AsPages()
            .FirstAsync();

        Assert.NotNull(page);
        Assert.NotNull(page.Value);
        Assert.Equal(2, page.Value.Count());
        Assert.Equal(2, page.Value.First()!.RootElement.EnumerateObject().Count());
    }
}
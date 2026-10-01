using System.Text.Json;

namespace AgeDigitalTwins.Test;

/// <summary>
/// Integration coverage for ORDER BY over twin properties (issue #113).
///
/// These run through the real product path — <c>QueryAsync</c> plus the SKIP/LIMIT pagination
/// rewrite — because that rewrite decides where SKIP/LIMIT land relative to ORDER BY, which
/// changes what Apache AGE receives.
///
/// Note on AGE versions: sorting on a WITH-defined scalar alias (<c>ORDER BY rp</c>) is rejected
/// by Apache AGE &lt; 1.7.0 with <c>could not find rte for rp</c>; it was fixed upstream in
/// apache/age#2269 and works from 1.7.0 on. Tests that depend on it are skipped below that
/// version so the PG16/PG17 cells of the CI matrix stay meaningful.
/// </summary>
[Trait("Category", "Integration")]
public class OrderByTests : TestBase
{
    private const string GeoModel = "dtmi:com:arcadis:climaterisk:HazardGeoTIFF;1";
    private const string SpatialModel = "dtmi:com:arcadis:climaterisk:HazardSpatialData;1";

    /// <summary>Seeded out of order so an unsorted scan cannot pass as a correct sort.</summary>
    private static readonly string[] ExpectedAscending = ["1990", "2020", "2050"];

    private static readonly string[] HazardModels =
    [
        $$"""
          {
            "@id": "{{GeoModel}}",
            "@type": "Interface",
            "@context": ["dtmi:dtdl:context;3"],
            "displayName": "HazardGeoTIFF",
            "contents": [
              { "@type": "Property", "name": "returnPeriod", "schema": "string" }
            ]
          }
          """,
        $$"""
          {
            "@id": "{{SpatialModel}}",
            "@type": "Interface",
            "@context": ["dtmi:dtdl:context;3"],
            "displayName": "HazardSpatialData",
            "contents": [
              { "@type": "Property", "name": "returnPeriod", "schema": "string" }
            ]
          }
          """,
    ];

    internal async Task IntializeAsync()
    {
        // TestBase uses a random per-test graph name, so the graph must be initialized
        // explicitly (schema, indexes and the is_of_model function family) before use.
        await Client.InitializeAsync();
        await Client.CreateModelsAsync(HazardModels);
    }

    /// <summary>
    /// Sort by a node property with a map projection aliased to the same name as the variable.
    /// This is the shape from case 1 of the issue and must work on every supported AGE version.
    /// </summary>
    [Fact]
    public async Task QueryAsync_OrderByNodeProperty_MapProjectionShadowingVariable()
    {
        await IntializeAsync();
        await SeedTwinsAsync();

        string graph = Client.GetGraphName();
        string query =
            $$"""
              MATCH (t:Twin)
              WHERE {{graph}}.is_of_model(t, '{{GeoModel}}')
              WITH t
              ORDER BY t.returnPeriod ASC
              RETURN t { .*, zoomable: false } AS t
              """;

        var periods = await RunAsync(query);
        var sorted = periods.OrderBy(p => int.Parse(p)).ToArray();

        // Only the two HazardGeoTIFF twins match. returnPeriod is a string, so the sort must
        // still come out numerically ascending — lexicographically "2050" sorts before "1990".
        Assert.Equal(new[] { "1990", "2050" }, sorted);
    }

    /// <summary>
    /// Same shape, descending, and with pagination engaged — the offset must be applied to the
    /// SORTED stream, otherwise page 2 does not continue page 1.
    /// </summary>
    [Fact]
    public async Task QueryAsync_OrderByNodeProperty_Descending_Paginated()
    {
        await IntializeAsync();
        await SeedTwinsAsync();

        string graph = Client.GetGraphName();
        string query =
            $$"""
              MATCH (t:Twin)
              WHERE {{graph}}.is_of_model(t, '{{GeoModel}}')
              WITH t
              ORDER BY toInteger(t.returnPeriod) DESC
              RETURN t { .*, zoomable: false } AS t
              """;

        var periods = await RunAsync(query, pageSize: 1);

        // 2050 then 1990 across two single-item pages proves ORDER BY was applied before SKIP.
        Assert.Equal(new[] { "2050", "1990" }, periods);
    }

    /// <summary>
    /// Case 2 of the issue, verbatim: a scalar alias introduced in WITH, sorted via that alias.
    /// Requires Apache AGE >= 1.7.0 (apache/age#2269).
    /// </summary>
    [OrderByAliasFact]
    public async Task QueryAsync_WithScalarAlias_OrderByAlias_Verbatim()
    {
        await IntializeAsync();
        await SeedTwinsAsync();

        string graph = Client.GetGraphName();
        string query =
            $$"""
              MATCH (t:Twin)
              WHERE ({{graph}}.is_of_model(t, '{{GeoModel}}')
                OR {{graph}}.is_of_model(t, '{{SpatialModel}}'))
              WITH t, toInteger(t.returnPeriod) AS rp
              ORDER BY rp ASC
              RETURN t { .*, zoomable: false } AS twin
              """;

        var periods = await RunAsync(query, alias: "twin");
        var sorted = periods.OrderBy(p => int.Parse(p)).ToArray();

        Assert.Equal(ExpectedAscending, sorted);
    }

    /// <summary>
    /// Case 2 across pages: the alias sort must survive the SKIP injection on every page.
    /// Requires Apache AGE >= 1.7.0.
    /// </summary>
    [OrderByAliasFact]
    public async Task QueryAsync_WithScalarAlias_OrderByAlias_Paginated()
    {
        await IntializeAsync();
        await SeedTwinsAsync();

        string query =
            """
            MATCH (t:Twin)
            WITH t, toInteger(t.returnPeriod) AS rp
            ORDER BY rp ASC
            RETURN t { .*, zoomable: false } AS twin
            """;

        var periods = await RunAsync(query, pageSize: 2, alias: "twin");
        var sorted = periods.OrderBy(p => int.Parse(p)).ToArray();

        Assert.Equal(ExpectedAscending, sorted);
    }

    /// <summary>
    /// Sorting by the alias after RETURN. Kept because it is the workaround we documented for
    /// AGE &lt; 1.7.0, and it must keep working when pagination appends SKIP/LIMIT.
    /// </summary>
    [Fact]
    public async Task QueryAsync_OrderByAliasAfterReturn()
    {
        await IntializeAsync();
        await SeedTwinsAsync();

        string query =
            """
            MATCH (t:Twin)
            WITH t, toInteger(t.returnPeriod) AS rp
            RETURN t { .*, zoomable: false } AS twin
            ORDER BY rp ASC
            """;

        var periods = await RunAsync(query, alias: "twin");
        var sorted = periods.OrderBy(p => int.Parse(p)).ToArray();

        Assert.Equal(ExpectedAscending, sorted);
    }

    /// <summary>
        /// Sorting by a scalar alias is covered by the WITH-alias tests. NOTE: two related shapes
        /// cannot be asserted through this path at all — the AGE driver emits an empty
        /// column-definition list when a RETURN item is not a plain node property
        /// (<c>... $$) as ( agtype)</c>), which PostgreSQL rejects with 42601. This affects:
        /// <c>RETURN t.returnPeriod AS period</c> (bare scalar) and
        /// <c>RETURN t { .returnPeriod } AS twin</c> (projection without <c>.*</c>). Both are
        /// pre-existing driver limitations, independent of the pagination rewrite, so they are
        /// deliberately left unasserted here rather than encoded as expected behaviour.
        /// </summary>
        [Fact]
        public async Task QueryAsync_OrderByNodeProperty_MapProjection_Paginated()
        {
            await IntializeAsync();
            await SeedTwinsAsync();

            // Full map projection (with .*) is the supported shape and keeps the sort key
            // in the payload, so it also exercises the sort key surviving the rewrite.
            string graph = Client.GetGraphName();
            string query =
                $$"""
                  MATCH (t:Twin)
                  WHERE {{graph}}.is_of_model(t, '{{GeoModel}}')
                  WITH t
                  ORDER BY toInteger(t.returnPeriod) DESC
                  RETURN t { .*, zoomable: false } AS twin
                  """;

            var periods = await RunAsync(query, pageSize: 1, alias: "twin");

            Assert.Equal(new[] { "2050", "1990" }, periods);
        }

    /// <summary>
    /// Runs the query through the pagination pipeline and collects returnPeriod values in the
    /// order the server produced them.
    /// </summary>
    private async Task<List<string>> RunAsync(
        string query,
        int? pageSize = null,
        string alias = "t"
    )
    {
        var periods = new List<string>();
        var pages = 0;

        var pageable = Client.QueryAsync<JsonDocument>(query);
        var pages1 = pageSize.HasValue
            ? pageable.AsPages(pageSizeHint: pageSize.Value)
            : pageable.AsPages();

        await foreach (var page in pages1)
        {
            pages++;
            Assert.True(pages < 25, "pagination did not terminate");
            if (page.Value is null)
                continue;

            periods.AddRange(
                page.Value.Select(d =>
                    d!.RootElement.GetProperty(alias).GetProperty("returnPeriod").GetString()!
                )
            );
        }

        return periods;
    }

    /// <summary>
    /// Seed twins in deliberately scrambled returnPeriod order. Natural scan order is
    /// 2050, 1990, 2020, so an unsorted result is distinguishable from a sorted one.
    /// </summary>
    private async Task SeedTwinsAsync()
    {
        await Client.CreateOrReplaceDigitalTwinAsync(
            "orderByTwinC",
            $$"""
              {"$dtId": "orderByTwinC", "$metadata": {"$model": "{{GeoModel}}"}, "returnPeriod": "2050"}
              """
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "orderByTwinA",
            $$"""
              {"$dtId": "orderByTwinA", "$metadata": {"$model": "{{GeoModel}}"}, "returnPeriod": "1990"}
              """
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "orderByTwinB",
            $$"""
              {"$dtId": "orderByTwinB", "$metadata": {"$model": "{{SpatialModel}}"}, "returnPeriod": "2020"}
              """
        );
    }
}
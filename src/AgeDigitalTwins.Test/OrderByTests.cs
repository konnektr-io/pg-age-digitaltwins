using System.Text.Json;
using Npgsql;

namespace AgeDigitalTwins.Test;

/// <summary>
/// Integration coverage for ORDER BY over twin properties (issue #113).
///
/// These run through the real product path — <c>QueryAsync</c> plus the SKIP/LIMIT pagination
/// rewrite — because that rewrite decides where SKIP/LIMIT land relative to ORDER BY, which
/// changes what Apache AGE receives.
///
/// Fixtures use generic, domain-neutral models (a room-like interface and a second interface
/// inheriting from it) rather than any customer- or domain-specific DTMI, so the suite documents
/// the ADT behaviour rather than one deployment's vocabulary.
///
/// AGE version notes (see <see cref="OrderByAliasFactAttribute"/>):
/// <list type="bullet">
///   <item>ORDER BY on a WITH-defined scalar alias fails on Apache AGE &lt; 1.7.0 with
///   <c>42703 could not find rte for &lt;alias&gt;</c> (upstream apache/age#2269). Asserted for
///   AGE &gt;= 1.7.0, and pinned as an xfail below that.</item>
///   <item>AGE accepts only ONE trailing clause set, so a query ending in ORDER BY cannot also
///   carry SKIP/LIMIT. Sorting by an alias placed AFTER the RETURN therefore breaks as soon as
///   pagination kicks in — the rewrite appends SKIP/LIMIT after it. Covered here as a
///   version-independent guard, because it is our own clause ordering that is wrong, not AGE.</item>
/// </list>
/// </summary>
[Trait("Category", "Integration")]
public class OrderByTests : TestBase
{
    private const string PrimaryModel = "dtmi:com:example:contoso:Widget;1";
    private const string DerivedModel = "dtmi:com:example:contoso:SubWidget;1";

    /// <summary>Seeded out of order so an unsorted scan cannot pass as a correct sort.</summary>
    private static readonly string[] ExpectedAscending = ["1990", "2020", "2050"];

    private static readonly string[] WidgetModels =
    [
        $$"""
          {
            "@id": "{{PrimaryModel}}",
            "@type": "Interface",
            "@context": ["dtmi:dtdl:context;3"],
            "displayName": "Widget",
            "contents": [
              { "@type": "Property", "name": "returnPeriod", "schema": "string" }
            ]
          }
          """,
        // Extends the primary model. returnPeriod is deliberately NOT redeclared here:
        // DTDL rejects a derived interface that declares a name it inherits transitively.
        // Inheriting it is exactly what is_of_model's descendant matching is meant to cover.
        $$"""
          {
            "@id": "{{DerivedModel}}",
            "@type": "Interface",
            "@context": ["dtmi:dtdl:context;3"],
            "displayName": "SubWidget",
            "extends": "{{PrimaryModel}}"
          }
          """,
    ];

    internal async Task IntializeAsync()
    {
        // TestBase uses a random per-test graph name, so the graph must be initialized
        // explicitly (schema, indexes and the is_of_model function family) before use.
        await Client.InitializeAsync();
        await Client.CreateModelsAsync(WidgetModels);
    }

    /// <summary>
    /// Sort by a node property with a map projection aliased to the same name as the variable.
    /// This is case 1 of issue #113 and must work on every supported AGE version.
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
              WHERE {{graph}}.is_of_model(t, '{{PrimaryModel}}')
              WITH t
              ORDER BY t.returnPeriod ASC
              RETURN t { .*, zoomable: false } AS t
              """;

        var periods = await RunAsync(query);

        // is_of_model includes descendants, so all three seeded twins match. returnPeriod is a
        // string, so ascending must still come out numerically — lexicographically "2050" < "1990".
        Assert.Equal(ExpectedAscending, periods.OrderBy(p => int.Parse(p)).ToArray());
    }

    /// <summary>
    /// Same shape, descending, with pagination engaged — the offset must be applied to the
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
              WHERE {{graph}}.is_of_model(t, '{{PrimaryModel}}')
              WITH t
              ORDER BY toInteger(t.returnPeriod) DESC
              RETURN t { .*, zoomable: false } AS twin
              """;

        var periods = await RunAsync(query, pageSize: 1, alias: "twin");

        // 2050, 2020, 1990 across single-item pages proves ORDER BY was applied before SKIP.
        Assert.Equal(new[] { "2050", "2020", "1990" }, periods);
    }

    /// <summary>
    /// Sorting by a scalar alias introduced in WITH, then sorted via that alias
    /// (<c>WITH t, toInteger(t.returnPeriod) AS rp ORDER BY rp</c>).
    ///
    /// Asserted for Apache AGE &gt;= 1.7.0, where apache/age#2269 made alias resolution work.
    /// The complementary assertion that this shape FAILS on AGE &lt; 1.7.0 lives in
    /// <see cref="OrderByAliasNotResolved_FailsOnAgeBefore17"/> — together they pin the
    /// version boundary from both sides.
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
              WHERE ({{graph}}.is_of_model(t, '{{PrimaryModel}}')
                OR {{graph}}.is_of_model(t, '{{DerivedModel}}'))
              WITH t, toInteger(t.returnPeriod) AS rp
              ORDER BY rp ASC
              RETURN t { .*, zoomable: false } AS twin
              """;

        var periods = await RunAsync(query, alias: "twin");

        Assert.Equal(ExpectedAscending, periods.OrderBy(p => int.Parse(p)).ToArray());
    }

    /// <summary>
    /// The alias sort must survive SKIP injection on every page. AGE &gt;= 1.7.0 only.
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

        Assert.Equal(ExpectedAscending, periods.OrderBy(p => int.Parse(p)).ToArray());
    }

    /// <summary>
    /// Inverse of <see cref="QueryAsync_WithScalarAlias_OrderByAlias_Verbatim"/>: on Apache AGE
    /// &lt; 1.7.0 the same query is REJECTED, because ORDER BY never resolved a WITH-defined
    /// alias (upstream apache/age#2269). Asserted so the CI PG16/PG17 cells keep proving the
    /// bug still exists rather than silently skipping — without this, the two
    /// <see cref="OrderByAliasFactAttribute"/> tests would be the only signal and they are
    /// skipped on exactly those cells.
    ///
    /// Two conditions keep this honest:
    /// <list type="bullet">
    ///   <item>Only asserted where the alias genuinely fails (AGE &lt; 1.7.0); elsewhere it
    ///   skips rather than asserting a success that has nothing to prove.</item>
    ///   <item>It pins the error the engine actually raised — <c>42703</c> with "could not find
    ///   rte" — so a future driver or AGE change that alters the failure turns this red
    ///   instead of quietly flipping a documented limitation into a pass.</item>
    /// </list>
    /// </summary>
    [OrderByAliasUnresolvedFact]
    public async Task OrderByAliasNotResolved_FailsOnAgeBefore17()
    {
        await IntializeAsync();
        await SeedTwinsAsync();

        string graph = Client.GetGraphName();
        string query =
            $$"""
              MATCH (t:Twin)
              WHERE {{graph}}.is_of_model(t, '{{PrimaryModel}}')
              WITH t, toInteger(t.returnPeriod) AS rp
              ORDER BY rp ASC
              RETURN t { .*, zoomable: false } AS twin
              """;

        var exception = await Record.ExceptionAsync(async () =>
        {
            var page = await Client.QueryAsync<JsonDocument>(query).AsPages().FirstAsync();
            Assert.NotNull(page);
        });

        Assert.NotNull(exception);
        var sqlException = Assert.IsType<Npgsql.PostgresException>(exception);
        Assert.Equal(PostgresErrorCodes.UndefinedColumn, sqlException.SqlState);
        Assert.Contains("could not find rte for rp", sqlException.Message);
    }

    /// <summary>
    /// Sorting by an alias placed AFTER the RETURN, with pagination engaged.
    ///
    /// This is the shape the #113 workaround suggested, and it is a trap: Cypher's trailing
    /// clause set is <c>ORDER BY</c>, and the pagination rewrite appends <c>SKIP</c>/<c>LIMIT</c>
    /// at the very END of the query — i.e. AFTER that <c>ORDER BY</c>. Measured on Apache AGE
    /// 1.7.0 the rewritten query is still accepted, so this asserts the sorted-across-pages
    /// result rather than an error: the rewrite preserves the user's requested ordering.
    ///
    /// Kept as an explicit test (rather than deleting the shape) because it is the behaviour a
    /// user following the workaround will hit, and because a future AGE or driver change that
    /// makes the rewritten clause order invalid would turn this red instead of silently
    /// breaking every paginated query that ends in ORDER BY.
    /// </summary>
    [Fact]
    public async Task QueryAsync_OrderByAfterReturn_Paginated()
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

        var periods = await RunAsync(query, pageSize: 2, alias: "twin");

        Assert.Equal(ExpectedAscending, periods.OrderBy(p => int.Parse(p)).ToArray());
    }

    /// <summary>
    /// Sorting by a node property with a full map projection, paginated. Exercises the sort key
    /// surviving the rewrite end to end.
    /// </summary>
    [Fact]
    public async Task QueryAsync_OrderByNodeProperty_MapProjection_Paginated()
    {
        await IntializeAsync();
        await SeedTwinsAsync();

        string graph = Client.GetGraphName();
        string query =
            $$"""
              MATCH (t:Twin)
              WHERE {{graph}}.is_of_model(t, '{{PrimaryModel}}')
              WITH t
              ORDER BY toInteger(t.returnPeriod) DESC
              RETURN t { .*, zoomable: false } AS twin
              """;

        var periods = await RunAsync(query, pageSize: 1, alias: "twin");

        Assert.Equal(new[] { "2050", "2020", "1990" }, periods);
    }

    /// <summary>
    /// Two RETURN shapes cannot round-trip through the AGE driver at all: it emits an empty
    /// column-definition list (<c>... $$) as ( agtype)</c>), which PostgreSQL rejects with
    /// 42601. This affects a bare scalar return item (<c>RETURN t.returnPeriod AS period</c>) and
    /// a map projection without <c>.*</c> (<c>RETURN t { .returnPeriod } AS twin</c>) — with or
    /// without ORDER BY, so it is independent of the pagination rewrite. Tracked in #115;
    /// deliberately not asserted here rather than encoded as expected behaviour.
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
    /// Seed twins in deliberately scrambled returnPeriod order, across both models so
    /// <c>is_of_model</c> descendant inheritance is exercised. Natural scan order is
    /// 2050, 1990, 2020, so an unsorted result is distinguishable from a sorted one.
    /// </summary>
    private async Task SeedTwinsAsync()
    {
        await Client.CreateOrReplaceDigitalTwinAsync(
            "widgetC",
            $$"""
              {"$dtId": "widgetC", "$metadata": {"$model": "{{PrimaryModel}}"}, "returnPeriod": "2050"}
              """
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "widgetA",
            $$"""
              {"$dtId": "widgetA", "$metadata": {"$model": "{{PrimaryModel}}"}, "returnPeriod": "1990"}
              """
        );
        await Client.CreateOrReplaceDigitalTwinAsync(
            "widgetB",
            $$"""
              {"$dtId": "widgetB", "$metadata": {"$model": "{{DerivedModel}}"}, "returnPeriod": "2020"}
              """
        );
    }
}
using AgeDigitalTwins.Events;
using AgeDigitalTwins.Events.Abstractions;
using AgeDigitalTwins.Events.Core.Events;
using AgeDigitalTwins.Events.Core.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Register the shared event queue as a singleton
builder.Services.AddSingleton<IEventQueue, EventQueue>();

// Register TelemetryListener as a singleton
builder.Services.AddSingleton(sp =>
{
    string connectionString =
        builder.Configuration.GetConnectionString("agedb")
        ?? builder.Configuration["ConnectionStrings:agedb"]
        ?? builder.Configuration["AgeConnectionString"]
        ?? throw new InvalidOperationException("Connection string is required.");

    var eventQueue = sp.GetRequiredService<IEventQueue>();
    var logger = sp.GetRequiredService<ILogger<TelemetryListener>>();

    return new TelemetryListener(connectionString, eventQueue, logger);
});

// Register SharedEventConsumer as a singleton
builder.Services.AddSingleton(sp =>
{
    var eventQueue = sp.GetRequiredService<IEventQueue>();
    var logger = sp.GetRequiredService<ILogger<SharedEventConsumer>>();

    string? source =
        builder.Configuration.GetSection("Parameters")["CustomEventSource"]
        ?? builder.Configuration["Parameters:CustomEventSource"]
        ?? builder.Configuration["CustomEventSource"];

    Uri sourceUri;
    if (!string.IsNullOrEmpty(source))
    {
        if (!Uri.TryCreate(source, UriKind.RelativeOrAbsolute, out sourceUri!))
        {
            UriBuilder uriBuilder = new(source);
            sourceUri = uriBuilder.Uri;
        }
    }
    else
    {
        string connectionString =
            builder.Configuration.GetConnectionString("agedb")
            ?? builder.Configuration["ConnectionStrings:agedb"]
            ?? builder.Configuration["AgeConnectionString"]
            ?? throw new InvalidOperationException("Connection string is required.");

        NpgsqlConnectionStringBuilder csb = new(connectionString);
        UriBuilder uriBuilder = new() { Scheme = "postgresql", Host = csb.Host };
        sourceUri = uriBuilder.Uri;
    }

    return new SharedEventConsumer(eventQueue, logger, sourceUri);
});

// Register EventSinkFactory as a singleton
builder.Services.AddSingleton(sp =>
{
    ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var dlqService = sp.GetRequiredService<DLQService>();
    return new EventSinkFactory(builder.Configuration, loggerFactory, dlqService);
});

// Register event sinks as a singleton list
builder.Services.AddSingleton(sp =>
{
    var eventSinkFactory = sp.GetRequiredService<EventSinkFactory>();
    return eventSinkFactory.CreateEventSinks();
});

// Register event routes as a singleton list
builder.Services.AddSingleton(sp =>
{
    var eventSinkFactory = sp.GetRequiredService<EventSinkFactory>();
    return eventSinkFactory.GetEventRoutes();
});

// Register DLQService as a singleton
builder.Services.AddSingleton(sp =>
{
    string connectionString =
        builder.Configuration.GetConnectionString("agedb")
        ?? builder.Configuration["ConnectionStrings:agedb"]
        ?? builder.Configuration["AgeConnectionString"]
        ?? throw new InvalidOperationException("Connection string is required.");

    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    var dataSource = dataSourceBuilder.Build();
    var logger = sp.GetRequiredService<ILogger<DLQService>>();
    return new DLQService(dataSource, logger);
});

// Register Subscription as a singleton
builder.Services.AddSingleton(sp =>
{
    string connectionString =
        builder.Configuration.GetConnectionString("agedb")
        ?? builder.Configuration["ConnectionStrings:agedb"]
        ?? builder.Configuration["AgeConnectionString"]
        ?? throw new InvalidOperationException("Connection string is required.");

    string publication =
        builder.Configuration.GetSection("Parameters")["AgePublication"]
        ?? builder.Configuration["Parameters:AgePublication"]
        ?? builder.Configuration["AgePublication"]
        ?? "age_pub";

    string replicationSlot =
        builder.Configuration.GetSection("Parameters")["AgeReplicationSlot"]
        ?? builder.Configuration["Parameters:AgeReplicationSlot"]
        ?? builder.Configuration["AgeReplicationSlot"]
        ?? "age_slot";

    string? source =
        builder.Configuration.GetSection("Parameters")["CustomEventSource"]
        ?? builder.Configuration["Parameters:CustomEventSource"]
        ?? builder.Configuration["CustomEventSource"];

    int maxBatchSize =
        builder.Configuration.GetSection("Parameters").GetValue<int?>("MaxBatchSize")
        ?? builder.Configuration.GetValue<int?>("Parameters:MaxBatchSize")
        ?? builder.Configuration.GetValue<int?>("MaxBatchSize")
        ?? 50;

    // How long to wait without a replication message before declaring the connection dead.
    // Keep this shorter than CNPG's WAL-receiver drain timeout so the WAL receiver slot is
    // released in time for a standby to be promoted. Default: 30 s.
    int walReceiverTimeoutSeconds =
        builder.Configuration.GetSection("Parameters").GetValue<int?>("WalReceiverTimeoutSeconds")
        ?? builder.Configuration.GetValue<int?>("Parameters:WalReceiverTimeoutSeconds")
        ?? builder.Configuration.GetValue<int?>("WalReceiverTimeoutSeconds")
        ?? 30;

    ILogger<AgeDigitalTwinsReplication> subscriptionLogger = sp.GetRequiredService<
        ILogger<AgeDigitalTwinsReplication>
    >();

    // Get the shared event queue from DI
    var eventQueue = sp.GetRequiredService<IEventQueue>();

    return new AgeDigitalTwinsReplication(
        connectionString,
        publication,
        replicationSlot,
        eventQueue,
        subscriptionLogger,
        maxBatchSize,
        walReceiverTimeoutSeconds
    );
});

// Add health checks
builder
    .Services.AddHealthChecks()
    .AddCheck<ReplicationHealthCheck>("replication", tags: ["live"])
    .AddCheck<EventSinksHealthCheck>("event_sinks", tags: ["live"]);

builder.Services.AddRequestTimeouts();
builder.Services.AddOutputCache();

var app = builder.Build();

// Resolve the singleton instances
var subscription = app.Services.GetRequiredService<AgeDigitalTwinsReplication>();
var telemetryListener = app.Services.GetRequiredService<TelemetryListener>();
var sharedEventConsumer = app.Services.GetRequiredService<SharedEventConsumer>();
var eventSinks = app.Services.GetRequiredService<List<IEventSink>>();
var eventRoutes = app.Services.GetRequiredService<List<EventRoute>>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

var cts = new CancellationTokenSource();

app.Lifetime.ApplicationStarted.Register(async () =>
{
    try
    {
        logger.LogInformation("Starting event processing services...");

        // Start all services concurrently
        var replicationTask = subscription.RunAsync(cts.Token);
        var telemetryTask = telemetryListener.RunAsync(cts.Token);

        // Start shared event consumer with event sinks and routes from DI
        var consumerTask = sharedEventConsumer.ConsumeEventsAsync(
            eventSinks,
            eventRoutes,
            cts.Token
        );

        // Initialize DLQ schema/table
        var dlqService = app.Services.GetRequiredService<DLQService>();
        await dlqService.InitializeSchemaAsync(cts.Token);
        logger.LogInformation("All event processing services started successfully");

        // Wait for any service to complete (which should only happen on cancellation or error)
        await Task.WhenAny(replicationTask, telemetryTask, consumerTask);
    }
    catch (Exception ex)
    {
        // Log the exception and exit
        logger.LogError(ex, "Error while running the event processing services.");
        app.Lifetime.StopApplication();
    }
});

app.Lifetime.ApplicationStopping.Register(async () =>
{
    logger.LogInformation("Stopping event processing services...");
    cts.Cancel();

    // Wait for the services to finish processing before disposing
    await subscription.DisposeAsync();
    logger.LogInformation("Event processing services stopped");
});

app.UseRequestTimeouts();
app.UseOutputCache();

app.MapDefaultEndpoints();

app.Run();

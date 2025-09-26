using AgeDigitalTwins.Events;
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

    ILogger<AgeDigitalTwinsReplication> subscriptionLogger = sp.GetRequiredService<
        ILogger<AgeDigitalTwinsReplication>
    >();
    ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    EventSinkFactory eventSinkFactory = new(builder.Configuration, loggerFactory);

    // Get the shared event queue from DI
    var eventQueue = sp.GetRequiredService<IEventQueue>();

    return new AgeDigitalTwinsReplication(
        connectionString,
        publication,
        replicationSlot,
        eventQueue,
        subscriptionLogger,
        maxBatchSize
    );
});

// Add replication health check
builder.Services.AddHealthChecks().AddCheck<ReplicationHealthCheck>("replication", tags: ["live"]);

builder.Services.AddRequestTimeouts();
builder.Services.AddOutputCache();

var app = builder.Build();

// Resolve the singleton instances
var subscription = app.Services.GetRequiredService<AgeDigitalTwinsReplication>();
var telemetryListener = app.Services.GetRequiredService<TelemetryListener>();
var sharedEventConsumer = app.Services.GetRequiredService<SharedEventConsumer>();
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

        // Start shared event consumer with event sinks and routes
        var eventSinkFactory = new EventSinkFactory(
            builder.Configuration,
            app.Services.GetRequiredService<ILoggerFactory>()
        );
        var eventSinks = eventSinkFactory.CreateEventSinks();
        var eventRoutes = eventSinkFactory.GetEventRoutes();
        var consumerTask = sharedEventConsumer.ConsumeEventsAsync(
            eventSinks,
            eventRoutes,
            cts.Token
        );

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

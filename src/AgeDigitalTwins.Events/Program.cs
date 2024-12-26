using AgeDigitalTwins.Events;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

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

    ILogger<AgeDigitalTwinsReplication> subscriptionLogger = sp.GetRequiredService<
        ILogger<AgeDigitalTwinsReplication>
    >();
    ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    EventSinkFactory eventSinkFactory = new(builder.Configuration, loggerFactory);

    return new AgeDigitalTwinsReplication(
        connectionString,
        publication,
        replicationSlot,
        source,
        eventSinkFactory,
        subscriptionLogger
    );
});

builder.Services.AddRequestTimeouts();
builder.Services.AddOutputCache();

var app = builder.Build();

// Resolve the singleton instance and start the subscription
var subscription = app.Services.GetRequiredService<AgeDigitalTwinsReplication>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

var cts = new CancellationTokenSource();

app.Lifetime.ApplicationStarted.Register(async () =>
{
    try
    {
        await subscription.StartAsync(cts.Token);
    }
    catch (Exception ex)
    {
        // Log the exception and exit
        logger.LogError(ex, "Failed to start subscription");
        app.Lifetime.StopApplication();
    }
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    cts.Cancel();
    subscription.Stop(); // Assuming Stop method exists to stop the subscription gracefully
});

app.UseRequestTimeouts();
app.UseOutputCache();

app.MapDefaultEndpoints();

app.Run();

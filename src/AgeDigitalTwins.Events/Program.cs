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

    ILogger<AgeDigitalTwinsSubscription> subscriptionLogger = sp.GetRequiredService<
        ILogger<AgeDigitalTwinsSubscription>
    >();
    ILogger<EventSinkFactory> eventSinkFactoryLogger = sp.GetRequiredService<
        ILogger<EventSinkFactory>
    >();

    EventSinkFactory eventSinkFactory = new(builder.Configuration, eventSinkFactoryLogger);

    return new AgeDigitalTwinsSubscription(
        connectionString,
        publication,
        replicationSlot,
        eventSinkFactory,
        subscriptionLogger
    );
});

var app = builder.Build();

// Resolve the singleton instance and start the subscription
var subscription = app.Services.GetRequiredService<AgeDigitalTwinsSubscription>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    await subscription.StartAsync();
}
catch (Exception ex)
{
    // Log the exception and exit
    logger.LogError(ex, "Failed to start subscription");
    return;
}

app.MapDefaultEndpoints();

app.Run();

using AgeDigitalTwins.Events;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Register Subscription as a singleton
builder.Services.AddSingleton(sp =>
    new AgeDigitalTwinsSubscription("your_connection_string", "your_publication", "your_replication_slot"));

var app = builder.Build();

// Resolve the singleton instance and start the subscription
var subscription = app.Services.GetRequiredService<AgeDigitalTwinsSubscription>();
await subscription.StartAsync();

app.MapDefaultEndpoints();

app.Run();

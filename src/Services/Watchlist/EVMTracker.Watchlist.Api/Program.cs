using EVMTracker.Messaging;
using EVMTracker.Observability;
using EVMTracker.Watchlist.Api.Endpoints;
using EVMTracker.Watchlist.Application;
using EVMTracker.Watchlist.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceDefaults();
builder.Services.AddWatchlistApplication();

string connectionString = builder.Configuration.GetConnectionString("Watchlist")
    ?? throw new InvalidOperationException(
        "The ConnectionStrings:Watchlist setting is required.");
builder.Services.AddWatchlistInfrastructure(connectionString);
builder.Services.AddRabbitMqMessaging(builder.Configuration);
builder.Services.AddOutboxDispatcher(builder.Configuration);

var app = builder.Build();

app.MapServiceDefaults();
app.MapWatchlistEndpoints();
app.MapGet(
    "/",
    () => Results.Ok(
        new
        {
            Service = "EVMTracker.Watchlist.Api",
            Status = "running",
        }));

app.Run();

public partial class Program;

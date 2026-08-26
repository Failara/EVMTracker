using EVMTracker.Notification.Application;
using EVMTracker.Notification.Infrastructure;
using EVMTracker.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceDefaults();
builder.Services.AddNotificationApplication();
string connectionString = builder.Configuration.GetConnectionString("Notification")
    ?? throw new InvalidOperationException(
        "The ConnectionStrings:Notification setting is required.");
builder.Services.AddNotificationInfrastructure(
    builder.Configuration,
    connectionString);

var app = builder.Build();

app.MapServiceDefaults();
app.MapGet(
    "/",
    () => Results.Ok(
        new
        {
            Service = "EVMTracker.Notification.Worker",
            Status = "running",
        }));

app.Run();

public partial class Program;

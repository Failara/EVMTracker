using EVMTracker.Messaging;
using EVMTracker.Notification.Application;
using EVMTracker.Notification.Infrastructure;
using EVMTracker.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceDefaults(
    builder.Configuration,
    "EVMTracker.Notification.Worker");
builder.Services.AddNotificationApplication();
string connectionString = builder.Configuration.GetConnectionString("Notification") ?? string.Empty;
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "The ConnectionStrings:Notification setting is required.");
}
builder.Services.AddNotificationInfrastructure(
    builder.Configuration,
    connectionString);
builder.Services.AddOutboxDispatcher(builder.Configuration);

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

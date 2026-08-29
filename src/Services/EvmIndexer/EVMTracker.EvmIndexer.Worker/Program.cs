using EVMTracker.EvmIndexer.Infrastructure;
using EVMTracker.Messaging;
using EVMTracker.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceDefaults(
    builder.Configuration,
    "EVMTracker.EvmIndexer.Worker");
string connectionString = builder.Configuration.GetConnectionString("EvmIndexer") ?? string.Empty;
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "The ConnectionStrings:EvmIndexer setting is required.");
}
builder.Services.AddEvmIndexerInfrastructure(builder.Configuration, connectionString);
builder.Services.AddOutboxDispatcher(builder.Configuration);

var app = builder.Build();

app.MapServiceDefaults();
app.MapGet(
    "/",
    () => Results.Ok(
        new
        {
            Service = "EVMTracker.EvmIndexer.Worker",
            Status = "running",
        }));

app.Run();

public partial class Program;

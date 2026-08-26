using EVMTracker.EvmIndexer.Infrastructure;
using EVMTracker.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceDefaults();
string connectionString = builder.Configuration.GetConnectionString("EvmIndexer")
    ?? throw new InvalidOperationException(
        "The ConnectionStrings:EvmIndexer setting is required.");
builder.Services.AddEvmIndexerInfrastructure(builder.Configuration, connectionString);

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

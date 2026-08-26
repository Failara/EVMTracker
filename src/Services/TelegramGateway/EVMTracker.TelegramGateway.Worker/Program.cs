using EVMTracker.Observability;
using EVMTracker.TelegramGateway.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceDefaults();
builder.Services.AddTelegramGatewayInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapServiceDefaults();
app.MapGet(
    "/",
    () => Results.Ok(
        new
        {
            Service = "EVMTracker.TelegramGateway.Worker",
            Status = "running",
        }));

app.Run();

public partial class Program;

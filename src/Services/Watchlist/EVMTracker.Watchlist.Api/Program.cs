using EVMTracker.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceDefaults();

var app = builder.Build();

app.MapServiceDefaults();
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

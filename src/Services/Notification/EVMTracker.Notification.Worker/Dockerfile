# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/Services/Notification/EVMTracker.Notification.Worker/EVMTracker.Notification.Worker.csproj --configfile NuGet.Config
RUN dotnet publish src/Services/Notification/EVMTracker.Notification.Worker/EVMTracker.Notification.Worker.csproj --configuration Release --no-restore --output /app/publish --property:UseAppHost=false

FROM build AS migrations
RUN dotnet tool restore --configfile NuGet.Config
RUN dotnet build src/Services/Notification/EVMTracker.Notification.Infrastructure/EVMTracker.Notification.Infrastructure.csproj --configuration Release --no-restore
ENTRYPOINT ["dotnet", "tool", "run", "dotnet-ef", "database", "update", "--no-build", "--project", "src/Services/Notification/EVMTracker.Notification.Infrastructure", "--startup-project", "src/Services/Notification/EVMTracker.Notification.Infrastructure", "--context", "NotificationDbContext"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=6 \
    CMD ["curl", "--fail", "--silent", "--show-error", "http://localhost:8080/health/live"]
ENTRYPOINT ["dotnet", "EVMTracker.Notification.Worker.dll"]

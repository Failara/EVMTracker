namespace EVMTracker.TelegramGateway.UnitTests;

public sealed class ModuleSmokeTests
{
    [Fact]
    public void ApplicationAssemblyCanBeLoaded()
    {
        string? assemblyName = typeof(Application.AssemblyReference).Assembly.GetName().Name;

        Assert.Equal("EVMTracker.TelegramGateway.Application", assemblyName);
    }
}

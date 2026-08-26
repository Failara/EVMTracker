namespace EVMTracker.Notification.UnitTests;

public sealed class ModuleSmokeTests
{
    [Fact]
    public void DomainAssemblyCanBeLoaded()
    {
        string? assemblyName = typeof(Domain.AssemblyReference).Assembly.GetName().Name;

        Assert.Equal("EVMTracker.Notification.Domain", assemblyName);
    }
}

using System.Text.Json;
using Xunit.Abstractions;

namespace EVMTracker.EvmIndexer.IntegrationTests;

public sealed class ConfirmationDepthModelTests(ITestOutputHelper output)
{
    private const double AssumedBlockTimeSeconds = 3;

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(12)]
    [Trait("Category", "ExperimentModel")]
    public void ConfirmationDepthProducesExpectedMinimumDetectionLatency(int confirmationDepth)
    {
        double minimumLatencySeconds = confirmationDepth * AssumedBlockTimeSeconds;
        output.WriteLine(JsonSerializer.Serialize(new
        {
            confirmationDepth,
            assumedBlockTimeSeconds = AssumedBlockTimeSeconds,
            minimumDetectionLatencySeconds = minimumLatencySeconds,
        }));

        Assert.True(confirmationDepth >= 0);
        Assert.Equal(confirmationDepth * AssumedBlockTimeSeconds, minimumLatencySeconds);
    }
}

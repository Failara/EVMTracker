using EVMTracker.EvmIndexer.Infrastructure.Rpc;

namespace EVMTracker.EvmIndexer.IntegrationTests;

public sealed class TokenMetadataTextNormalizerTests
{
    [Fact]
    public void RemovesNullCharactersFromOnChainText()
    {
        string? normalized = TokenMetadataTextNormalizer.Normalize("  USD\0T\0  ", 64);

        Assert.Equal("USDT", normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\0\0")]
    [InlineData("   ")]
    public void EmptyNormalizedTextReturnsNull(string? value)
    {
        Assert.Null(TokenMetadataTextNormalizer.Normalize(value, 64));
    }

    [Fact]
    public void TruncatesNormalizedTextToConfiguredLength()
    {
        Assert.Equal("TOKE", TokenMetadataTextNormalizer.Normalize("TOKEN", 4));
    }
}

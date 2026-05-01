using NodaTime;
namespace YahooQuotesApi.HistoryTest;

public class IntervalsTests : XunitTestBase
{
    private YahooQuotes YahooQuotes { get; }

    public IntervalsTests(ITestOutputHelper output) : base(output)
    {
        YahooQuotes = new YahooQuotesBuilder()
            .WithLogger(Logger)
            .WithHistoryStartDate(Instant.FromUtc(2026, 4, 20, 17, 20, 0))
            .WithHistoryEndDate(Instant.FromUtc(2026, 4, 20, 17, 30, 0))
            .DoNotUseAdjustedClose()
            .Build();
    }

    [Theory]
    [InlineData("YPFD.BA")]
    public async Task OneMinuteInterval(string symbol, string  baseSymbol = "")
    {
        Result<History> result = await YahooQuotes
            .GetHistoryAsync(symbol, baseSymbol, "1m", TestContext.Current.CancellationToken);

        History history = result.Value;

        Assert.Equal(symbol, history.Symbol.Name);
        Assert.False(history.Ticks.IsDefaultOrEmpty);
        Assert.Equal(11, history.Ticks.Length); // 10 minutes span + 1
    }
}

using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Threading.Tasks;

namespace YahooQuotesApi.Demo;

public static class Program
{
    public static async Task Main()
    {
        Console.WriteLine("Please wait...");

        ILogger logger = LoggerFactory
            .Create(x => x
                .AddSimpleConsole(x => x.SingleLine = false)
                .SetMinimumLevel(LogLevel.Trace))
            .CreateLogger("Demo");

        //await new MyApp(logger).Run(10000, HistoryFlags.None, "");
        await new MyApp(logger).Run(100, "JPY=X");

        await Task.Delay(3000);

        Console.WriteLine("Completed!");

        // Testing 1m intervals for YPFD.BA (Arg), mirroring Yahoo chart API
        long period1 = 1776705600L; // 2026-04-20T17:20:00Z
        long period2 = 1777051800L;   // 2026-04-24T17:30:00Z

        Instant start = Instant.FromUnixTimeSeconds(period1);
        Instant end = Instant.FromUnixTimeSeconds(period2);

        YahooQuotes dailyYQ = new YahooQuotesBuilder()
            .WithLogger(logger)
            .WithHistoryStartDate(start)
            .WithHistoryEndDate(end)
            .Build();

        var results = await dailyYQ.GetHistoryAsync("YPFD.BA", "", "1m");

        if(results.HasError)
        {
            logger.LogError("Error fetching history: {Error}", results.Error);
        }
        else
        {
            Console.WriteLine($"History for YPFD.BA (1m intervals):");

            foreach (var tick in results.Value.Ticks)
            {
                Console.WriteLine($"Date: {tick.Date}, Open: {tick.Open}, High: {tick.High}, Low: {tick.Low}, Close: {tick.Close}, AdjClose: {tick.AdjustedClose}, Volume: {tick.Volume}");
            }
        }

        Console.WriteLine("Press a key to exit...");
        Console.ReadLine();
    }
}

using Microsoft.Extensions.Logging;
using NodaTime;
using System;
using System.Collections.Generic;
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

        // testing 1m intervals for YPFD.BA (Arg)
        // It is good practice to use 'long' for Unix timestamps to avoid overflow
        long startTimestamp = 1776705600L;
        long endTimestamp = 1777051800L;

        // Parse to NodaTime.Instant
        Instant start = Instant.FromUnixTimeSeconds(startTimestamp);
        Instant end = Instant.FromUnixTimeSeconds(endTimestamp);

        YahooQuotes dailyYQ = new YahooQuotesBuilder()
            .WithLogger(logger)
            .WithHistoryStartDate(start)
            .Build();

        var results = await dailyYQ.GetHistoryAsync("YPFD.BA", "YPFD.BA", "1m");

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

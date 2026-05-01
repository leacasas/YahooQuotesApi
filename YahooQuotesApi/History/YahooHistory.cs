using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace YahooQuotesApi;

public sealed class YahooHistory
{
    private ILogger Logger { get; }
    private CookieAndCrumb CookieAndCrumb { get; }
    private Instant Start { get; }
    private Instant End { get; }
    private IHttpClientFactory HttpClientFactory { get; }
    private HistoryBasePricesCreator HistoryBasePricesCreator { get; }
    private HistoryCreator HistoryCreator { get; }
    private ParallelProducerCache<Symbol, Result<History>> Cache { get; }

    public YahooHistory(ILogger logger, YahooQuotesBuilder builder, CookieAndCrumb crumbService, HistoryCreator hc, HistoryBasePricesCreator hbc, IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        Logger = logger;
        CookieAndCrumb = crumbService;
        Start = builder.HistoryStartDate;
        End = builder.HistoryEndDate;
        HttpClientFactory = httpClientFactory;
        HistoryCreator = hc;
        HistoryBasePricesCreator = hbc;
        Cache = new(builder.Clock, builder.HistoryCacheDuration);
    }

    internal async Task<Dictionary<Symbol, Result<History>>> GetHistoryAsync(IEnumerable<Symbol> syms, Symbol baseSymbol, string interval, CancellationToken ct)
    {
        HashSet<Symbol> symbols = [.. syms];

        if (symbols.Any(s => s.IsCurrencyRate))
            throw new ArgumentException($"Invalid symbol: {symbols.First(s => s.IsCurrencyRate)}.");
        if (baseSymbol != default && baseSymbol.IsCurrencyRate)
            throw new ArgumentException($"Invalid base symbol: {baseSymbol}.");
        if (baseSymbol == default && symbols.Any(s => s.IsCurrency))
            throw new ArgumentException($"Base symbol required.");
        try
        {
            return await GetHistoryAsync(symbols, baseSymbol, interval, ct).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.LogCritical(e, "GetHistoryAsync() error.");
            throw;
        }
    }

    private async Task<Dictionary<Symbol, Result<History>>> GetHistoryAsync(HashSet<Symbol> symbols, Symbol baseSymbol, string interval, CancellationToken ct)
    {
        HashSet<Symbol> stockSymbols = [.. symbols.Where(s => s.IsStock)];
        if (baseSymbol != default && baseSymbol.IsStock)
            stockSymbols.Add(baseSymbol);

        Dictionary<Symbol, Result<History>> results = [];

        await AddToResultsAsync(stockSymbols, results, interval, ct).ConfigureAwait(false);   
        await AddCurrenciesToResults(symbols, baseSymbol, results, interval, ct).ConfigureAwait(false);

        return HistoryBasePricesCreator.Create(symbols, baseSymbol, results);
    }

    private async Task AddCurrenciesToResults(IEnumerable<Symbol> symbols, Symbol baseSymbol, Dictionary<Symbol, Result<History>> results, string interval, CancellationToken ct)
    {
        // currencies + historyBase currency + history currencies
        List<Symbol> currencySymbols = [];
        foreach (var symbol in symbols.Where(s => s.IsCurrency))
        {
            currencySymbols.Add(symbol);
            results[symbol] = HistoryCreator.CreateFromSymbol(symbol).ToResult();
        }

        if (baseSymbol.IsValid)
        {
            if (baseSymbol.IsCurrency)
                currencySymbols.Add(baseSymbol);
            foreach (History stock in results.Values.Where(r => r.HasValue).Select(r => r.Value).Where(h => h.Currency.IsValid))
                currencySymbols.Add(stock.Currency);
        }

        HashSet<Symbol> rateSymbols = [.. currencySymbols
            .Where(c => c.Name != "USD=X")
            .Select(c => $"USD{c}".ToSymbol())];

        await AddToResultsAsync(rateSymbols, results, interval, ct).ConfigureAwait(false);
    }

    private async Task AddToResultsAsync(IEnumerable<Symbol> symbols, Dictionary<Symbol, Result<History>> results, string interval, CancellationToken ct)
    {
        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(symbols, parallelOptions, async (symbol, ct) =>
        {
            Result<History> result = await Cache.Get(symbol, () => Produce(symbol, interval, ct)).ConfigureAwait(false);
            lock (results)
            {
                results.Add(symbol, result);
            }
        }).ConfigureAwait(false);
    }

    private async Task<Result<History>> Produce(Symbol symbol, string interval, CancellationToken ct)
    {
        var (cookies, crumb) = await CookieAndCrumb.Get(ct).ConfigureAwait(false);
        Uri uri = GetUri(symbol, crumb, interval);
        Logger.LogInformation("{Uri}", uri.ToString());

        HttpClient httpClient = HttpClientFactory.CreateClient("HttpV2");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.Add("Cookie", cookies);

        using HttpResponseMessage response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        string? contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType != "application/json")
        {
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException e)
            {
                return Result<History>.Fail(e);
            }
            return Result<History>.Fail($"Invalid content type: {contentType}.");
        }

        using Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, default, ct).ConfigureAwait(false);
        return HistoryCreator.CreateFromJson(doc, symbol.Name);
    }

    private Uri GetUri(Symbol symbol, string crumb, string interval)
    {
        string url = $"https://query2.finance.yahoo.com/v8/finance/chart/{symbol}?" +
            "events=history,div,split" +
            $"&interval={interval}" +
            $"&period1={Start.ToUnixTimeSeconds()}" +
            $"&period2={(End == default ? Instant.MaxValue.ToUnixTimeSeconds() : End.ToUnixTimeSeconds())}" +
            $"&crumb={crumb}";
        return new Uri(url);
    }
}

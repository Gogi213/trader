using Mexc.Net.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trader.Core.Models;
using Trader.ExchangeApi.Abstractions;
using Trader.Scanner.Abstractions;
using Trader.Scanner.Models;

namespace Trader.Scanner;

public class MarketScannerService : IMarketScannerService
{
    private readonly IMexcRestApiClient _apiClient;
    private readonly ILogger<MarketScannerService> _logger;
    private readonly ScannerOptions _options;

    public MarketScannerService(IMexcRestApiClient apiClient, ILogger<MarketScannerService> logger, IOptions<ScannerOptions> options)
    {
        _apiClient = apiClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<TradingSymbol?> FindBestSymbolAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting market scan...");

        // 1. Get all symbols
        var exchangeInfoResult = await _apiClient.GetExchangeInfoAsync(ct);
        if (!exchangeInfoResult.Success) return null;

        var allSymbols = exchangeInfoResult.Data.Symbols.Where(s => s.QuoteAsset == "USDT" && s.Status == SymbolStatus.Enabled).Select(s => s.Name);

        // 2. Filter by 24h Volume
        var tickersResult = await _apiClient.GetTickersAsync(ct: ct);
        if (!tickersResult.Success) return null;

        var highVolumeSymbols = tickersResult.Data
            .Where(t => allSymbols.Contains(t.Symbol) && t.QuoteVolume > _options.Min24hVolumeUsdt)
            .Select(t => t.Symbol);

        _logger.LogInformation("Found {Count} symbols with volume > {Volume} USDT in 24h.", highVolumeSymbols.Count(), _options.Min24hVolumeUsdt);

        var candidates = new List<TradingSymbol>();

        foreach (var symbol in highVolumeSymbols)
        {
            // 3. Filter by 15m Volume
            var klinesResult = await _apiClient.GetKlinesAsync(symbol, KlineInterval.FifteenMinutes, limit: 1, ct: ct);
            if (!klinesResult.Success || !klinesResult.Data.Any()) continue;

            if (klinesResult.Data.First().QuoteVolume < _options.Min15mVolumeUsdt) continue;

            // 4. Filter by Spread
            var orderBookResult = await _apiClient.GetOrderBookAsync(symbol, 5, ct);
            if (!orderBookResult.Success || !orderBookResult.Data.Bids.Any() || !orderBookResult.Data.Asks.Any()) continue;

            var bestBid = orderBookResult.Data.Bids.First().Price;
            var bestAsk = orderBookResult.Data.Asks.First().Price;
            var spread = (bestAsk - bestBid) / bestBid * 100;

            if (spread < _options.MinSpreadPercentage) continue;

            // 5. Trend Check (Simple version)
            if (!await IsInSidewaysTrend(symbol, ct))
            {
                _logger.LogDebug("Symbol {Symbol} skipped due to trend.", symbol);
                continue;
            }

            candidates.Add(new TradingSymbol(symbol, spread, klinesResult.Data.First().QuoteVolume));
        }

        if (!candidates.Any())
        {
            _logger.LogWarning("No suitable trading symbols found after all filters.");
            return null;
        }

        // 6. Sort by spread
        var bestCandidate = candidates.OrderByDescending(c => c.Spread).First();
        _logger.LogInformation("Best candidate found: {Symbol} with spread {Spread}%", bestCandidate.Name, bestCandidate.Spread);

        return bestCandidate;
    }

    private async Task<bool> IsInSidewaysTrend(string symbol, CancellationToken ct)
    {
        // Simple sentiment check: price should be stable across different timeframes.
        var klines15m = await _apiClient.GetKlinesAsync(symbol, KlineInterval.FifteenMinutes, limit: 4, ct: ct); // last hour
        if (!klines15m.Success || !klines15m.Data.Any()) return false;

        var prices = klines15m.Data.Select(k => k.ClosePrice).ToList();
        var maxPrice = prices.Max();
        var minPrice = prices.Min();

        // If the price change in the last hour is more than 1.5%, consider it a trend.
        if ((maxPrice - minPrice) / minPrice * 100 > 1.5m)
        {
            return false;
        }

        return true;
    }
}
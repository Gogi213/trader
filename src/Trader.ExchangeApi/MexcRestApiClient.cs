using CryptoExchange.Net.Objects;
using Mexc.Net.Clients;
using Mexc.Net.Enums;
using Mexc.Net.Interfaces.Clients;
using Mexc.Net.Objects.Models;
using Mexc.Net.Objects.Models.Spot;
using Microsoft.Extensions.Logging;
using Trader.ExchangeApi.Abstractions;

namespace Trader.ExchangeApi;

/// <summary>
/// Adapter for the MEXC REST API client.
/// It wraps the underlying Mexc.Net library to provide a simplified and controlled interface
/// for the rest of the application.
/// </summary>
public class MexcRestApiClient : IMexcRestApiClient
{
    private readonly IMexcRestClient _client;
    private readonly ILogger<MexcRestApiClient> _logger;

    public MexcRestApiClient(IMexcRestClient client, ILogger<MexcRestApiClient> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<WebCallResult<MexcExchangeInfo>> GetExchangeInfoAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching exchange info...");
        var result = await _client.SpotApi.ExchangeData.GetExchangeInfoAsync(null, ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to get exchange info: {Error}", result.Error);
        }
        return result;
    }

    public async Task<WebCallResult<IEnumerable<MexcTicker>>> GetTickersAsync(string? symbol = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching tickers for symbol: {Symbol}", symbol ?? "All");

        if (!string.IsNullOrEmpty(symbol))
        {
            var singleResult = await _client.SpotApi.ExchangeData.GetTickerAsync(symbol, ct);
            if (!singleResult.Success)
            {
                _logger.LogError("Failed to get ticker for {Symbol}: {Error}", symbol, singleResult.Error);
                return singleResult.As<IEnumerable<MexcTicker>>(null);
            }
            return singleResult.As<IEnumerable<MexcTicker>>(new[] { singleResult.Data });
        }

        var result = await _client.SpotApi.ExchangeData.GetTickersAsync(ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to get tickers: {Error}", result.Error);
            return result.As<IEnumerable<MexcTicker>>(null);
        }
        return result.As<IEnumerable<MexcTicker>>(result.Data);
    }

    public async Task<WebCallResult<MexcOrderBook>> GetOrderBookAsync(string symbol, int? limit = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching order book for {Symbol} with limit {Limit}", symbol, limit);
        var result = await _client.SpotApi.ExchangeData.GetOrderBookAsync(symbol, limit, ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to get order book for {Symbol}: {Error}", symbol, result.Error);
        }
        return result;
    }

    public async Task<WebCallResult<IEnumerable<MexcKline>>> GetKlinesAsync(string symbol, KlineInterval interval, DateTime? startTime = null, DateTime? endTime = null, int? limit = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Fetching klines for {Symbol} with interval {Interval}", symbol, interval);
        var result = await _client.SpotApi.ExchangeData.GetKlinesAsync(symbol, interval, startTime, endTime, limit, ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to get klines for {Symbol}: {Error}", symbol, result.Error);
            return result.As<IEnumerable<MexcKline>>(null);
        }
        return result.As<IEnumerable<MexcKline>>(result.Data);
    }

    public async Task<WebCallResult<string>> StartUserStreamAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Starting user stream...");
        var result = await _client.SpotApi.Account.StartUserStreamAsync(ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to start user stream: {Error}", result.Error);
        }
        else
        {
            _logger.LogInformation("User stream started successfully.");
        }
        return result;
    }

    public async Task<WebCallResult<MexcOrder>> PlaceOrderAsync(string symbol, OrderSide side, OrderType type, decimal quantity, decimal? price = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Placing order: {Symbol}, {Side}, {Type}, {Quantity}, Price: {Price}", symbol, side, type, quantity, price);
        var result = await _client.SpotApi.Trading.PlaceOrderAsync(symbol, side, type, quantity, price, ct: ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to place order: {Error}", result.Error);
        }
        return result.As<MexcOrder>(result.Data);
    }

    public async Task<WebCallResult<MexcOrder>> CancelOrderAsync(string symbol, string orderId, CancellationToken ct = default)
    {
        _logger.LogInformation("Canceling order {OrderId} for {Symbol}", orderId, symbol);
        var result = await _client.SpotApi.Trading.CancelOrderAsync(symbol, orderId, ct: ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to cancel order {OrderId}: {Error}", orderId, result.Error);
        }
        return result;
    }

    public async Task<WebCallResult<MexcOrder>> ModifyOrderAsync(
        string symbol,
        string oldOrderId,
        OrderSide side,
        decimal newPrice,
        decimal quantity,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Modifying order {OrderId} for {Symbol}: {Side} @ {Price}", oldOrderId, symbol, side, newPrice);

        // MEXC doesn't have a native modify endpoint, so we do atomic cancel+place
        // Step 1: Cancel the old order
        var cancelResult = await CancelOrderAsync(symbol, oldOrderId, ct);
        if (!cancelResult.Success)
        {
            _logger.LogError("Failed to cancel order during modification: {Error}", cancelResult.Error);
            return cancelResult;
        }

        // Step 2: Immediately place the new order
        var placeResult = await PlaceOrderAsync(symbol, side, OrderType.Limit, quantity, newPrice, ct);
        if (!placeResult.Success)
        {
            _logger.LogError("Failed to place new order after cancellation: {Error}. Old order was canceled!", placeResult.Error);
        }
        else
        {
            _logger.LogInformation("Order modified successfully: Old={OldOrderId}, New={NewOrderId}",
                oldOrderId, placeResult.Data.OrderId);
        }

        return placeResult;
    }
}
using CryptoExchange.Net.Objects;
using Mexc.Net.Enums;
using Mexc.Net.Objects.Models;
using Mexc.Net.Objects.Models.Spot;

namespace Trader.ExchangeApi.Abstractions;

/// <summary>
/// Defines the contract for a client that interacts with the MEXC REST API.
/// This abstraction allows the core application to be independent of the specific exchange library.
/// </summary>
public interface IMexcRestApiClient
{
    /// <summary>
    /// Gets the exchange trading rules and symbol information.
    /// </summary>
    Task<WebCallResult<MexcExchangeInfo>> GetExchangeInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the latest ticker information for all or a specific symbol.
    /// </summary>
    Task<WebCallResult<IEnumerable<MexcTicker>>> GetTickersAsync(string? symbol = null, CancellationToken ct = default);

    /// <summary>
    /// Gets the order book for a specific symbol.
    /// </summary>
    Task<WebCallResult<MexcOrderBook>> GetOrderBookAsync(string symbol, int? limit = null, CancellationToken ct = default);

    /// <summary>
    /// Gets kline/candlestick data for a specific symbol.
    /// </summary>
    Task<WebCallResult<IEnumerable<MexcKline>>> GetKlinesAsync(string symbol, KlineInterval interval, DateTime? startTime = null, DateTime? endTime = null, int? limit = null, CancellationToken ct = default);

    /// <summary>
    /// Starts a new user stream and returns the listen key.
    /// </summary>
    Task<WebCallResult<string>> StartUserStreamAsync(CancellationToken ct = default);

    /// <summary>
    /// Places a new order.
    /// </summary>
    Task<WebCallResult<MexcOrder>> PlaceOrderAsync(string symbol, OrderSide side, OrderType type, decimal quantity, decimal? price = null, CancellationToken ct = default);

    /// <summary>
    /// Cancels an order.
    /// </summary>
    Task<WebCallResult<MexcOrder>> CancelOrderAsync(string symbol, string orderId, CancellationToken ct = default);

    /// <summary>
    /// Modifies an existing order atomically by canceling the old one and placing a new one.
    /// This is a composite operation that ensures the order is updated as quickly as possible.
    /// </summary>
    Task<WebCallResult<MexcOrder>> ModifyOrderAsync(
        string symbol,
        string oldOrderId,
        OrderSide side,
        decimal newPrice,
        decimal quantity,
        CancellationToken ct = default);
}
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using CryptoExchange.Net.Sockets;
using Mexc.Net.Objects.Models.Spot;
using Mexc.Net.Objects.Sockets.Subscriptions;

namespace Trader.ExchangeApi.Abstractions;

/// <summary>
/// Defines the contract for a client that interacts with the MEXC WebSocket API.
/// This abstraction allows the core application to be independent of the specific exchange library.
/// </summary>
public interface IMexcSocketApiClient
{
    /// <summary>
    /// Subscribes to order book updates for a specific symbol.
    /// </summary>
    /// <param name="symbol">The symbol to subscribe to.</param>
    /// <param name="onData">The handler for incoming data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the subscription details.</returns>
    Task<CallResult<UpdateSubscription>> SubscribeToOrderBookUpdatesAsync(string symbol, Action<DataEvent<MexcStreamOrderBook>> onData, CancellationToken ct = default);

    /// <summary>
    /// Subscribes to user-specific order updates.
    /// </summary>
    /// <param name="listenKey">The listen key for the user stream.</param>
    /// <param name="onOrderUpdate">The handler for order update events.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing the subscription details.</returns>
    Task<CallResult<UpdateSubscription>> SubscribeToOrderUpdatesAsync(string listenKey, Action<DataEvent<MexcUserOrderUpdate>> onOrderUpdate, CancellationToken ct = default);
}
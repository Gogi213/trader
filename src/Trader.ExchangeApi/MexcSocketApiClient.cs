using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using CryptoExchange.Net.Sockets;
using Mexc.Net.Clients;
using Mexc.Net.Interfaces.Clients;
using Mexc.Net.Objects.Models.Spot;
using Mexc.Net.Objects.Sockets.Subscriptions;
using Microsoft.Extensions.Logging;
using Trader.ExchangeApi.Abstractions;

namespace Trader.ExchangeApi;

/// <summary>
/// Adapter for the MEXC WebSocket API client.
/// It wraps the underlying Mexc.Net library to provide a simplified and controlled interface.
/// </summary>
public class MexcSocketApiClient : IMexcSocketApiClient
{
    private readonly IMexcSocketClient _socketClient;
    private readonly ILogger<MexcSocketApiClient> _logger;

    public MexcSocketApiClient(IMexcSocketClient socketClient, ILogger<MexcSocketApiClient> logger)
    {
        _socketClient = socketClient;
        _logger = logger;
    }

    public async Task<CallResult<UpdateSubscription>> SubscribeToOrderBookUpdatesAsync(string symbol, Action<DataEvent<MexcStreamOrderBook>> onData, CancellationToken ct = default)
    {
        _logger.LogInformation("Subscribing to order book updates for {Symbol}", symbol);
        var result = await _socketClient.SpotApi.SubscribeToOrderBookUpdatesAsync(symbol, onData, ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to subscribe to order book updates for {Symbol}: {Error}", symbol, result.Error);
        }
        return result;
    }

    public async Task<CallResult<UpdateSubscription>> SubscribeToOrderUpdatesAsync(string listenKey, Action<DataEvent<MexcUserOrderUpdate>> onOrderUpdate, CancellationToken ct = default)
    {
        _logger.LogInformation("Subscribing to user order updates");
        var result = await _socketClient.SpotApi.SubscribeToOrderUpdatesAsync(listenKey, onOrderUpdate, ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to subscribe to user order updates: {Error}", result.Error);
        }
        return result;
    }
}
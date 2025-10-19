using Mexc.Net.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trader.Core.Abstractions;
using Trader.Core.Models;
using Trader.Core.Services;
using Trader.ExchangeApi.Abstractions;

namespace Trader.Core;

public class TradingStrategyService : ITradingStrategyService
{
    private readonly ILogger<TradingStrategyService> _logger;
    private readonly IMexcSocketApiClient _socketApiClient;
    private readonly IMexcRestApiClient _restApiClient;
    private readonly PositionManager _positionManager;
    private readonly TradingOptions _options;
    private readonly CircuitBreakerOptions _circuitBreakerOptions;

    private readonly OrderState _orderState = new();
    private bool _isCircuitBreakerTripped;
    private DateTime _lastOrderUpdateTime = DateTime.MinValue;
    private const int DEBOUNCE_MS = 200; // Минимум 200ms между обновлениями

    public TradingStrategyService(
        ILogger<TradingStrategyService> logger,
        IMexcSocketApiClient socketApiClient,
        IMexcRestApiClient restApiClient,
        PositionManager positionManager,
        IOptions<TradingOptions> options,
        IOptions<CircuitBreakerOptions> circuitBreakerOptions)
    {
        _logger = logger;
        _socketApiClient = socketApiClient;
        _restApiClient = restApiClient;
        _positionManager = positionManager;
        _options = options.Value;
        _circuitBreakerOptions = circuitBreakerOptions.Value;
    }

    public async Task ExecuteAsync(TradingSymbol symbol, CancellationToken ct)
    {
        _logger.LogInformation("Executing trading strategy for {Symbol}", symbol.Name);

        // Start user stream
        var listenKeyResult = await _restApiClient.StartUserStreamAsync(ct);
        if (!listenKeyResult.Success)
        {
            _logger.LogError("Failed to start user stream: {Error}", listenKeyResult.Error);
            return;
        }
        var listenKey = listenKeyResult.Data;

        // Place initial orders
        await PlaceInitialOrdersAsync(symbol.Name, ct);

        // Subscribe to order book updates
        var orderBookSubscription = await _socketApiClient.SubscribeToOrderBookUpdatesAsync(
            symbol.Name,
            async dataEvent => await OnOrderBookUpdateAsync(symbol.Name, dataEvent.Data, ct),
            ct);

        // Subscribe to user order updates
        var orderSubscription = await _socketApiClient.SubscribeToOrderUpdatesAsync(
            listenKey,
            async dataEvent => await OnOrderUpdateAsync(symbol.Name, dataEvent.Data, ct),
            ct);

        if (!orderBookSubscription.Success || !orderSubscription.Success)
        {
            _logger.LogError("Failed to subscribe to streams for {Symbol}", symbol.Name);
            return;
        }

        _logger.LogInformation("Successfully subscribed to order book and user order streams for {Symbol}", symbol.Name);

        // Keep running
        await Task.Delay(Timeout.Infinite, ct);
    }

    private async Task OnOrderBookUpdateAsync(string symbol, dynamic orderBook, CancellationToken ct)
    {
        try
        {
            // Debounce check
            var now = DateTime.UtcNow;
            if ((now - _lastOrderUpdateTime).TotalMilliseconds < DEBOUNCE_MS)
            {
                return;
            }

            var bids = orderBook.Bids as IEnumerable<dynamic>;
            var asks = orderBook.Asks as IEnumerable<dynamic>;

            var bestBid = bids?.FirstOrDefault();
            var bestAsk = asks?.FirstOrDefault();

            if (bestBid == null || bestAsk == null)
            {
                _logger.LogWarning("Order book has no bids or asks");
                return;
            }

            decimal bestBidPrice = bestBid?.Price ?? 0;
            decimal bestAskPrice = bestAsk?.Price ?? 0;

            // Circuit Breaker check
            var spread = (bestAskPrice - bestBidPrice) / bestBidPrice * 100;
            if (spread < _circuitBreakerOptions.MinSpreadPercentage || spread > _circuitBreakerOptions.MaxSpreadPercentage)
            {
                if (!_isCircuitBreakerTripped)
                {
                    _isCircuitBreakerTripped = true;
                    _logger.LogWarning("Circuit breaker tripped! Spread is {Spread}%. Halting trading.", spread);
                    await CancelAllOrdersAsync(symbol, ct);
                }
                return;
            }

            if (_isCircuitBreakerTripped)
            {
                _logger.LogInformation("Resuming trading as spread is back to normal ({Spread}%).", spread);
                _isCircuitBreakerTripped = false;
                await PlaceInitialOrdersAsync(symbol, ct);
                return;
            }

            // Update BID order if needed (when no inventory)
            if (!_positionManager.HasOpenPosition())
            {
                await UpdateBidOrderIfNeededAsync(symbol, bestBidPrice, ct);
            }

            // Update ASK order if needed (when have inventory)
            if (_positionManager.HasOpenPosition())
            {
                var position = _positionManager.GetCurrentPosition();
                var targetAskPrice = position!.TargetSellPrice;
                await UpdateAskOrderIfNeededAsync(symbol, targetAskPrice, ct);
            }

            _lastOrderUpdateTime = now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order book update");
        }
    }

    private async Task OnOrderUpdateAsync(string symbol, dynamic orderUpdate, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Order update received for symbol {Symbol}", symbol);

            if (orderUpdate.Status != OrderStatus.Filled)
            {
                return;
            }

            // BUY order filled - open position
            if (orderUpdate.Side == OrderSide.Buy)
            {
                decimal buyPrice = orderUpdate.Price;
                decimal quantity = orderUpdate.QuantityFilled;

                _positionManager.OpenPosition(symbol, buyPrice, quantity, _options.TargetSpreadPercentage);
                _orderState.ClearBidOrder();

                _logger.LogInformation("BUY order filled at {Price}. Position opened. Target sell: {TargetPrice}",
                    buyPrice, _positionManager.GetCurrentPosition()!.TargetSellPrice);

                // Place ASK order at target price
                var targetAskPrice = _positionManager.GetCurrentPosition()!.TargetSellPrice;
                await PlaceAskOrderAsync(symbol, targetAskPrice, quantity, ct);
            }
            // SELL order filled - close position
            else
            {
                decimal sellPrice = orderUpdate.Price;
                var position = _positionManager.GetCurrentPosition();

                if (position != null)
                {
                    var profit = position.CalculateProfitUsdt(sellPrice);
                    var profitPercent = position.CalculateProfitPercent(sellPrice);

                    _logger.LogInformation("SELL order filled at {Price}. Profit: {ProfitUsdt} USDT ({ProfitPercent}%)",
                        sellPrice, profit, profitPercent);
                }

                _positionManager.ClosePosition();
                _orderState.ClearAskOrder();

                // Place new BID order
                var orderBook = await _restApiClient.GetOrderBookAsync(symbol, 1, ct);
                if (orderBook.Success)
                {
                    var bestBidPrice = orderBook.Data.Bids.First().Price;
                    await PlaceBidOrderAsync(symbol, bestBidPrice, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order update");
        }
    }

    private async Task UpdateBidOrderIfNeededAsync(string symbol, decimal newBidPrice, CancellationToken ct)
    {
        if (!_orderState.HasActiveBidOrder)
        {
            await PlaceBidOrderAsync(symbol, newBidPrice, ct);
            return;
        }

        // Check if price changed significantly (threshold)
        var currentBidPrice = _orderState.BidPrice!.Value;
        var priceChangePercent = Math.Abs((newBidPrice - currentBidPrice) / currentBidPrice * 100);

        if (priceChangePercent < _options.OrderUpdateThresholdPercent)
        {
            return; // No significant change
        }

        _logger.LogDebug("BID price changed by {ChangePercent}%. Modifying order from {OldPrice} to {NewPrice}",
            priceChangePercent, currentBidPrice, newBidPrice);

        // Modify order
        var quantity = _options.OrderAmountUsdt / newBidPrice;
        var result = await _restApiClient.ModifyOrderAsync(
            symbol,
            _orderState.BidOrderId!,
            OrderSide.Buy,
            newBidPrice,
            quantity,
            ct);

        if (result.Success)
        {
            _orderState.SetBidOrder(result.Data.OrderId, newBidPrice);
        }
        else
        {
            _logger.LogError("Failed to modify BID order: {Error}", result.Error);
            _orderState.ClearBidOrder();
        }
    }

    private async Task UpdateAskOrderIfNeededAsync(string symbol, decimal newAskPrice, CancellationToken ct)
    {
        if (!_orderState.HasActiveAskOrder)
        {
            var position = _positionManager.GetCurrentPosition();
            if (position != null)
            {
                await PlaceAskOrderAsync(symbol, newAskPrice, position.Quantity, ct);
            }
            return;
        }

        // Check if price changed significantly
        var currentAskPrice = _orderState.AskPrice!.Value;
        var priceChangePercent = Math.Abs((newAskPrice - currentAskPrice) / currentAskPrice * 100);

        if (priceChangePercent < _options.OrderUpdateThresholdPercent)
        {
            return;
        }

        _logger.LogDebug("ASK price changed by {ChangePercent}%. Modifying order from {OldPrice} to {NewPrice}",
            priceChangePercent, currentAskPrice, newAskPrice);

        var currentPosition = _positionManager.GetCurrentPosition();
        if (currentPosition == null)
        {
            _logger.LogWarning("Cannot modify ASK order: no open position");
            return;
        }

        var result = await _restApiClient.ModifyOrderAsync(
            symbol,
            _orderState.AskOrderId!,
            OrderSide.Sell,
            newAskPrice,
            currentPosition.Quantity,
            ct);

        if (result.Success)
        {
            _orderState.SetAskOrder(result.Data.OrderId, newAskPrice);
        }
        else
        {
            _logger.LogError("Failed to modify ASK order: {Error}", result.Error);
            _orderState.ClearAskOrder();
        }
    }

    private async Task PlaceBidOrderAsync(string symbol, decimal price, CancellationToken ct)
    {
        var quantity = _options.OrderAmountUsdt / price;
        var result = await _restApiClient.PlaceOrderAsync(symbol, OrderSide.Buy, OrderType.Limit, quantity, price, ct);

        if (result.Success)
        {
            _orderState.SetBidOrder(result.Data.OrderId, price);
            _logger.LogInformation("BID order placed: OrderId={OrderId}, Price={Price}, Quantity={Quantity}",
                result.Data.OrderId, price, quantity);
        }
        else
        {
            _logger.LogError("Failed to place BID order: {Error}", result.Error);
        }
    }

    private async Task PlaceAskOrderAsync(string symbol, decimal price, decimal quantity, CancellationToken ct)
    {
        var result = await _restApiClient.PlaceOrderAsync(symbol, OrderSide.Sell, OrderType.Limit, quantity, price, ct);

        if (result.Success)
        {
            _orderState.SetAskOrder(result.Data.OrderId, price);
            _logger.LogInformation("ASK order placed: OrderId={OrderId}, Price={Price}, Quantity={Quantity}",
                result.Data.OrderId, price, quantity);
        }
        else
        {
            _logger.LogError("Failed to place ASK order: {Error}", result.Error);
        }
    }

    private async Task PlaceInitialOrdersAsync(string symbol, CancellationToken ct)
    {
        var orderBook = await _restApiClient.GetOrderBookAsync(symbol, 1, ct);
        if (!orderBook.Success)
        {
            _logger.LogError("Failed to get order book for initial orders");
            return;
        }

        var bestBidPrice = orderBook.Data.Bids.First().Price;
        var bestAskPrice = orderBook.Data.Asks.First().Price;

        _logger.LogInformation("Placing initial orders: BID={BestBid}, ASK={BestAsk}", bestBidPrice, bestAskPrice);

        await PlaceBidOrderAsync(symbol, bestBidPrice, ct);
    }

    private async Task CancelAllOrdersAsync(string symbol, CancellationToken ct)
    {
        if (_orderState.HasActiveBidOrder)
        {
            await _restApiClient.CancelOrderAsync(symbol, _orderState.BidOrderId!, ct);
            _orderState.ClearBidOrder();
        }

        if (_orderState.HasActiveAskOrder)
        {
            await _restApiClient.CancelOrderAsync(symbol, _orderState.AskOrderId!, ct);
            _orderState.ClearAskOrder();
        }

        _logger.LogInformation("All orders canceled");
    }
}

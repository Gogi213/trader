using Mexc.Net.Clients;
using Mexc.Net.Objects;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Abstractions;
using TradingBot.Core.Domain;
using CryptoExchange.Net.Authentication;
using MexcOrderSide = Mexc.Net.Enums.OrderSide;
using MexcOrderType = Mexc.Net.Enums.OrderType;
using MexcOrderStatus = Mexc.Net.Enums.OrderStatus;

namespace TradingBot.Core.Services;

public class MexcExchangeAdapter : IExchangeAdapter, IDisposable
{
    private readonly ILogger<MexcExchangeAdapter> _logger;
    private readonly MexcRestClient _client;

    public MexcExchangeAdapter(string apiKey, string apiSecret, ILogger<MexcExchangeAdapter> logger)
    {
        _logger = logger;

        _client = new MexcRestClient(options =>
        {
            options.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
        });

        _logger.LogInformation("MEXC Exchange Adapter initialized with API credentials");
    }

    public async Task<OrderResult> PlaceOrderAsync(
        string symbol,
        OrderSide side,
        OrderType type,
        decimal quantity,
        decimal? price = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Placing {Type} {Side} order: {Symbol}, Qty: {Quantity}, Price: {Price}",
                type, side, symbol, quantity, price);

            var mexcSide = side == OrderSide.Buy ? MexcOrderSide.Buy : MexcOrderSide.Sell;
            var mexcType = type == OrderType.Limit ? MexcOrderType.Limit : MexcOrderType.Market;

            var result = await _client.SpotApi.Trading.PlaceOrderAsync(
                symbol: symbol,
                side: mexcSide,
                type: mexcType,
                quantity: quantity,
                price: price,
                ct: cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to place order: {Error}", result.Error?.Message);
                return OrderResult.Failed(result.Error?.Message ?? "Unknown error");
            }

            var mexcOrder = result.Data;
            var order = new Order
            {
                OrderId = mexcOrder.OrderId,
                Symbol = mexcOrder.Symbol,
                Side = side,
                Type = type,
                Quantity = mexcOrder.Quantity,
                Price = mexcOrder.Price,
                QuantityFilled = mexcOrder.QuantityFilled,
                Status = MapOrderStatus(mexcOrder.Status),
                CreateTime = mexcOrder.Timestamp,
                UpdateTime = mexcOrder.UpdateTime
            };

            _logger.LogInformation("Order placed successfully. OrderId: {OrderId}", order.OrderId);

            return OrderResult.Successful(order.OrderId, order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while placing order");
            return OrderResult.Failed(ex.Message);
        }
    }

    public async Task<bool> CancelOrderAsync(string orderId, string? symbol = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(symbol))
            {
                _logger.LogError("Symbol is required for cancel order");
                return false;
            }

            _logger.LogInformation("Canceling order: {OrderId}, Symbol: {Symbol}", orderId, symbol);

            var result = await _client.SpotApi.Trading.CancelOrderAsync(
                symbol: symbol,
                orderId: orderId,
                ct: cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to cancel order: {Error}", result.Error?.Message);
                return false;
            }

            _logger.LogInformation("Order canceled successfully: {OrderId}", orderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while canceling order");
            return false;
        }
    }

    public async Task<IEnumerable<Order>> GetOpenOrdersAsync(string? symbol = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(symbol))
            {
                _logger.LogWarning("Symbol is required for GetOpenOrdersAsync");
                return Enumerable.Empty<Order>();
            }

            _logger.LogDebug("Getting open orders for symbol: {Symbol}", symbol);

            var result = await _client.SpotApi.Trading.GetOpenOrdersAsync(
                symbol: symbol,
                ct: cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to get open orders: {Error}", result.Error?.Message);
                return Enumerable.Empty<Order>();
            }

            var orders = result.Data.Select(o => new Order
            {
                OrderId = o.OrderId,
                Symbol = o.Symbol,
                Side = o.Side == MexcOrderSide.Buy ? OrderSide.Buy : OrderSide.Sell,
                Type = o.OrderType == MexcOrderType.Limit ? OrderType.Limit : OrderType.Market,
                Status = MapOrderStatus(o.Status),
                Quantity = o.Quantity,
                Price = o.Price,
                QuantityFilled = o.QuantityFilled,
                AveragePrice = o.QuantityFilled > 0 ? o.QuoteQuantityFilled / o.QuantityFilled : null,
                CreateTime = o.Timestamp,
                UpdateTime = o.UpdateTime
            }).ToList();

            _logger.LogDebug("Retrieved {Count} open orders", orders.Count);

            return orders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting open orders");
            return Enumerable.Empty<Order>();
        }
    }

    public async Task<Order?> GetOrderAsync(string orderId, string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting order: {OrderId}, Symbol: {Symbol}", orderId, symbol);

            var result = await _client.SpotApi.Trading.GetOrderAsync(
                symbol: symbol,
                orderId: orderId,
                ct: cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to get order: {Error}", result.Error?.Message);
                return null;
            }

            var o = result.Data;
            return new Order
            {
                OrderId = o.OrderId,
                Symbol = o.Symbol,
                Side = o.Side == MexcOrderSide.Buy ? OrderSide.Buy : OrderSide.Sell,
                Type = o.OrderType == MexcOrderType.Limit ? OrderType.Limit : OrderType.Market,
                Status = MapOrderStatus(o.Status),
                Quantity = o.Quantity,
                Price = o.Price,
                QuantityFilled = o.QuantityFilled,
                AveragePrice = o.QuantityFilled > 0 ? o.QuoteQuantityFilled / o.QuantityFilled : null,
                CreateTime = o.Timestamp,
                UpdateTime = o.UpdateTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting order");
            return null;
        }
    }

    public async Task<IEnumerable<Balance>> GetBalancesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting account balances");

            var result = await _client.SpotApi.Account.GetAccountInfoAsync(ct: cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to get balances: {Error}", result.Error?.Message);
                return Enumerable.Empty<Balance>();
            }

            var balances = result.Data.Balances
                .Where(b => b.Available > 0 || b.Locked > 0)
                .Select(b => new Balance
                {
                    Asset = b.Asset,
                    Available = b.Available,
                    Locked = b.Locked
                })
                .ToList();

            _logger.LogInformation("Retrieved {Count} balances with non-zero amounts", balances.Count);
            foreach (var balance in balances)
            {
                _logger.LogInformation("  {Asset}: Available={Available}, Locked={Locked}, Total={Total}",
                    balance.Asset, balance.Available, balance.Locked, balance.Total);
            }

            return balances;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting balances");
            return Enumerable.Empty<Balance>();
        }
    }

    public async Task<OrderBook> GetOrderBookAsync(string symbol, int limit = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting order book for {Symbol}, limit: {Limit}", symbol, limit);

            var result = await _client.SpotApi.ExchangeData.GetOrderBookAsync(
                symbol: symbol,
                limit: limit,
                ct: cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to get order book: {Error}", result.Error?.Message);
                return new OrderBook { Symbol = symbol };
            }

            var orderBook = new OrderBook
            {
                Symbol = symbol,
                Timestamp = DateTime.UtcNow,
                Bids = result.Data.Bids.Select(b => new OrderBookEntry
                {
                    Price = b.Price,
                    Quantity = b.Quantity
                }).ToList(),
                Asks = result.Data.Asks.Select(a => new OrderBookEntry
                {
                    Price = a.Price,
                    Quantity = a.Quantity
                }).ToList()
            };

            _logger.LogDebug("Order book retrieved: Best Bid: {BestBid}, Best Ask: {BestAsk}, Mid: {Mid}",
                orderBook.BestBid, orderBook.BestAsk, orderBook.MidPrice);

            return orderBook;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting order book");
            return new OrderBook { Symbol = symbol };
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Testing connection to MEXC API...");

            // Test 1: Ping
            var pingResult = await _client.SpotApi.ExchangeData.PingAsync(ct: cancellationToken);
            if (!pingResult.Success)
            {
                _logger.LogError("Ping failed: {Error}", pingResult.Error?.Message);
                return false;
            }
            _logger.LogInformation("Ping successful");

            // Test 2: Get account info
            var accountResult = await _client.SpotApi.Account.GetAccountInfoAsync(ct: cancellationToken);
            if (!accountResult.Success)
            {
                _logger.LogError("Account info failed: {Error}", accountResult.Error?.Message);
                return false;
            }

            var balancesCount = accountResult.Data.Balances.Count(b => b.Available > 0 || b.Locked > 0);
            _logger.LogInformation("Connection test successful! Account has {Count} non-zero balances", balancesCount);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during connection test");
            return false;
        }
    }

    private static OrderStatus MapOrderStatus(MexcOrderStatus mexcStatus)
    {
        return mexcStatus switch
        {
            MexcOrderStatus.New => OrderStatus.New,
            MexcOrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
            MexcOrderStatus.Filled => OrderStatus.Filled,
            MexcOrderStatus.Canceled => OrderStatus.Canceled,
            MexcOrderStatus.PartiallyCanceled => OrderStatus.PartiallyFilled,
            _ => OrderStatus.New
        };
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

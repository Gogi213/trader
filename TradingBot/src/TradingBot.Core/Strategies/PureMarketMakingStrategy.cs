using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingBot.Core.Abstractions;
using TradingBot.Core.Domain;

namespace TradingBot.Core.Strategies;

/// <summary>
/// Реализация стратегии Pure Market Making
/// Выставляет симметричные bid/ask ордера вокруг центральной цены
/// </summary>
public sealed class PureMarketMakingStrategy : ITradingStrategy
{
    private readonly ILogger<PureMarketMakingStrategy> _logger;
    private readonly IExchangeAdapter _exchange;
    private readonly PureMarketMakingOptions _options;
    private readonly IRiskManager? _riskManager; // Sprint 5: опционально

    // Состояние стратегии
    private readonly Dictionary<string, Order> _activeOrders = new();
    private DateTime _lastOrderRefreshTime = DateTime.MinValue;
    private bool _isRunning;

    // Sprint 4: Ping-Pong state
    private OrderSide? _lastFilledSide;

    // Sprint 4: Filled Order Delay state
    private DateTime _filledOrderDelayUntil = DateTime.MinValue;

    public string Name => "PureMarketMaking";

    public PureMarketMakingStrategy(
        ILogger<PureMarketMakingStrategy> logger,
        IExchangeAdapter exchange,
        IOptions<PureMarketMakingOptions> options,
        IRiskManager? riskManager = null) // Sprint 5: опционально
    {
        _logger = logger;
        _exchange = exchange;
        _options = options.Value;
        _riskManager = riskManager;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Инициализация стратегии {Strategy} ===", Name);

        _logger.LogInformation("Конфигурация:");
        _logger.LogInformation("  Market: {Market}", _options.Market);
        _logger.LogInformation("  Bid Spread: {BidSpread}%", _options.BidSpread);
        _logger.LogInformation("  Ask Spread: {AskSpread}%", _options.AskSpread);
        _logger.LogInformation("  Order Amount: {Amount}", _options.OrderAmount);
        _logger.LogInformation("  Order Refresh Time: {Time}s", _options.OrderRefreshTime);
        _logger.LogInformation("  Order Refresh Tolerance: {Tolerance}%", _options.OrderRefreshTolerancePct);
        _logger.LogInformation("  Price Type: {PriceType}", _options.PriceType);

        // Проверка подключения
        var connectionOk = await _exchange.TestConnectionAsync(cancellationToken);
        if (!connectionOk)
        {
            throw new InvalidOperationException("Не удалось подключиться к бирже MEXC");
        }

        _logger.LogInformation("Подключение к бирже установлено");

        // Получение балансов
        var balances = await _exchange.GetBalancesAsync(cancellationToken);
        _logger.LogInformation("Текущие балансы:");
        foreach (var balance in balances.Where(b => b.Available > 0).Take(5))
        {
            _logger.LogInformation("  {Asset}: {Available}", balance.Asset, balance.Available);
        }

        // Синхронизация открытых ордеров
        await SyncOpenOrdersAsync(cancellationToken);

        _isRunning = true;
        _logger.LogInformation("Стратегия {Strategy} инициализирована успешно\n", Name);
    }

    public async Task TickAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            _logger.LogWarning("Стратегия не запущена, пропускаем tick");
            return;
        }

        try
        {
            _logger.LogDebug("=== Tick начат ===");

            // Шаг 1: Проверки готовности
            if (!await IsReadyToTradeAsync(cancellationToken))
            {
                _logger.LogDebug("Стратегия не готова к торговле");
                return;
            }

            // Шаг 2: Создание Proposal
            var proposal = await CreateProposalAsync(cancellationToken);
            if (proposal.Bids.Count == 0 && proposal.Asks.Count == 0)
            {
                _logger.LogDebug("Proposal пуст");
                return;
            }

            _logger.LogInformation("Создан Proposal: {BuyOrders} Buy, {SellOrders} Sell",
                proposal.Bids.Count, proposal.Asks.Count);

            // Шаг 2.5: Sprint 4 - Проверка Filled Order Delay
            if (_options.FilledOrderDelay > 0 && DateTime.UtcNow < _filledOrderDelayUntil)
            {
                var remaining = (_filledOrderDelayUntil - DateTime.UtcNow).TotalSeconds;
                _logger.LogDebug("Задержка после исполнения активна, осталось {Remaining:F1}s", remaining);
                return;
            }

            // Шаг 2.6: Sprint 4 - Применение Ping-Pong
            if (_options.PingPongEnabled && _lastFilledSide.HasValue)
            {
                if (_lastFilledSide == OrderSide.Buy)
                {
                    _logger.LogDebug("Ping-Pong: последняя исполненная сторона BUY, убираем все BUY ордера");
                    proposal.Bids.Clear();
                }
                else
                {
                    _logger.LogDebug("Ping-Pong: последняя исполненная сторона SELL, убираем все SELL ордера");
                    proposal.Asks.Clear();
                }

                if (proposal.Bids.Count == 0 && proposal.Asks.Count == 0)
                {
                    _logger.LogDebug("Proposal пуст после Ping-Pong");
                    return;
                }
            }

            // Шаг 2.7: Sprint 4 - Применение Inventory Skew
            if (_options.InventorySkewEnabled)
            {
                await ApplyInventorySkewAsync(proposal, cancellationToken);
            }

            // Шаг 3: Sprint 4 - проверка MaxOrderAge
            if (_options.MaxOrderAge > 0 && _activeOrders.Count > 0)
            {
                var now = DateTime.UtcNow;
                var staleOrders = _activeOrders.Values
                    .Where(o => (now - o.CreateTime).TotalSeconds > _options.MaxOrderAge)
                    .ToList();

                if (staleOrders.Count > 0)
                {
                    _logger.LogWarning("Обнаружено {Count} устаревших ордеров (возраст > {MaxAge}s), отменяю...",
                        staleOrders.Count, _options.MaxOrderAge);

                    await CancelAllActiveOrdersAsync(cancellationToken);
                    await PlaceOrdersFromProposalAsync(proposal, cancellationToken);
                    _lastOrderRefreshTime = DateTime.UtcNow;
                    return;
                }
            }

            // Шаг 4: Проверка необходимости обновления
            if (!ShouldRefreshOrders(proposal))
            {
                _logger.LogDebug("Обновление ордеров не требуется");
                return;
            }

            // Шаг 5: Отмена устаревших ордеров
            await CancelAllActiveOrdersAsync(cancellationToken);

            // Шаг 5: Размещение новых ордеров
            await PlaceOrdersFromProposalAsync(proposal, cancellationToken);

            _lastOrderRefreshTime = DateTime.UtcNow;
            _logger.LogDebug("=== Tick завершен ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в tick стратегии");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Остановка стратегии {Strategy}", Name);
        _isRunning = false;

        await CancelAllActiveOrdersAsync(cancellationToken);

        _logger.LogInformation("Стратегия остановлена");
    }

    // ==================== Приватные методы ====================

    private async Task<bool> IsReadyToTradeAsync(CancellationToken cancellationToken)
    {
        var balances = await _exchange.GetBalancesAsync(cancellationToken);
        var quoteBalance = balances.FirstOrDefault(b => b.Asset == "USDT");

        if (quoteBalance == null || quoteBalance.Available < 1m)
        {
            _logger.LogWarning("Недостаточно USDT для торговли (доступно: {Available})",
                quoteBalance?.Available ?? 0);
            return false;
        }

        return true;
    }

    private async Task<Proposal> CreateProposalAsync(CancellationToken cancellationToken)
    {
        var proposal = new Proposal();

        var orderBook = await _exchange.GetOrderBookAsync(_options.Market, 5, cancellationToken);

        if (!orderBook.BestBid.HasValue || !orderBook.BestAsk.HasValue)
        {
            _logger.LogWarning("Не удалось получить best bid/ask");
            return proposal;
        }

        // Центральная цена
        decimal centralPrice = _options.PriceType switch
        {
            "mid_price" => orderBook.MidPrice ?? 0,
            "best_bid" => orderBook.BestBid.Value,
            "best_ask" => orderBook.BestAsk.Value,
            _ => orderBook.MidPrice ?? 0
        };

        if (centralPrice <= 0)
        {
            _logger.LogWarning("Центральная цена некорректна: {Price}", centralPrice);
            return proposal;
        }

        // Sprint 4: Создаем несколько уровней ордеров
        for (int level = 0; level < _options.OrderLevels; level++)
        {
            // Дополнительный спред для каждого уровня
            decimal levelSpreadMultiplier = 1 + (level * _options.OrderLevelSpread / 100m);

            // Расчет цен с учетом уровня
            decimal bidSpread = _options.BidSpread * levelSpreadMultiplier;
            decimal askSpread = _options.AskSpread * levelSpreadMultiplier;

            decimal bidPrice = centralPrice * (1 - bidSpread / 100m);
            decimal askPrice = centralPrice * (1 + askSpread / 100m);

            // Sprint 4: Проверка price ceiling/floor
            if (_options.PriceCeiling.HasValue && askPrice > _options.PriceCeiling.Value)
            {
                _logger.LogWarning("ASK цена {Price:F4} превышает потолок {Ceiling:F4}, пропускаем уровень {Level}",
                    askPrice, _options.PriceCeiling.Value, level);
                continue;
            }

            if (_options.PriceFloor.HasValue && bidPrice < _options.PriceFloor.Value)
            {
                _logger.LogWarning("BID цена {Price:F4} ниже пола {Floor:F4}, пропускаем уровень {Level}",
                    bidPrice, _options.PriceFloor.Value, level);
                continue;
            }

            bidPrice = Math.Round(bidPrice, 4, MidpointRounding.ToZero);
            askPrice = Math.Round(askPrice, 4, MidpointRounding.AwayFromZero);

            // Расчет количества
            decimal bidQuantity = _options.OrderAmount / bidPrice;
            decimal askQuantity = _options.OrderAmount / askPrice;

            bidQuantity = Math.Round(bidQuantity, 2, MidpointRounding.ToZero);
            askQuantity = Math.Round(askQuantity, 2, MidpointRounding.ToZero);

            proposal.Bids.Add(new PriceSize(bidPrice, bidQuantity));
            proposal.Asks.Add(new PriceSize(askPrice, askQuantity));

            _logger.LogInformation("  Уровень {Level} BUY:  {Qty} {Market} @ {Price:F4}",
                level, bidQuantity, _options.Market, bidPrice);
            _logger.LogInformation("  Уровень {Level} SELL: {Qty} {Market} @ {Price:F4}",
                level, askQuantity, _options.Market, askPrice);
        }

        return proposal;
    }

    private bool ShouldRefreshOrders(Proposal proposal)
    {
        if (_activeOrders.Count == 0)
        {
            _logger.LogDebug("Нет активных ордеров, требуется размещение");
            return true;
        }

        var timeSinceLastRefresh = (DateTime.UtcNow - _lastOrderRefreshTime).TotalSeconds;
        if (timeSinceLastRefresh < _options.OrderRefreshTime)
        {
            _logger.LogDebug("Прошло {Time:F1}s с последнего обновления (порог: {Threshold}s)",
                timeSinceLastRefresh, _options.OrderRefreshTime);
            return false;
        }

        var tolerance = (decimal)_options.OrderRefreshTolerancePct / 100m;

        // Проверяем bid ордера
        if (proposal.Bids.Count > 0)
        {
            var proposedBid = proposal.Bids[0];
            var activeBidOrder = _activeOrders.Values.FirstOrDefault(o => o.Side == OrderSide.Buy);

            if (activeBidOrder == null)
            {
                return true;
            }

            var activePrice = activeBidOrder.Price ?? 0;
            if (activePrice > 0)
            {
                var priceDiff = Math.Abs(proposedBid.Price - activePrice) / activePrice;
                if (priceDiff > tolerance)
                {
                    _logger.LogDebug("Цена BID изменилась на {Diff:P2}, требуется обновление", priceDiff);
                    return true;
                }
            }
        }

        // Проверяем ask ордера
        if (proposal.Asks.Count > 0)
        {
            var proposedAsk = proposal.Asks[0];
            var activeAskOrder = _activeOrders.Values.FirstOrDefault(o => o.Side == OrderSide.Sell);

            if (activeAskOrder == null)
            {
                return true;
            }

            var activePrice = activeAskOrder.Price ?? 0;
            if (activePrice > 0)
            {
                var priceDiff = Math.Abs(proposedAsk.Price - activePrice) / activePrice;
                if (priceDiff > tolerance)
                {
                    _logger.LogDebug("Цена ASK изменилась на {Diff:P2}, требуется обновление", priceDiff);
                    return true;
                }
            }
        }

        return false;
    }

    private async Task CancelAllActiveOrdersAsync(CancellationToken cancellationToken)
    {
        if (_activeOrders.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Отмена {Count} активных ордеров", _activeOrders.Count);

        var orderIds = _activeOrders.Keys.ToList();
        foreach (var orderId in orderIds)
        {
            try
            {
                var success = await _exchange.CancelOrderAsync(orderId, _options.Market, cancellationToken);
                if (success)
                {
                    _activeOrders.Remove(orderId);
                    _logger.LogDebug("  ✓ Ордер {OrderId} отменен", orderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене ордера {OrderId}", orderId);
            }
        }
    }

    private async Task PlaceOrdersFromProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        // Размещаем bid ордера
        foreach (var bid in proposal.Bids)
        {
            try
            {
                // Sprint 5: Проверка рисков
                var quantity = bid.Size;
                if (_riskManager != null)
                {
                    var riskCheck = await _riskManager.CanPlaceOrderAsync(
                        OrderSide.Buy, bid.Price, bid.Size, cancellationToken);

                    if (!riskCheck.Approved)
                    {
                        _logger.LogWarning("  ✗ BUY ордер отклонен Risk Manager: {Reason}", riskCheck.Reason);
                        continue;
                    }

                    quantity = riskCheck.AdjustedQuantity;
                }

                var result = await _exchange.PlaceOrderAsync(
                    symbol: _options.Market,
                    side: OrderSide.Buy,
                    type: OrderType.Limit,
                    quantity: quantity, // Используем проверенное количество
                    price: bid.Price,
                    cancellationToken: cancellationToken);

                if (result.Success && result.OrderId != null)
                {
                    var order = new Order
                    {
                        OrderId = result.OrderId,
                        Symbol = _options.Market,
                        Side = OrderSide.Buy,
                        Type = OrderType.Limit,
                        Price = bid.Price,
                        Quantity = bid.Size,
                        QuantityFilled = 0,
                        Status = OrderStatus.New,
                        CreateTime = DateTime.UtcNow
                    };

                    _activeOrders[result.OrderId] = order;
                    _logger.LogInformation("  ✓ BUY ордер размещен @ {Price:F4}", bid.Price);
                }
                else
                {
                    _logger.LogWarning("  ✗ Не удалось разместить BUY ордер: {Error}", result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при размещении BUY ордера");
            }
        }

        // Размещаем ask ордера
        foreach (var ask in proposal.Asks)
        {
            try
            {
                // Sprint 5: Проверка рисков
                var quantity = ask.Size;
                if (_riskManager != null)
                {
                    var riskCheck = await _riskManager.CanPlaceOrderAsync(
                        OrderSide.Sell, ask.Price, ask.Size, cancellationToken);

                    if (!riskCheck.Approved)
                    {
                        _logger.LogWarning("  ✗ SELL ордер отклонен Risk Manager: {Reason}", riskCheck.Reason);
                        continue;
                    }

                    quantity = riskCheck.AdjustedQuantity;
                }

                var result = await _exchange.PlaceOrderAsync(
                    symbol: _options.Market,
                    side: OrderSide.Sell,
                    type: OrderType.Limit,
                    quantity: quantity, // Используем проверенное количество
                    price: ask.Price,
                    cancellationToken: cancellationToken);

                if (result.Success && result.OrderId != null)
                {
                    var order = new Order
                    {
                        OrderId = result.OrderId,
                        Symbol = _options.Market,
                        Side = OrderSide.Sell,
                        Type = OrderType.Limit,
                        Price = ask.Price,
                        Quantity = ask.Size,
                        QuantityFilled = 0,
                        Status = OrderStatus.New,
                        CreateTime = DateTime.UtcNow
                    };

                    _activeOrders[result.OrderId] = order;
                    _logger.LogInformation("  ✓ SELL ордер размещен @ {Price:F4}", ask.Price);
                }
                else
                {
                    _logger.LogWarning("  ✗ Не удалось разместить SELL ордер: {Error}", result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при размещении SELL ордера");
            }
        }
    }

    /// <summary>
    /// Sprint 4: Применение Inventory Skew к Proposal
    /// Балансирует портфель путем асимметричного изменения объемов bid/ask ордеров
    /// </summary>
    private async Task ApplyInventorySkewAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Получить текущие балансы
            var balances = await _exchange.GetBalancesAsync(cancellationToken);
            var baseAsset = _options.Market.Replace("USDT", "");
            var quoteAsset = "USDT";

            var baseBalance = balances.FirstOrDefault(b => b.Asset == baseAsset);
            var quoteBalance = balances.FirstOrDefault(b => b.Asset == quoteAsset);

            if (baseBalance == null || quoteBalance == null)
            {
                _logger.LogWarning("Не удалось получить балансы для Inventory Skew");
                return;
            }

            // 2. Получить mid price
            var orderBook = await _exchange.GetOrderBookAsync(_options.Market, 5, cancellationToken);
            var midPrice = orderBook.MidPrice;

            if (!midPrice.HasValue || midPrice.Value <= 0)
            {
                _logger.LogWarning("Не удалось получить mid price для Inventory Skew");
                return;
            }

            // 3. Расчет общей стоимости портфеля в quote asset (USDT)
            var baseValueInQuote = baseBalance.Total * midPrice.Value;
            var totalPortfolioValue = baseValueInQuote + quoteBalance.Total;

            // 4. Расчет текущего процента базового актива
            var currentBasePct = (baseValueInQuote / totalPortfolioValue) * 100m;

            // 5. Расчет отклонения от цели
            var inventoryDelta = currentBasePct - _options.InventoryTargetBasePct;

            // 6. Расчет "зоны комфорта"
            var inventoryRange = (_options.OrderAmount * 2) * _options.InventoryRangeMultiplier;

            // 7. Расчет коэффициентов перекоса
            // Если inventoryDelta положительный (избыток base), увеличиваем ask, уменьшаем bid
            // Если отрицательный (дефицит base), увеличиваем bid, уменьшаем ask
            var skewFactor = inventoryDelta / (inventoryRange / 2);
            skewFactor = Math.Max(-1m, Math.Min(1m, skewFactor)); // Ограничиваем [-1, 1]

            var askRatio = 1m + skewFactor;  // От 0 до 2
            var bidRatio = 1m - skewFactor;  // От 2 до 0

            _logger.LogInformation("Inventory Skew:");
            _logger.LogInformation("  Portfolio Value: {Value:F2} USDT", totalPortfolioValue);
            _logger.LogInformation("  Current Base %: {Current:F2}%, Target: {Target:F2}%",
                currentBasePct, _options.InventoryTargetBasePct);
            _logger.LogInformation("  Inventory Delta: {Delta:F2}%", inventoryDelta);
            _logger.LogInformation("  Skew Factor: {Factor:F3}", skewFactor);
            _logger.LogInformation("  Bid Ratio: {BidRatio:F3}, Ask Ratio: {AskRatio:F3}",
                bidRatio, askRatio);

            // 8. Применить коэффициенты к объемам
            for (int i = 0; i < proposal.Bids.Count; i++)
            {
                var originalBid = proposal.Bids[i];
                var newSize = originalBid.Size * bidRatio;
                newSize = Math.Round(newSize, 2, MidpointRounding.ToZero);

                if (newSize > 0.01m) // Минимальный размер
                {
                    proposal.Bids[i] = new PriceSize(originalBid.Price, newSize);
                }
                else
                {
                    _logger.LogWarning("  BUY ордер слишком мал после Inventory Skew ({Size}), пропускаем", newSize);
                    proposal.Bids.RemoveAt(i);
                    i--;
                }
            }

            for (int i = 0; i < proposal.Asks.Count; i++)
            {
                var originalAsk = proposal.Asks[i];
                var newSize = originalAsk.Size * askRatio;
                newSize = Math.Round(newSize, 2, MidpointRounding.ToZero);

                if (newSize > 0.01m) // Минимальный размер
                {
                    proposal.Asks[i] = new PriceSize(originalAsk.Price, newSize);
                }
                else
                {
                    _logger.LogWarning("  SELL ордер слишком мал после Inventory Skew ({Size}), пропускаем", newSize);
                    proposal.Asks.RemoveAt(i);
                    i--;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при применении Inventory Skew");
        }
    }

    private async Task SyncOpenOrdersAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Синхронизация открытых ордеров с биржей");

        var openOrders = await _exchange.GetOpenOrdersAsync(_options.Market, cancellationToken);

        _activeOrders.Clear();
        foreach (var order in openOrders)
        {
            if (order.OrderId != null)
            {
                _activeOrders[order.OrderId] = order;
            }
        }

        _logger.LogInformation("Найдено {Count} открытых ордеров", _activeOrders.Count);
    }
}

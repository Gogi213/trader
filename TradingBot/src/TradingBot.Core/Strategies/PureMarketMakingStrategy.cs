using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _configuration;

    // Конфигурация стратегии
    private string _symbol = string.Empty;
    private decimal _bidSpread;
    private decimal _askSpread;
    private decimal _orderAmount;
    private double _orderRefreshTime;
    private decimal _orderRefreshTolerancePct;
    private string _priceType = "mid_price";

    // Состояние стратегии
    private readonly Dictionary<string, Order> _activeOrders = new();
    private DateTime _lastOrderRefreshTime = DateTime.MinValue;
    private bool _isRunning;

    public string Name => "PureMarketMaking";

    public PureMarketMakingStrategy(
        ILogger<PureMarketMakingStrategy> logger,
        IExchangeAdapter exchange,
        IConfiguration configuration)
    {
        _logger = logger;
        _exchange = exchange;
        _configuration = configuration;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Инициализация стратегии {Strategy} ===", Name);

        // Загрузка конфигурации
        var config = _configuration.GetSection("PureMarketMaking");
        _symbol = config["Symbol"] ?? throw new InvalidOperationException("Symbol не настроен");
        _bidSpread = config.GetValue<decimal>("BidSpread");
        _askSpread = config.GetValue<decimal>("AskSpread");
        _orderAmount = config.GetValue<decimal>("OrderAmount");
        _orderRefreshTime = config.GetValue<double>("OrderRefreshTime");
        _orderRefreshTolerancePct = config.GetValue<decimal>("OrderRefreshTolerancePct");
        _priceType = config["PriceType"] ?? "mid_price";

        _logger.LogInformation("Конфигурация:");
        _logger.LogInformation("  Symbol: {Symbol}", _symbol);
        _logger.LogInformation("  Bid Spread: {BidSpread}%", _bidSpread);
        _logger.LogInformation("  Ask Spread: {AskSpread}%", _askSpread);
        _logger.LogInformation("  Order Amount: {Amount}", _orderAmount);
        _logger.LogInformation("  Order Refresh Time: {Time}s", _orderRefreshTime);
        _logger.LogInformation("  Order Refresh Tolerance: {Tolerance}%", _orderRefreshTolerancePct);
        _logger.LogInformation("  Price Type: {PriceType}", _priceType);

        // Проверка подключения к бирже
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
        _logger.LogInformation("Стратегия {Strategy} инициализирована успешно", Name);
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
                _logger.LogDebug("Стратегия не готова к торговле, пропускаем tick");
                return;
            }

            // Шаг 2: Создание базового Proposal
            var proposal = await CreateProposalAsync(cancellationToken);
            if (proposal.TotalOrders == 0)
            {
                _logger.LogDebug("Proposal пуст, нечего размещать");
                return;
            }

            _logger.LogInformation("Создан Proposal: {BuyOrders} Buy, {SellOrders} Sell",
                proposal.Buys.Count, proposal.Sells.Count);

            // Шаг 3: Проверка необходимости обновления ордеров
            if (!ShouldRefreshOrders(proposal))
            {
                _logger.LogDebug("Обновление ордеров не требуется");
                return;
            }

            // Шаг 4: Отмена устаревших ордеров
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

        // Отменяем все активные ордера
        await CancelAllActiveOrdersAsync(cancellationToken);

        _logger.LogInformation("Стратегия остановлена");
    }

    // ==================== Приватные методы ====================

    /// <summary>
    /// Проверяет готовность стратегии к торговле
    /// </summary>
    private async Task<bool> IsReadyToTradeAsync(CancellationToken cancellationToken)
    {
        // Проверка балансов
        var balances = await _exchange.GetBalancesAsync(cancellationToken);
        var baseAsset = _symbol.Replace("USDT", ""); // Упрощение
        var quoteAsset = "USDT";

        var baseBalance = balances.FirstOrDefault(b => b.Asset == baseAsset);
        var quoteBalance = balances.FirstOrDefault(b => b.Asset == quoteAsset);

        if (quoteBalance == null || quoteBalance.Available < 1m)
        {
            _logger.LogWarning("Недостаточно {Asset} для торговли (доступно: {Available})",
                quoteAsset, quoteBalance?.Available ?? 0);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Создает Proposal с bid/ask ордерами
    /// </summary>
    private async Task<Proposal> CreateProposalAsync(CancellationToken cancellationToken)
    {
        var proposal = new Proposal();

        // Получаем order book для определения центральной цены
        var orderBook = await _exchange.GetOrderBookAsync(_symbol, 5, cancellationToken);

        if (!orderBook.BestBid.HasValue || !orderBook.BestAsk.HasValue)
        {
            _logger.LogWarning("Не удалось получить best bid/ask из order book");
            return proposal;
        }

        // Определяем центральную цену
        decimal centralPrice = _priceType switch
        {
            "mid_price" => orderBook.MidPrice ?? 0,
            "last_price" => orderBook.MidPrice ?? 0, // Упрощение, можно добавить last_price
            "best_bid" => orderBook.BestBid.Value,
            "best_ask" => orderBook.BestAsk.Value,
            _ => orderBook.MidPrice ?? 0
        };

        if (centralPrice <= 0)
        {
            _logger.LogWarning("Центральная цена некорректна: {Price}", centralPrice);
            return proposal;
        }

        _logger.LogDebug("Центральная цена ({PriceType}): {Price:F8}", _priceType, centralPrice);

        // Рассчитываем цены bid/ask ордеров
        decimal bidPrice = centralPrice * (1 - _bidSpread / 100m);
        decimal askPrice = centralPrice * (1 + _askSpread / 100m);

        // Округление цен (для MEXC обычно 4-8 знаков)
        bidPrice = Math.Round(bidPrice, 4, MidpointRounding.ToZero);
        askPrice = Math.Round(askPrice, 4, MidpointRounding.AwayFromZero);

        _logger.LogDebug("Рассчитанные цены: Bid={Bid:F8}, Ask={Ask:F8}", bidPrice, askPrice);

        // Рассчитываем количество базового актива
        decimal bidQuantity = _orderAmount / bidPrice;
        decimal askQuantity = _orderAmount / askPrice;

        // Округление количества (для большинства пар 2 знака)
        bidQuantity = Math.Round(bidQuantity, 2, MidpointRounding.ToZero);
        askQuantity = Math.Round(askQuantity, 2, MidpointRounding.ToZero);

        // Создаем bid ордер
        proposal.Buys.Add(new ProposedOrder
        {
            Symbol = _symbol,
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            Price = bidPrice,
            Quantity = bidQuantity,
            Level = 0
        });

        // Создаем ask ордер
        proposal.Sells.Add(new ProposedOrder
        {
            Symbol = _symbol,
            Side = OrderSide.Sell,
            Type = OrderType.Limit,
            Price = askPrice,
            Quantity = askQuantity,
            Level = 0
        });

        _logger.LogInformation("Proposal создан:");
        _logger.LogInformation("  BUY:  {Qty} {Symbol} @ {Price:F8}", bidQuantity, _symbol, bidPrice);
        _logger.LogInformation("  SELL: {Qty} {Symbol} @ {Price:F8}", askQuantity, _symbol, askPrice);

        return proposal;
    }

    /// <summary>
    /// Проверяет, нужно ли обновлять ордера
    /// </summary>
    private bool ShouldRefreshOrders(Proposal proposal)
    {
        // Если нет активных ордеров, размещаем новые
        if (_activeOrders.Count == 0)
        {
            _logger.LogDebug("Нет активных ордеров, требуется размещение");
            return true;
        }

        // Проверяем время с последнего обновления
        var timeSinceLastRefresh = (DateTime.UtcNow - _lastOrderRefreshTime).TotalSeconds;
        if (timeSinceLastRefresh < _orderRefreshTime)
        {
            _logger.LogDebug("Прошло {Time:F1}s с последнего обновления (порог: {Threshold}s)",
                timeSinceLastRefresh, _orderRefreshTime);
            return false;
        }

        // Проверяем, изменились ли цены достаточно сильно
        var tolerance = _orderRefreshTolerancePct / 100m;

        foreach (var proposedOrder in proposal.Buys.Concat(proposal.Sells))
        {
            var activeOrder = _activeOrders.Values.FirstOrDefault(o =>
                o.Symbol == proposedOrder.Symbol && o.Side == proposedOrder.Side);

            if (activeOrder == null)
            {
                _logger.LogDebug("Не найден активный ордер для {Side} {Symbol}",
                    proposedOrder.Side, proposedOrder.Symbol);
                return true;
            }

            var activePrice = activeOrder.Price ?? 0;
            if (activePrice == 0) continue;

            var priceDiff = Math.Abs(proposedOrder.Price - activePrice) / activePrice;
            if (priceDiff > tolerance)
            {
                _logger.LogDebug("Цена изменилась на {Diff:P2} (порог: {Tolerance:P2}), требуется обновление",
                    priceDiff, tolerance);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Отменяет все активные ордера
    /// </summary>
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
                var success = await _exchange.CancelOrderAsync(orderId, _symbol, cancellationToken);
                if (success)
                {
                    _activeOrders.Remove(orderId);
                    _logger.LogDebug("Ордер {OrderId} отменен", orderId);
                }
                else
                {
                    _logger.LogWarning("Не удалось отменить ордер {OrderId}", orderId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене ордера {OrderId}", orderId);
            }
        }
    }

    /// <summary>
    /// Размещает ордера из Proposal
    /// </summary>
    private async Task PlaceOrdersFromProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Размещение {Count} ордеров из Proposal", proposal.TotalOrders);

        foreach (var proposedOrder in proposal.Buys.Concat(proposal.Sells))
        {
            try
            {
                var result = await _exchange.PlaceOrderAsync(
                    symbol: proposedOrder.Symbol,
                    side: proposedOrder.Side,
                    type: proposedOrder.Type,
                    quantity: proposedOrder.Quantity,
                    price: proposedOrder.Price,
                    cancellationToken: cancellationToken);

                if (result.Success && result.OrderId != null)
                {
                    var order = new Order
                    {
                        OrderId = result.OrderId,
                        Symbol = proposedOrder.Symbol,
                        Side = proposedOrder.Side,
                        Type = proposedOrder.Type,
                        Price = proposedOrder.Price,
                        Quantity = proposedOrder.Quantity,
                        QuantityFilled = 0,
                        Status = OrderStatus.New,
                        CreateTime = DateTime.UtcNow
                    };

                    _activeOrders[result.OrderId] = order;

                    _logger.LogInformation("✓ Ордер размещен: {Order}", proposedOrder);
                }
                else
                {
                    _logger.LogWarning("✗ Не удалось разместить ордер: {Order}. Причина: {ErrorMessage}",
                        proposedOrder, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при размещении ордера {Order}", proposedOrder);
            }
        }
    }

    /// <summary>
    /// Синхронизирует открытые ордера с биржей
    /// </summary>
    private async Task SyncOpenOrdersAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Синхронизация открытых ордеров с биржей");

        var openOrders = await _exchange.GetOpenOrdersAsync(_symbol, cancellationToken);

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

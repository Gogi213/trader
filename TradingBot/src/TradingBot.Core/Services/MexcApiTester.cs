using Microsoft.Extensions.Logging;
using TradingBot.Core.Abstractions;
using TradingBot.Core.Domain;

namespace TradingBot.Core.Services;

/// <summary>
/// Сервис для тестирования MEXC API с реальными торговыми операциями
/// </summary>
public class MexcApiTester
{
    private readonly IExchangeAdapter _exchange;
    private readonly ILogger<MexcApiTester> _logger;

    public MexcApiTester(IExchangeAdapter exchange, ILogger<MexcApiTester> logger)
    {
        _exchange = exchange;
        _logger = logger;
    }

    /// <summary>
    /// Запуск всех тестов
    /// </summary>
    public async Task RunAllTestsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== НАЧАЛО ТЕСТИРОВАНИЯ MEXC API ===");

        // Сначала проверим подключение
        _logger.LogInformation("\n--- Проверка подключения ---");
        var connectionOk = await _exchange.TestConnectionAsync(cancellationToken);
        if (!connectionOk)
        {
            _logger.LogError("Тест подключения провалился. Остановка тестов.");
            return;
        }

        // Получим балансы
        _logger.LogInformation("\n--- Текущие балансы ---");
        var balances = await _exchange.GetBalancesAsync(cancellationToken);
        foreach (var balance in balances)
        {
            _logger.LogInformation("{Asset}: {Available} (заблокировано: {Locked})",
                balance.Asset, balance.Available, balance.Locked);
        }

        // Тест 1: Лимитный ордер
        _logger.LogInformation("\n\n=== ТЕСТ 1: Лимитный ордер ===");
        await Test1_LimitOrderAsync(cancellationToken);

        // Пауза между тестами
        _logger.LogInformation("\n--- Пауза 5 секунд между тестами ---");
        await Task.Delay(5000, cancellationToken);

        // Тест 2: Маркет ордера
        _logger.LogInformation("\n\n=== ТЕСТ 2: Маркет ордера ===");
        await Test2_MarketOrderAsync(cancellationToken);

        _logger.LogInformation("\n\n=== ТЕСТИРОВАНИЕ ЗАВЕРШЕНО ===");
    }

    /// <summary>
    /// Тест 1: Лимитный ордер на XRP/USDT
    /// - Размещение лимитки на $2 по цене $2 за XRP
    /// - Ожидание 15 секунд
    /// - Отмена ордера
    /// </summary>
    private async Task Test1_LimitOrderAsync(CancellationToken cancellationToken)
    {
        const string symbol = "XRPUSDT";
        const decimal targetUsdtAmount = 2m;
        const decimal limitPrice = 2m; // $2 за XRP

        try
        {
            _logger.LogInformation("Символ: {Symbol}", symbol);
            _logger.LogInformation("Целевая сумма: ${Amount} USDT", targetUsdtAmount);
            _logger.LogInformation("Лимитная цена: ${Price}", limitPrice);

            // Получаем текущую цену для справки
            var orderBook = await _exchange.GetOrderBookAsync(symbol, 5, cancellationToken);
            _logger.LogInformation("Текущая цена на бирже: Bid={Bid}, Ask={Ask}, Mid={Mid}",
                orderBook.BestBid, orderBook.BestAsk, orderBook.MidPrice);

            // Рассчитываем количество XRP
            decimal xrpQuantity = targetUsdtAmount / limitPrice;
            _logger.LogInformation("Рассчитанное количество XRP: {Quantity}", xrpQuantity);

            // Размещаем лимитный ордер на покупку
            _logger.LogInformation("\n→ Размещаем лимитный BUY ордер...");
            var result = await _exchange.PlaceOrderAsync(
                symbol: symbol,
                side: OrderSide.Buy,
                type: OrderType.Limit,
                quantity: xrpQuantity,
                price: limitPrice,
                cancellationToken: cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("✗ Не удалось разместить ордер: {Error}", result.ErrorMessage);
                return;
            }

            _logger.LogInformation("✓ Ордер размещен! OrderId: {OrderId}", result.OrderId);
            _logger.LogInformation("  Символ: {Symbol}, Сторона: {Side}, Количество: {Qty}, Цена: {Price}",
                result.Order?.Symbol, result.Order?.Side, result.Order?.Quantity, result.Order?.Price);

            // Ждем 15 секунд
            _logger.LogInformation("\n⏳ Ожидание 15 секунд...");
            for (int i = 15; i > 0; i--)
            {
                _logger.LogInformation("  {Seconds} сек...", i);
                await Task.Delay(1000, cancellationToken);
            }

            // Проверяем статус ордера
            _logger.LogInformation("\n→ Проверяем статус ордера...");
            var order = await _exchange.GetOrderAsync(result.OrderId!, symbol, cancellationToken);
            if (order != null)
            {
                _logger.LogInformation("  Статус: {Status}, Заполнено: {Filled}/{Total}",
                    order.Status, order.QuantityFilled, order.Quantity);
            }

            // Отменяем ордер
            _logger.LogInformation("\n→ Отменяем ордер...");
            var cancelled = await _exchange.CancelOrderAsync(result.OrderId!, symbol, cancellationToken);

            if (cancelled)
            {
                _logger.LogInformation("✓ Ордер успешно отменен!");
            }
            else
            {
                _logger.LogWarning("⚠ Не удалось отменить ордер (возможно, уже исполнен или отменен)");
            }

            _logger.LogInformation("\n✓ ТЕСТ 1 ЗАВЕРШЕН УСПЕШНО");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "✗ ТЕСТ 1 ПРОВАЛЕН с ошибкой");
        }
    }

    /// <summary>
    /// Тест 2: Маркет ордера на XRP/USDT
    /// - Вход в позицию на $2 по маркету
    /// - Ожидание 15 секунд
    /// - Закрытие позиции по маркету
    /// </summary>
    private async Task Test2_MarketOrderAsync(CancellationToken cancellationToken)
    {
        const string symbol = "XRPUSDT";
        const decimal targetUsdtAmount = 2m;

        try
        {
            _logger.LogInformation("Символ: {Symbol}", symbol);
            _logger.LogInformation("Целевая сумма: ${Amount} USDT", targetUsdtAmount);

            // Получаем текущую цену
            var orderBook = await _exchange.GetOrderBookAsync(symbol, 5, cancellationToken);
            _logger.LogInformation("Текущая цена на бирже: Bid={Bid}, Ask={Ask}, Mid={Mid}",
                orderBook.BestBid, orderBook.BestAsk, orderBook.MidPrice);

            if (!orderBook.BestAsk.HasValue)
            {
                _logger.LogError("✗ Не удалось получить цену Ask");
                return;
            }

            // Рассчитываем количество XRP на основе Ask цены
            decimal currentPrice = orderBook.BestAsk.Value;
            decimal xrpQuantity = targetUsdtAmount / currentPrice;

            // MEXC требует определенную точность для количества
            // Для большинства символов это 2-8 знаков после запятой
            // Округляем до 2 знаков для безопасности (минимальная точность для XRP)
            xrpQuantity = Math.Round(xrpQuantity, 2, MidpointRounding.ToZero);

            _logger.LogInformation("Рассчитанное количество XRP: {Quantity} (по цене ~${Price})",
                xrpQuantity, currentPrice);

            // Вход в позицию - маркет ордер на покупку
            _logger.LogInformation("\n→ ВХОД В ПОЗИЦИЮ: Маркет BUY ордер...");
            var buyResult = await _exchange.PlaceOrderAsync(
                symbol: symbol,
                side: OrderSide.Buy,
                type: OrderType.Market,
                quantity: xrpQuantity,
                price: null, // Маркет ордер без цены
                cancellationToken: cancellationToken);

            if (!buyResult.Success)
            {
                _logger.LogError("✗ Не удалось разместить BUY ордер: {Error}", buyResult.ErrorMessage);
                return;
            }

            _logger.LogInformation("✓ BUY ордер исполнен! OrderId: {OrderId}", buyResult.OrderId);
            _logger.LogInformation("  Количество: {Qty}, Статус: {Status}",
                buyResult.Order?.Quantity, buyResult.Order?.Status);

            // Проверяем детали исполнения
            await Task.Delay(2000, cancellationToken); // Даем время на обработку
            var buyOrder = await _exchange.GetOrderAsync(buyResult.OrderId!, symbol, cancellationToken);
            if (buyOrder != null)
            {
                _logger.LogInformation("  Исполнено: {Filled}, Средняя цена: ${AvgPrice}",
                    buyOrder.QuantityFilled, buyOrder.AveragePrice);
            }

            // Ждем 15 секунд
            _logger.LogInformation("\n⏳ УДЕРЖИВАЕМ ПОЗИЦИЮ 15 секунд...");
            for (int i = 15; i > 0; i--)
            {
                _logger.LogInformation("  {Seconds} сек...", i);
                await Task.Delay(1000, cancellationToken);
            }

            // Получаем актуальный баланс XRP для точного закрытия
            var balances = await _exchange.GetBalancesAsync(cancellationToken);
            var xrpBalance = balances.FirstOrDefault(b => b.Asset == "XRP");

            decimal sellQuantity = xrpQuantity; // По умолчанию продаем то же количество
            if (xrpBalance != null && xrpBalance.Available > 0)
            {
                sellQuantity = xrpBalance.Available; // Продаем весь доступный баланс
                _logger.LogInformation("Текущий баланс XRP: {Balance}, будем продавать: {SellQty}",
                    xrpBalance.Available, sellQuantity);
            }

            // Округляем количество для продажи до 2 знаков
            sellQuantity = Math.Round(sellQuantity, 2, MidpointRounding.ToZero);

            // Выход из позиции - маркет ордер на продажу
            _logger.LogInformation("\n→ ВЫХОД ИЗ ПОЗИЦИИ: Маркет SELL ордер...");
            var sellResult = await _exchange.PlaceOrderAsync(
                symbol: symbol,
                side: OrderSide.Sell,
                type: OrderType.Market,
                quantity: sellQuantity,
                price: null, // Маркет ордер без цены
                cancellationToken: cancellationToken);

            if (!sellResult.Success)
            {
                _logger.LogError("✗ Не удалось разместить SELL ордер: {Error}", sellResult.ErrorMessage);
                return;
            }

            _logger.LogInformation("✓ SELL ордер исполнен! OrderId: {OrderId}", sellResult.OrderId);

            // Проверяем детали исполнения
            await Task.Delay(2000, cancellationToken);
            var sellOrder = await _exchange.GetOrderAsync(sellResult.OrderId!, symbol, cancellationToken);
            if (sellOrder != null && buyOrder != null)
            {
                _logger.LogInformation("  Исполнено: {Filled}, Средняя цена: ${AvgPrice}",
                    sellOrder.QuantityFilled, sellOrder.AveragePrice);

                // Рассчитываем P&L
                if (buyOrder.AveragePrice.HasValue && sellOrder.AveragePrice.HasValue)
                {
                    decimal buyTotal = buyOrder.QuantityFilled * buyOrder.AveragePrice.Value;
                    decimal sellTotal = sellOrder.QuantityFilled * sellOrder.AveragePrice.Value;
                    decimal pnl = sellTotal - buyTotal;
                    decimal pnlPercent = (pnl / buyTotal) * 100;

                    _logger.LogInformation("\n💰 РЕЗУЛЬТАТ СДЕЛКИ:");
                    _logger.LogInformation("  Покупка: {BuyQty} XRP по ${BuyPrice} = ${BuyTotal} USDT",
                        buyOrder.QuantityFilled, buyOrder.AveragePrice.Value, buyTotal);
                    _logger.LogInformation("  Продажа: {SellQty} XRP по ${SellPrice} = ${SellTotal} USDT",
                        sellOrder.QuantityFilled, sellOrder.AveragePrice.Value, sellTotal);
                    _logger.LogInformation("  P&L: ${PnL} USDT ({PnlPercent:F2}%)", pnl, pnlPercent);
                }
            }

            _logger.LogInformation("\n✓ ТЕСТ 2 ЗАВЕРШЕН УСПЕШНО");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "✗ ТЕСТ 2 ПРОВАЛЕН с ошибкой");
        }
    }
}

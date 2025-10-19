using Microsoft.Extensions.Logging;
using TradingBot.Core.Abstractions;
using TradingBot.Core.Domain;

namespace TradingBot.Core.Services;

/// <summary>
/// Вспомогательный сервис для подготовки к тестированию PMM стратегии
/// Покупает начальный баланс XRP для возможности размещения SELL ордеров
/// </summary>
public sealed class PmmTestHelper
{
    private readonly ILogger<PmmTestHelper> _logger;
    private readonly IExchangeAdapter _exchange;

    public PmmTestHelper(ILogger<PmmTestHelper> logger, IExchangeAdapter exchange)
    {
        _logger = logger;
        _exchange = exchange;
    }

    /// <summary>
    /// Покупает начальное количество XRP для тестирования PMM
    /// </summary>
    public async Task BuyInitialXrpBalanceAsync(decimal usdtAmount, CancellationToken cancellationToken = default)
    {
        const string symbol = "XRPUSDT";

        _logger.LogInformation("=== Подготовка к тестированию PMM ===");
        _logger.LogInformation("Покупаем начальный баланс XRP на сумму ${Amount} USDT", usdtAmount);

        try
        {
            // Получаем текущую цену
            var orderBook = await _exchange.GetOrderBookAsync(symbol, 5, cancellationToken);
            if (!orderBook.BestAsk.HasValue)
            {
                _logger.LogError("Не удалось получить цену Ask");
                return;
            }

            decimal currentPrice = orderBook.BestAsk.Value;
            decimal xrpQuantity = usdtAmount / currentPrice;
            xrpQuantity = Math.Round(xrpQuantity, 2, MidpointRounding.ToZero);

            _logger.LogInformation("Текущая цена XRP: ${Price}", currentPrice);
            _logger.LogInformation("Количество для покупки: {Qty} XRP", xrpQuantity);

            // Размещаем маркет BUY ордер
            var result = await _exchange.PlaceOrderAsync(
                symbol: symbol,
                side: OrderSide.Buy,
                type: OrderType.Market,
                quantity: xrpQuantity,
                price: null,
                cancellationToken: cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("✓ XRP успешно куплен! OrderId: {OrderId}", result.OrderId);

                // Проверяем баланс
                await Task.Delay(2000, cancellationToken); // Ждем обновления баланса
                var balances = await _exchange.GetBalancesAsync(cancellationToken);
                var xrpBalance = balances.FirstOrDefault(b => b.Asset == "XRP");

                if (xrpBalance != null)
                {
                    _logger.LogInformation("Текущий баланс XRP: {Available} (заблокировано: {Locked})",
                        xrpBalance.Available, xrpBalance.Locked);
                }
            }
            else
            {
                _logger.LogError("✗ Не удалось купить XRP: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при покупке начального баланса XRP");
        }
    }

    /// <summary>
    /// Отображает текущие балансы
    /// </summary>
    public async Task ShowBalancesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("\n=== Текущие балансы ===");

        var balances = await _exchange.GetBalancesAsync(cancellationToken);

        foreach (var balance in balances.Where(b => b.Total > 0).OrderByDescending(b => b.Total))
        {
            _logger.LogInformation("{Asset}: Available={Available}, Locked={Locked}, Total={Total}",
                balance.Asset, balance.Available, balance.Locked, balance.Total);
        }
    }
}

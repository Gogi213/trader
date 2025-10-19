using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Abstractions;

namespace TradingBot.Core.Services;

/// <summary>
/// Сервис для очистки открытых ордеров
/// </summary>
public sealed class OrderCleanupService
{
    private readonly ILogger<OrderCleanupService> _logger;
    private readonly IExchangeAdapter _exchange;

    public OrderCleanupService(ILogger<OrderCleanupService> logger, IExchangeAdapter exchange)
    {
        _logger = logger;
        _exchange = exchange;
    }

    /// <summary>
    /// Отменяет все открытые ордера для указанного символа
    /// </summary>
    public async Task CancelAllOpenOrdersAsync(string symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Отмена всех открытых ордеров для {Symbol} ===", symbol);

        try
        {
            var openOrders = await _exchange.GetOpenOrdersAsync(symbol, cancellationToken);

            _logger.LogInformation("Найдено {Count} открытых ордеров", openOrders.Count());

            foreach (var order in openOrders)
            {
                if (order.OrderId != null)
                {
                    _logger.LogInformation("Отмена ордера: {OrderId} | {Side} {Qty} @ {Price}",
                        order.OrderId, order.Side, order.Quantity, order.Price);

                    var success = await _exchange.CancelOrderAsync(order.OrderId, symbol, cancellationToken);

                    if (success)
                    {
                        _logger.LogInformation("  ✓ Ордер {OrderId} отменен", order.OrderId);
                    }
                    else
                    {
                        _logger.LogWarning("  ✗ Не удалось отменить ордер {OrderId}", order.OrderId);
                    }

                    await Task.Delay(500, cancellationToken); // Небольшая задержка между отменами
                }
            }

            _logger.LogInformation("✓ Очистка завершена");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке ордеров");
        }
    }

    /// <summary>
    /// Показывает все открытые ордера
    /// </summary>
    public async Task ShowOpenOrdersAsync(string symbol, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Открытые ордера для {Symbol} ===", symbol);

        try
        {
            var openOrders = await _exchange.GetOpenOrdersAsync(symbol, cancellationToken);

            if (!openOrders.Any())
            {
                _logger.LogInformation("Нет открытых ордеров");
                return;
            }

            _logger.LogInformation("Всего: {Count} ордеров", openOrders.Count());

            foreach (var order in openOrders)
            {
                _logger.LogInformation("  {OrderId} | {Side} {Qty} @ {Price} | Status: {Status}",
                    order.OrderId, order.Side, order.Quantity, order.Price, order.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении открытых ордеров");
        }
    }
}

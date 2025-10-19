using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingBot.Core.Abstractions;
using TradingBot.Core.Domain;

namespace TradingBot.Core.Services;

/// <summary>
/// Sprint 5: Управление рисками
/// Проверяет ордера перед размещением на соответствие лимитам
/// </summary>
public sealed class RiskManager : IRiskManager
{
    private readonly ILogger<RiskManager> _logger;
    private readonly IExchangeAdapter _exchange;
    private readonly RiskManagementOptions _options;

    public RiskManager(
        ILogger<RiskManager> logger,
        IExchangeAdapter exchange,
        IOptions<RiskManagementOptions> options)
    {
        _logger = logger;
        _exchange = exchange;
        _options = options.Value;
    }

    public async Task<RiskCheckResult> CanPlaceOrderAsync(
        OrderSide side,
        decimal price,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Проверка на "fat finger" (слишком большой ордер)
            if (_options.MaxOrderValueUsdt > 0)
            {
                var orderValue = price * quantity;
                if (orderValue > _options.MaxOrderValueUsdt)
                {
                    _logger.LogWarning("Ордер отклонен: стоимость {Value} превышает лимит {Limit}",
                        orderValue, _options.MaxOrderValueUsdt);
                    return RiskCheckResult.Reject($"Order value {orderValue:F2} exceeds limit {_options.MaxOrderValueUsdt:F2}");
                }
            }

            // 2. Проверка достаточности средств
            if (!await HasSufficientFundsAsync(side, price, quantity, cancellationToken))
            {
                // Попытка скорректировать количество
                var maxSize = await GetMaxOrderSizeAsync(side, price, cancellationToken);

                if (maxSize >= _options.MinOrderSize)
                {
                    _logger.LogWarning("Недостаточно средств для {Qty}, скорректировано до {MaxQty}",
                        quantity, maxSize);
                    return RiskCheckResult.Success(maxSize);
                }

                return RiskCheckResult.Reject("Insufficient funds");
            }

            // 3. Проверка минимального размера
            if (quantity < _options.MinOrderSize)
            {
                _logger.LogWarning("Ордер отклонен: размер {Qty} меньше минимума {Min}",
                    quantity, _options.MinOrderSize);
                return RiskCheckResult.Reject($"Order size {quantity} below minimum {_options.MinOrderSize}");
            }

            return RiskCheckResult.Success(quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке рисков");
            return RiskCheckResult.Reject("Risk check failed");
        }
    }

    public async Task<bool> HasSufficientFundsAsync(
        OrderSide side,
        decimal price,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var balances = await _exchange.GetBalancesAsync(cancellationToken);

            if (side == OrderSide.Buy)
            {
                // Для покупки нужен quote asset (USDT)
                var quoteBalance = balances.FirstOrDefault(b => b.Asset == "USDT");
                if (quoteBalance == null)
                {
                    return false;
                }

                var requiredAmount = price * quantity;
                return quoteBalance.Available >= requiredAmount;
            }
            else
            {
                // Для продажи нужен base asset (например, XRP)
                // Извлекаем base asset из символа (например, XRPUSDT -> XRP)
                var baseAsset = ExtractBaseAsset();
                var baseBalance = balances.FirstOrDefault(b => b.Asset == baseAsset);

                if (baseBalance == null)
                {
                    return false;
                }

                return baseBalance.Available >= quantity;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке достаточности средств");
            return false;
        }
    }

    public async Task<decimal> GetMaxOrderSizeAsync(
        OrderSide side,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var balances = await _exchange.GetBalancesAsync(cancellationToken);

            if (side == OrderSide.Buy)
            {
                var quoteBalance = balances.FirstOrDefault(b => b.Asset == "USDT");
                if (quoteBalance == null || quoteBalance.Available <= 0)
                {
                    return 0;
                }

                var maxQuantity = quoteBalance.Available / price;

                // Применяем safety margin (по умолчанию 95% от доступных средств)
                maxQuantity *= _options.SafetyMarginPct / 100m;

                // Округляем вниз до 2 знаков
                return Math.Round(maxQuantity, 2, MidpointRounding.ToZero);
            }
            else
            {
                var baseAsset = ExtractBaseAsset();
                var baseBalance = balances.FirstOrDefault(b => b.Asset == baseAsset);

                if (baseBalance == null || baseBalance.Available <= 0)
                {
                    return 0;
                }

                var maxQuantity = baseBalance.Available * _options.SafetyMarginPct / 100m;
                return Math.Round(maxQuantity, 2, MidpointRounding.ToZero);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при расчете максимального размера ордера");
            return 0;
        }
    }

    private string ExtractBaseAsset()
    {
        // Простое извлечение: убираем "USDT" из конца символа
        // Например: XRPUSDT -> XRP
        // TODO: сделать более универсальным для других пар
        return "XRP"; // Хардкод для текущей пары
    }
}

/// <summary>
/// Опции для Risk Management
/// </summary>
public class RiskManagementOptions
{
    public const string SectionName = "RiskManagement";

    /// <summary>
    /// Максимальная стоимость одного ордера в USDT (0 = без лимита)
    /// </summary>
    public decimal MaxOrderValueUsdt { get; set; } = 10m;

    /// <summary>
    /// Минимальный размер ордера
    /// </summary>
    public decimal MinOrderSize { get; set; } = 0.01m;

    /// <summary>
    /// Safety margin - процент от доступных средств для использования (95% = оставляем 5% резерв)
    /// </summary>
    public decimal SafetyMarginPct { get; set; } = 95m;
}

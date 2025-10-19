using TradingBot.Core.Domain;

namespace TradingBot.Core.Abstractions;

/// <summary>
/// Sprint 5: Интерфейс для управления рисками
/// </summary>
public interface IRiskManager
{
    /// <summary>
    /// Проверяет, можно ли разместить ордер с учетом рисков
    /// </summary>
    Task<RiskCheckResult> CanPlaceOrderAsync(
        OrderSide side,
        decimal price,
        decimal quantity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет достаточность средств для ордера
    /// </summary>
    Task<bool> HasSufficientFundsAsync(
        OrderSide side,
        decimal price,
        decimal quantity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить максимально допустимый размер ордера
    /// </summary>
    Task<decimal> GetMaxOrderSizeAsync(
        OrderSide side,
        decimal price,
        CancellationToken cancellationToken = default);
}

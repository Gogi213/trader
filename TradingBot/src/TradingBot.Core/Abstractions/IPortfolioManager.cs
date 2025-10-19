using TradingBot.Core.Domain;

namespace TradingBot.Core.Abstractions;

/// <summary>
/// Sprint 5: Интерфейс для управления портфелем
/// </summary>
public interface IPortfolioManager
{
    /// <summary>
    /// Текущий PnL в USDT
    /// </summary>
    decimal CurrentPnL { get; }

    /// <summary>
    /// Общая стоимость портфеля в USDT
    /// </summary>
    decimal TotalPortfolioValue { get; }

    /// <summary>
    /// Количество выполненных сделок
    /// </summary>
    int TotalTrades { get; }

    /// <summary>
    /// Win rate (процент прибыльных сделок)
    /// </summary>
    decimal WinRate { get; }

    /// <summary>
    /// Инициализировать портфель
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновить состояние портфеля
    /// </summary>
    Task UpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Зарегистрировать выполненную сделку
    /// </summary>
    void RegisterTrade(Trade trade);

    /// <summary>
    /// Получить статистику портфеля
    /// </summary>
    PortfolioStats GetStats();
}

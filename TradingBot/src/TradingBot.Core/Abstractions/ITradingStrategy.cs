using TradingBot.Core.Domain;

namespace TradingBot.Core.Abstractions;

/// <summary>
/// Интерфейс торговой стратегии
/// </summary>
public interface ITradingStrategy
{
    /// <summary>
    /// Название стратегии
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Инициализация стратегии
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Основной цикл стратегии (tick)
    /// Вызывается периодически для создания/обновления ордеров
    /// </summary>
    Task TickAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Остановка стратегии и очистка ресурсов
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

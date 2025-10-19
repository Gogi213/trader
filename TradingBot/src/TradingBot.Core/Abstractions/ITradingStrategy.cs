using System.Threading;
using System.Threading.Tasks;

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
    /// Инициализация стратегии (вызывается один раз при запуске)
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Один тик стратегии - выполняет одну итерацию логики
    /// Вызывается периодически внешним циклом
    /// </summary>
    Task TickAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Остановка стратегии (отменяет активные ордера)
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

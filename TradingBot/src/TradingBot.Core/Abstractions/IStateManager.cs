using TradingBot.Core.Domain;

namespace TradingBot.Core.Abstractions;

/// <summary>
/// Sprint 5: Интерфейс для управления состоянием бота
/// </summary>
public interface IStateManager
{
    /// <summary>
    /// Сохранить текущее состояние
    /// </summary>
    Task SaveStateAsync(BotState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Загрузить сохраненное состояние
    /// </summary>
    Task<BotState?> LoadStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверить наличие сохраненного состояния
    /// </summary>
    bool HasSavedState();
}

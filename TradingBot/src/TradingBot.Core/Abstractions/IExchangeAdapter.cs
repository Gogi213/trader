using TradingBot.Core.Domain;

namespace TradingBot.Core.Abstractions;

/// <summary>
/// Интерфейс для взаимодействия с биржей
/// </summary>
public interface IExchangeAdapter
{
    /// <summary>
    /// Размещение ордера на бирже
    /// </summary>
    Task<OrderResult> PlaceOrderAsync(
        string symbol,
        OrderSide side,
        OrderType type,
        decimal quantity,
        decimal? price = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отмена ордера
    /// </summary>
    Task<bool> CancelOrderAsync(string orderId, string? symbol = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получение списка открытых ордеров
    /// </summary>
    Task<IEnumerable<Order>> GetOpenOrdersAsync(string? symbol = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получение информации об ордере
    /// </summary>
    Task<Order?> GetOrderAsync(string orderId, string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получение балансов аккаунта
    /// </summary>
    Task<IEnumerable<Balance>> GetBalancesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Получение стакана ордеров
    /// </summary>
    Task<OrderBook> GetOrderBookAsync(string symbol, int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// Тест подключения к API
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}

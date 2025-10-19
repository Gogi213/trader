namespace TradingBot.Core.Domain;

/// <summary>
/// Предложение ордеров для размещения
/// Содержит список bid и ask ордеров, которые стратегия планирует разместить
/// </summary>
public sealed class Proposal
{
    /// <summary>
    /// Список bid (покупка) ордеров для размещения
    /// </summary>
    public List<ProposedOrder> Buys { get; init; } = new();

    /// <summary>
    /// Список ask (продажа) ордеров для размещения
    /// </summary>
    public List<ProposedOrder> Sells { get; init; } = new();

    /// <summary>
    /// Общее количество ордеров в предложении
    /// </summary>
    public int TotalOrders => Buys.Count + Sells.Count;
}

/// <summary>
/// Предлагаемый ордер для размещения
/// </summary>
public sealed class ProposedOrder
{
    /// <summary>
    /// Символ торговой пары (например, BTCUSDT)
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Сторона ордера (Buy/Sell)
    /// </summary>
    public required OrderSide Side { get; init; }

    /// <summary>
    /// Тип ордера (Limit/Market)
    /// </summary>
    public required OrderType Type { get; init; }

    /// <summary>
    /// Цена ордера (для лимитных ордеров)
    /// </summary>
    public decimal Price { get; init; }

    /// <summary>
    /// Количество базового актива
    /// </summary>
    public decimal Quantity { get; init; }

    /// <summary>
    /// Уровень ордера (0 - ближайший к рынку, 1, 2, ... - дальше)
    /// Используется для множественных уровней ордеров
    /// </summary>
    public int Level { get; init; }

    public override string ToString()
    {
        return $"{Side} {Quantity} {Symbol} @ {Price:F8} (Level: {Level})";
    }
}

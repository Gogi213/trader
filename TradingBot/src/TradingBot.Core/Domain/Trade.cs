namespace TradingBot.Core.Domain;

/// <summary>
/// Sprint 5: Модель выполненной сделки
/// </summary>
public class Trade
{
    public string? TradeId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public OrderSide Side { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Commission { get; set; }
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Стоимость сделки в quote currency
    /// </summary>
    public decimal QuoteAmount => Price * Quantity;

    /// <summary>
    /// Чистая прибыль/убыток (для закрывающих сделок)
    /// </summary>
    public decimal? PnL { get; set; }
}

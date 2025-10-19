namespace TradingBot.Core.Domain;

/// <summary>
/// Sprint 5: Состояние бота для сохранения/восстановления
/// </summary>
public class BotState
{
    public DateTime Timestamp { get; set; }
    public string Market { get; set; } = string.Empty;
    public List<Order> ActiveOrders { get; set; } = new();
    public List<Trade> TradeHistory { get; set; } = new();
    public decimal InitialPortfolioValue { get; set; }
    public decimal CurrentPnL { get; set; }
    public int TotalTrades { get; set; }
}

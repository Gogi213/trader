namespace TradingBot.Core.Domain;

/// <summary>
/// Sprint 5: Статистика портфеля
/// </summary>
public class PortfolioStats
{
    public decimal TotalValue { get; set; }
    public decimal CurrentPnL { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal LargestWin { get; set; }
    public decimal LargestLoss { get; set; }
    public int ActiveOrders { get; set; }
    public DateTime SessionStart { get; set; }
    public TimeSpan SessionDuration { get; set; }
}

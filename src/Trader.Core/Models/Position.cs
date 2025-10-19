namespace Trader.Core.Models;

/// <summary>
/// Represents an open trading position
/// </summary>
public class Position
{
    public string Symbol { get; set; } = string.Empty;
    public decimal BuyPrice { get; set; }
    public decimal Quantity { get; set; }
    public DateTime OpenTime { get; set; }
    public decimal TargetSellPrice { get; set; }
    public bool IsOpen { get; set; }

    public Position()
    {
    }

    public Position(string symbol, decimal buyPrice, decimal quantity, decimal targetSellPrice)
    {
        Symbol = symbol;
        BuyPrice = buyPrice;
        Quantity = quantity;
        TargetSellPrice = targetSellPrice;
        OpenTime = DateTime.UtcNow;
        IsOpen = true;
    }

    public decimal CalculateProfitPercent(decimal currentPrice)
    {
        if (BuyPrice == 0) return 0;
        return ((currentPrice - BuyPrice) / BuyPrice) * 100;
    }

    public decimal CalculateProfitUsdt(decimal currentPrice)
    {
        return (currentPrice - BuyPrice) * Quantity;
    }
}

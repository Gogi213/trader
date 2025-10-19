namespace Trader.Core.Models;

public class TradingOptions
{
    public const string SectionName = "Trading";

    public decimal OrderAmountUsdt { get; set; }
    public decimal TargetSpreadPercentage { get; set; }
    public decimal OrderUpdateThresholdPercent { get; set; } = 0.01m;
}
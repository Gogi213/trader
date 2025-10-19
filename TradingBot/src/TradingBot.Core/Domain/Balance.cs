namespace TradingBot.Core.Domain;

public class Balance
{
    public string Asset { get; set; } = string.Empty;
    public decimal Available { get; set; }
    public decimal Locked { get; set; }
    public decimal Total => Available + Locked;
}

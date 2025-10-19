namespace TradingBot.Core.Domain;

public class SpreadDto
{
    public string Exchange { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public decimal BestBid { get; set; }
    public decimal BestAsk { get; set; }
    public decimal SpreadPercentage { get; set; }
    public decimal MinVolume { get; set; }
    public decimal MaxVolume { get; set; }
}
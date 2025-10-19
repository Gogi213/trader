namespace TradingBot.Core.Domain;

public class OrderBookEntry
{
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
}

public class OrderBook
{
    public string Symbol { get; set; } = string.Empty;
    public List<OrderBookEntry> Bids { get; set; } = new();
    public List<OrderBookEntry> Asks { get; set; } = new();
    public DateTime Timestamp { get; set; }

    public decimal? BestBid => Bids.FirstOrDefault()?.Price;
    public decimal? BestAsk => Asks.FirstOrDefault()?.Price;
    public decimal? MidPrice => BestBid.HasValue && BestAsk.HasValue
        ? (BestBid.Value + BestAsk.Value) / 2
        : null;
}

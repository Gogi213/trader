using System.Collections.Generic;

namespace TradingBot.Core.Domain;

/// <summary>
/// Represents a price and size pair for an order.
/// </summary>
/// <param name="Price">The price of the order.</param>
/// <param name="Size">The size/amount of the order.</param>
public record PriceSize(decimal Price, decimal Size);

/// <summary>
/// Represents a set of proposed orders (bids and asks) to be placed on the market.
/// </summary>
public class Proposal
{
    public List<PriceSize> Bids { get; } = new();
    
    public List<PriceSize> Asks { get; } = new();
}

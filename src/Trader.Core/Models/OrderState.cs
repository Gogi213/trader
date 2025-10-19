namespace Trader.Core.Models;

/// <summary>
/// Tracks the current state of active orders
/// </summary>
public class OrderState
{
    /// <summary>
    /// Active BID (buy) order ID
    /// </summary>
    public string? BidOrderId { get; set; }

    /// <summary>
    /// Active BID order price
    /// </summary>
    public decimal? BidPrice { get; set; }

    /// <summary>
    /// Active ASK (sell) order ID
    /// </summary>
    public string? AskOrderId { get; set; }

    /// <summary>
    /// Active ASK order price
    /// </summary>
    public decimal? AskPrice { get; set; }

    public bool HasActiveBidOrder => !string.IsNullOrEmpty(BidOrderId);
    public bool HasActiveAskOrder => !string.IsNullOrEmpty(AskOrderId);

    public void ClearBidOrder()
    {
        BidOrderId = null;
        BidPrice = null;
    }

    public void ClearAskOrder()
    {
        AskOrderId = null;
        AskPrice = null;
    }

    public void SetBidOrder(string orderId, decimal price)
    {
        BidOrderId = orderId;
        BidPrice = price;
    }

    public void SetAskOrder(string orderId, decimal price)
    {
        AskOrderId = orderId;
        AskPrice = price;
    }
}

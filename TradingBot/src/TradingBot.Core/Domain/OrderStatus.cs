namespace TradingBot.Core.Domain;

public enum OrderStatus
{
    New,
    PartiallyFilled,
    Filled,
    Canceled,
    Rejected,
    Expired
}

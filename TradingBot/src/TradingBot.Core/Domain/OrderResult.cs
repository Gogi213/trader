namespace TradingBot.Core.Domain;

public class OrderResult
{
    public bool Success { get; set; }
    public string? OrderId { get; set; }
    public string? ErrorMessage { get; set; }
    public Order? Order { get; set; }

    public static OrderResult Successful(string orderId, Order order)
    {
        return new OrderResult
        {
            Success = true,
            OrderId = orderId,
            Order = order
        };
    }

    public static OrderResult Failed(string errorMessage)
    {
        return new OrderResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}

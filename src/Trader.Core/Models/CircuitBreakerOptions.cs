namespace Trader.Core.Models;

public class CircuitBreakerOptions
{
    public const string SectionName = "CircuitBreaker";

    public decimal MinSpreadPercentage { get; set; }
    public decimal MaxSpreadPercentage { get; set; }
}
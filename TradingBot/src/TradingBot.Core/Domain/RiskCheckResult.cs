namespace TradingBot.Core.Domain;

/// <summary>
/// Sprint 5: Результат проверки рисков
/// </summary>
public class RiskCheckResult
{
    public bool Approved { get; set; }
    public string? Reason { get; set; }
    public decimal AdjustedQuantity { get; set; }

    public static RiskCheckResult Success(decimal quantity) => new()
    {
        Approved = true,
        AdjustedQuantity = quantity
    };

    public static RiskCheckResult Reject(string reason) => new()
    {
        Approved = false,
        Reason = reason,
        AdjustedQuantity = 0
    };
}

using Trader.Core.Models;

namespace Trader.Core.Abstractions;

/// <summary>
/// Defines the contract for the core trading strategy service.
/// </summary>
public interface ITradingStrategyService
{
    /// <summary>
    /// Starts the trading strategy for the specified symbol.
    /// </summary>
    /// <param name="symbol">The trading symbol to operate on.</param>
    /// <param name="ct">A token to signal when to stop the strategy.</param>
    Task ExecuteAsync(TradingSymbol symbol, CancellationToken ct);
}
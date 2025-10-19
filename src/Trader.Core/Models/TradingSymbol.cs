namespace Trader.Core.Models;

/// <summary>
/// Represents a trading symbol with its relevant properties.
/// </summary>
public record TradingSymbol(string Name, decimal Spread, decimal Volume);
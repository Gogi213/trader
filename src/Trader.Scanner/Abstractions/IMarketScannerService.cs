using Trader.Core.Models;

namespace Trader.Scanner.Abstractions;

/// <summary>
/// Defines the contract for a service that scans the market to find suitable trading instruments.
/// </summary>
public interface IMarketScannerService
{
    /// <summary>
    /// Scans the market and returns the most promising trading symbol based on predefined criteria.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="TradingSymbol"/> or null if no suitable symbol is found.</returns>
    Task<TradingSymbol?> FindBestSymbolAsync(CancellationToken ct = default);
}
using Microsoft.Extensions.Logging;
using Trader.Core.Models;

namespace Trader.Core.Services;

/// <summary>
/// Manages the current trading position
/// </summary>
public class PositionManager
{
    private readonly ILogger<PositionManager> _logger;
    private Position? _currentPosition;

    public PositionManager(ILogger<PositionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Opens a new position
    /// </summary>
    public void OpenPosition(string symbol, decimal buyPrice, decimal quantity, decimal targetSpread)
    {
        if (_currentPosition != null && _currentPosition.IsOpen)
        {
            _logger.LogWarning("Attempted to open a new position while one is already open. Closing existing position first.");
            ClosePosition();
        }

        var targetSellPrice = CalculateTargetPrice(buyPrice, targetSpread);
        _currentPosition = new Position(symbol, buyPrice, quantity, targetSellPrice);

        _logger.LogInformation(
            "Position opened: Symbol={Symbol}, BuyPrice={BuyPrice}, Quantity={Quantity}, TargetSellPrice={TargetSellPrice}",
            symbol, buyPrice, quantity, targetSellPrice);
    }

    /// <summary>
    /// Closes the current position
    /// </summary>
    public void ClosePosition()
    {
        if (_currentPosition == null)
        {
            _logger.LogWarning("Attempted to close position but no position is open.");
            return;
        }

        _currentPosition.IsOpen = false;
        _logger.LogInformation("Position closed: Symbol={Symbol}, BuyPrice={BuyPrice}",
            _currentPosition.Symbol, _currentPosition.BuyPrice);

        _currentPosition = null;
    }

    /// <summary>
    /// Gets the current position or null if no position is open
    /// </summary>
    public Position? GetCurrentPosition()
    {
        return _currentPosition?.IsOpen == true ? _currentPosition : null;
    }

    /// <summary>
    /// Calculates the target sell price based on buy price and target spread
    /// </summary>
    public decimal CalculateTargetPrice(decimal buyPrice, decimal targetSpreadPercent)
    {
        return buyPrice * (1 + targetSpreadPercent / 100m);
    }

    /// <summary>
    /// Checks if there's an open position
    /// </summary>
    public bool HasOpenPosition()
    {
        return _currentPosition != null && _currentPosition.IsOpen;
    }

    /// <summary>
    /// Updates the target sell price (e.g., when market moves favorably)
    /// </summary>
    public void UpdateTargetSellPrice(decimal newTargetPrice)
    {
        if (_currentPosition == null || !_currentPosition.IsOpen)
        {
            _logger.LogWarning("Cannot update target sell price: no open position");
            return;
        }

        if (newTargetPrice <= _currentPosition.BuyPrice)
        {
            _logger.LogWarning("Cannot set target sell price ({NewPrice}) below buy price ({BuyPrice})",
                newTargetPrice, _currentPosition.BuyPrice);
            return;
        }

        _logger.LogDebug("Updating target sell price from {OldPrice} to {NewPrice}",
            _currentPosition.TargetSellPrice, newTargetPrice);

        _currentPosition.TargetSellPrice = newTargetPrice;
    }
}

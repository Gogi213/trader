using Microsoft.Extensions.Logging;
using TradingBot.Core.Abstractions;
using TradingBot.Core.Domain;

namespace TradingBot.Core.Services;

/// <summary>
/// Sprint 5: Управление портфелем и отслеживание PnL
/// </summary>
public sealed class PortfolioManager : IPortfolioManager
{
    private readonly ILogger<PortfolioManager> _logger;
    private readonly IExchangeAdapter _exchange;
    private readonly List<Trade> _trades = new();
    private readonly DateTime _sessionStart = DateTime.UtcNow;

    private decimal _initialPortfolioValue;
    private decimal _currentPortfolioValue;

    public decimal CurrentPnL => _currentPortfolioValue - _initialPortfolioValue;
    public decimal TotalPortfolioValue => _currentPortfolioValue;
    public int TotalTrades => _trades.Count;

    public decimal WinRate
    {
        get
        {
            if (_trades.Count == 0) return 0;
            var winningTrades = _trades.Count(t => t.PnL.HasValue && t.PnL.Value > 0);
            return (decimal)winningTrades / _trades.Count * 100m;
        }
    }

    public PortfolioManager(
        ILogger<PortfolioManager> logger,
        IExchangeAdapter exchange)
    {
        _logger = logger;
        _exchange = exchange;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("=== Инициализация Portfolio Manager ===");

        _initialPortfolioValue = await CalculatePortfolioValueAsync(cancellationToken);
        _currentPortfolioValue = _initialPortfolioValue;

        _logger.LogInformation("Начальная стоимость портфеля: {Value:F2} USDT", _initialPortfolioValue);
    }

    public async Task UpdateAsync(CancellationToken cancellationToken = default)
    {
        _currentPortfolioValue = await CalculatePortfolioValueAsync(cancellationToken);
    }

    public void RegisterTrade(Trade trade)
    {
        _trades.Add(trade);
        _logger.LogInformation("Зарегистрирована сделка: {Side} {Qty} @ {Price}, PnL: {PnL:F4}",
            trade.Side, trade.Quantity, trade.Price, trade.PnL ?? 0);
    }

    public PortfolioStats GetStats()
    {
        var tradesWithPnL = _trades.Where(t => t.PnL.HasValue).ToList();
        var winningTrades = tradesWithPnL.Where(t => t.PnL!.Value > 0).ToList();
        var losingTrades = tradesWithPnL.Where(t => t.PnL!.Value < 0).ToList();

        return new PortfolioStats
        {
            TotalValue = _currentPortfolioValue,
            CurrentPnL = CurrentPnL,
            TotalTrades = _trades.Count,
            WinningTrades = winningTrades.Count,
            LosingTrades = losingTrades.Count,
            WinRate = WinRate,
            LargestWin = winningTrades.Any() ? winningTrades.Max(t => t.PnL!.Value) : 0,
            LargestLoss = losingTrades.Any() ? losingTrades.Min(t => t.PnL!.Value) : 0,
            SessionStart = _sessionStart,
            SessionDuration = DateTime.UtcNow - _sessionStart
        };
    }

    private async Task<decimal> CalculatePortfolioValueAsync(CancellationToken cancellationToken)
    {
        try
        {
            var balances = await _exchange.GetBalancesAsync(cancellationToken);

            // USDT баланс
            var usdtBalance = balances.FirstOrDefault(b => b.Asset == "USDT");
            var usdtValue = usdtBalance?.Total ?? 0;

            // Получаем текущую цену для конвертации других активов
            var orderBook = await _exchange.GetOrderBookAsync("XRPUSDT", 5, cancellationToken);
            var midPrice = orderBook.MidPrice ?? 0;

            // XRP баланс в USDT
            var xrpBalance = balances.FirstOrDefault(b => b.Asset == "XRP");
            var xrpValue = (xrpBalance?.Total ?? 0) * midPrice;

            var totalValue = usdtValue + xrpValue;

            _logger.LogDebug("Portfolio Value: USDT={USDT:F2}, XRP={XRP:F4} (@ {Price:F4}) = {Total:F2} USDT",
                usdtValue, xrpBalance?.Total ?? 0, midPrice, totalValue);

            return totalValue;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при расчете стоимости портфеля");
            return _currentPortfolioValue; // Возвращаем предыдущее значение
        }
    }
}

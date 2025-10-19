using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Trader.Core.Abstractions;
using Trader.Core.Models;

namespace Trader.ConsoleHost;

public class TraderWorker : BackgroundService
{
    private readonly ILogger<TraderWorker> _logger;
    private readonly ITradingStrategyService _tradingStrategy;
    private readonly IConfiguration _configuration;

    public TraderWorker(
        ILogger<TraderWorker> logger,
        ITradingStrategyService tradingStrategy,
        IConfiguration configuration)
    {
        _logger = logger;
        _tradingStrategy = tradingStrategy;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TraderWorker starting at: {time}", DateTimeOffset.Now);

        var symbolName = _configuration["TradingSymbol"];

        if (string.IsNullOrWhiteSpace(symbolName))
        {
            _logger.LogError("TradingSymbol is not configured in appsettings.json");
            return;
        }

        _logger.LogInformation("Trading will start on symbol: {symbol}", symbolName);

        var symbol = new TradingSymbol(symbolName, 0, 0); // Spread and volume will be determined dynamically
        await _tradingStrategy.ExecuteAsync(symbol, stoppingToken);
    }
}
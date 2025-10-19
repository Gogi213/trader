using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Abstractions;

namespace TradingBot.ConsoleHost;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ITradingStrategyService _tradingStrategyService;
    private readonly IPortfolioManager? _portfolioManager; // Sprint 5: опционально

    public Worker(
        ILogger<Worker> logger,
        ITradingStrategyService tradingStrategyService,
        IPortfolioManager? portfolioManager = null)
    {
        _logger = logger;
        _tradingStrategyService = tradingStrategyService;
        _portfolioManager = portfolioManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

        try
        {
            // Sprint 5: Инициализация портфеля
            if (_portfolioManager != null)
            {
                await _portfolioManager.InitializeAsync(stoppingToken);
            }

            await _tradingStrategyService.StartAsync(stoppingToken);

            // Sprint 5: Периодический вывод статистики (каждые 60 секунд)
            if (_portfolioManager != null)
            {
                _ = Task.Run(async () =>
                {
                    while (!stoppingToken.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                        await _portfolioManager.UpdateAsync(stoppingToken);

                        var stats = _portfolioManager.GetStats();
                        _logger.LogInformation("=== СТАТИСТИКА ПОРТФЕЛЯ ===");
                        _logger.LogInformation("Общая стоимость: {Value:F2} USDT", stats.TotalValue);
                        _logger.LogInformation("PnL: {PnL:F4} USDT ({PnLPct:F2}%)",
                            stats.CurrentPnL,
                            stats.TotalValue > 0 ? (stats.CurrentPnL / stats.TotalValue * 100) : 0);
                        _logger.LogInformation("Сделок: {Total} (W:{Win} / L:{Loss}), Win Rate: {WinRate:F1}%",
                            stats.TotalTrades, stats.WinningTrades, stats.LosingTrades, stats.WinRate);
                        _logger.LogInformation("Время работы: {Duration}", stats.SessionDuration.ToString(@"hh\:mm\:ss"));
                    }
                }, stoppingToken);
            }

            // Keep the worker alive until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Worker stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "A critical error occurred in the worker.");
        }
        finally
        {
            await _tradingStrategyService.StopAsync(CancellationToken.None);
            _logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
        }
    }
}
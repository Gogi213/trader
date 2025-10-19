using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Abstractions;

namespace TradingBot.Core.Services;

/// <summary>
/// Сервис управления торговыми стратегиями
/// Запускает и управляет жизненным циклом всех стратегий
/// </summary>
public class TradingStrategyService : ITradingStrategyService
{
    private readonly ILogger<TradingStrategyService> _logger;
    private readonly IEnumerable<ITradingStrategy> _strategies;
    private readonly List<Task> _strategyTasks = new();
    private CancellationTokenSource? _cts;

    public TradingStrategyService(ILogger<TradingStrategyService> logger, IEnumerable<ITradingStrategy> strategies)
    {
        _logger = logger;
        _strategies = strategies;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== Запуск Trading Strategy Service ===");

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        foreach (var strategy in _strategies)
        {
            _logger.LogInformation("Запуск стратегии: {StrategyName}", strategy.Name);

            // Инициализация стратегии
            await strategy.InitializeAsync(cancellationToken);

            // Запуск основного цикла стратегии в отдельной задаче
            var task = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await strategy.TickAsync(_cts.Token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка в tick стратегии {StrategyName}", strategy.Name);
                    }

                    // Задержка между тиками (10 секунд)
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10), _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                // Остановка стратегии
                await strategy.StopAsync(CancellationToken.None);

            }, _cts.Token);

            _strategyTasks.Add(task);
        }

        _logger.LogInformation("Все стратегии запущены");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Остановка Trading Strategy Service...");

        _cts?.Cancel();

        try
        {
            await Task.WhenAll(_strategyTasks);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Стратегии остановлены");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при остановке стратегий");
        }

        _cts?.Dispose();
        _logger.LogInformation("Trading Strategy Service остановлен");
    }
}
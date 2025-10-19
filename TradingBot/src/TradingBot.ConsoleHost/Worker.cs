using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Abstractions;
using TradingBot.Core.Services;

namespace TradingBot.ConsoleHost;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IWebSocketConsumerService _webSocketConsumerService;
    private readonly ITradingStrategyService _tradingStrategyService;
    private readonly ITradingStrategy _pmmStrategy;
    private readonly MexcApiTester _mexcApiTester;
    private readonly PmmTestHelper _pmmTestHelper;

    public Worker(ILogger<Worker> logger,
        IWebSocketConsumerService webSocketConsumerService,
        ITradingStrategyService tradingStrategyService,
        ITradingStrategy pmmStrategy,
        MexcApiTester mexcApiTester,
        PmmTestHelper pmmTestHelper)
    {
        _logger = logger;
        _webSocketConsumerService = webSocketConsumerService;
        _tradingStrategyService = tradingStrategyService;
        _pmmStrategy = pmmStrategy;
        _mexcApiTester = mexcApiTester;
        _pmmTestHelper = pmmTestHelper;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

        // ========================================
        // РЕЖИМ PMM СТРАТЕГИИ С ПОДГОТОВКОЙ
        // ========================================
        _logger.LogInformation("\n\n📊 ЗАПУСК PURE MARKET MAKING СТРАТЕГИИ\n");

        try
        {
            // Шаг 1: Показываем начальные балансы
            await _pmmTestHelper.ShowBalancesAsync(stoppingToken);

            // Шаг 2: Покупаем начальный баланс XRP (1 USDT на XRP)
            _logger.LogInformation("\n--- Подготовка: покупка начального баланса XRP ---");
            await _pmmTestHelper.BuyInitialXrpBalanceAsync(1m, stoppingToken);

            _logger.LogInformation("\n--- Ожидание 5 секунд для обновления баланса ---");
            await Task.Delay(5000, stoppingToken);

            // Шаг 3: Показываем обновленные балансы
            await _pmmTestHelper.ShowBalancesAsync(stoppingToken);

            _logger.LogInformation("\n");

            // Шаг 4: Инициализация стратегии
            await _pmmStrategy.InitializeAsync(stoppingToken);

            _logger.LogInformation("\n🔄 Запуск основного цикла стратегии (Ctrl+C для остановки)\n");

            // Основной цикл - выполняем tick каждые 10 секунд
            // Ограничим 10 итерациями для теста
            int tickCount = 0;
            const int maxTicks = 10;

            while (!stoppingToken.IsCancellationRequested && tickCount < maxTicks)
            {
                try
                {
                    await _pmmStrategy.TickAsync(stoppingToken);
                    tickCount++;
                    _logger.LogInformation("--- Tick {Count}/{Max} завершен ---\n", tickCount, maxTicks);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка в tick цикле");
                }

                if (tickCount < maxTicks)
                {
                    // Задержка между тиками (10 секунд)
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }

            _logger.LogInformation("\n--- Тест завершен после {Count} тиков ---", tickCount);

            // Остановка стратегии
            await _pmmStrategy.StopAsync(CancellationToken.None);

            // Показываем финальные балансы
            _logger.LogInformation("\n");
            await _pmmTestHelper.ShowBalancesAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Работа стратегии остановлена пользователем");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при выполнении PMM стратегии");
        }

        _logger.LogInformation("\n\n✅ Стратегия остановлена");

        // ========================================
        // РЕЖИМ ТЕСТИРОВАНИЯ MEXC API (закомментирован)
        // ========================================
        // _logger.LogInformation("\n\n🧪 ЗАПУСК ТЕСТОВ MEXC API\n");
        //
        // try
        // {
        //     await _mexcApiTester.RunAllTestsAsync(stoppingToken);
        // }
        // catch (Exception ex)
        // {
        //     _logger.LogError(ex, "Критическая ошибка при выполнении тестов MEXC API");
        // }
        //
        // _logger.LogInformation("\n\n✅ Тестирование завершено. Приложение будет остановлено.");
        //
        // // Останавливаем приложение после тестов
        // await Task.Delay(3000, stoppingToken);
        // Environment.Exit(0);
    }
}
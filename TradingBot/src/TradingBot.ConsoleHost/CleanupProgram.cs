using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Services;

namespace TradingBot.ConsoleHost;

/// <summary>
/// Программа для очистки открытых ордеров
/// Запуск: dotnet run --cleanup
/// </summary>
public static class CleanupProgram
{
    public static async Task RunCleanupAsync(string[] args)
    {
        Console.WriteLine("=== Утилита очистки открытых ордеров ===\n");

        // Настройки из конфигурации (хардкод для простоты)
        var apiKey = "mx0vgl2GCBSEJV0C8Q";
        var apiSecret = "256739ba45c04e3da03e4ec52c6521b3";
        var symbol = "XRPUSDT";

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var exchangeLogger = loggerFactory.CreateLogger<MexcExchangeAdapter>();
        var cleanupLogger = loggerFactory.CreateLogger<OrderCleanupService>();

        var exchange = new MexcExchangeAdapter(apiKey, apiSecret, exchangeLogger);
        var cleanup = new OrderCleanupService(cleanupLogger, exchange);

        Console.WriteLine("1. Показать открытые ордера");
        Console.WriteLine("2. Отменить все открытые ордера");
        Console.WriteLine();

        if (args.Length > 0 && args[0] == "--cancel-all")
        {
            // Автоматическая отмена
            await cleanup.CancelAllOpenOrdersAsync(symbol);
        }
        else
        {
            // Показать ордера
            await cleanup.ShowOpenOrdersAsync(symbol);

            Console.WriteLine("\nОтменить все ордера? (y/n)");
            var answer = Console.ReadLine();

            if (answer?.ToLower() == "y")
            {
                await cleanup.CancelAllOpenOrdersAsync(symbol);
            }
        }

        Console.WriteLine("\nГотово!");
    }
}

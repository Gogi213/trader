using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Abstractions;
using TradingBot.Core.Domain;
using TradingBot.Core.Services;
using TradingBot.Core.Strategies;

namespace TradingBot.ConsoleHost;

public static class Program
{
    public static async Task Main(string[] args)
    {
        // Режим очистки ордеров
        if (args.Length > 0 && (args[0] == "--cleanup" || args[0] == "--cancel-all"))
        {
            await CleanupProgram.RunCleanupAsync(args);
            return;
        }

        // Обычный режим
        CreateHostBuilder(args).Build().Run();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<ITradingStrategyService, TradingStrategyService>();

                // Options
                services.Configure<PureMarketMakingOptions>(hostContext.Configuration.GetSection(PureMarketMakingOptions.SectionName));
                services.Configure<RiskManagementOptions>(hostContext.Configuration.GetSection(RiskManagementOptions.SectionName));

                // MEXC API Integration
                services.AddSingleton<IExchangeAdapter>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<MexcExchangeAdapter>>();
                    var apiKey = hostContext.Configuration["Mexc:ApiKey"];
                    var apiSecret = hostContext.Configuration["Mexc:ApiSecret"];

                    if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
                    {
                        throw new InvalidOperationException("MEXC API credentials are not configured.");
                    }

                    return new MexcExchangeAdapter(apiKey, apiSecret, logger);
                });

                // Sprint 5: Risk Management & Portfolio
                services.AddSingleton<IRiskManager, RiskManager>();
                services.AddSingleton<IPortfolioManager, PortfolioManager>();
                services.AddSingleton<IStateManager, JsonStateManager>();

                // Pure Market Making Strategy
                services.AddSingleton<ITradingStrategy, PureMarketMakingStrategy>();

                services.AddHostedService<Worker>();
            });
}

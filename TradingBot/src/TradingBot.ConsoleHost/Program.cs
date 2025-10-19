using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Abstractions;
using TradingBot.Core.Services;
using TradingBot.Core.Strategies;

namespace TradingBot.ConsoleHost;

public static class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<ITradingStrategyService, TradingStrategyService>();

                services.AddSingleton<IWebSocketConsumerService>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<WebSocketConsumerService>>();
                    var connectionString = hostContext.Configuration["ConnectionStrings:WebSocket"];
                    if (string.IsNullOrEmpty(connectionString))
                    {
                        throw new InvalidOperationException("WebSocket connection string is not configured.");
                    }
                    return new WebSocketConsumerService(new Uri(connectionString), logger);
                });

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

                // Pure Market Making Strategy
                services.AddSingleton<ITradingStrategy, PureMarketMakingStrategy>();

                // Testing services
                services.AddSingleton<MexcApiTester>();

                services.AddHostedService<Worker>();
            });
}

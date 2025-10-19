using CryptoExchange.Net.Authentication;
using Mexc.Net.Clients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Trader.ConsoleHost;
using Trader.ExchangeApi;
using Trader.ExchangeApi.Abstractions;
using Trader.Core;
using Trader.Core.Abstractions;
using Trader.Core.Models;
using Trader.Core.Services;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Use a static logger for startup, as the host isn't built yet.
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Configuring host...");
            var host = CreateHostBuilder(args).Build();

            Log.Information("Starting Trader bot host...");
            await host.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly.");
            return 1;
        }
        finally
        {
            Log.Information("Trader bot host stopped.");
            await Log.CloseAndFlushAsync();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.Configure<TradingOptions>(hostContext.Configuration.GetSection(TradingOptions.SectionName));
                services.Configure<CircuitBreakerOptions>(hostContext.Configuration.GetSection(CircuitBreakerOptions.SectionName));

                services.AddMexc(options =>
                {
                    options.ApiCredentials = new ApiCredentials(
                        hostContext.Configuration["Mexc:ApiKey"]!,
                        hostContext.Configuration["Mexc:ApiSecret"]!);
                });

                services.AddSingleton<IMexcRestApiClient, MexcRestApiClient>();
                services.AddSingleton<IMexcSocketApiClient, MexcSocketApiClient>();
                services.AddSingleton<PositionManager>();
                services.AddSingleton<ITradingStrategyService, TradingStrategyService>();

                services.AddHostedService<TraderWorker>();
            })
            .UseSerilog();
}

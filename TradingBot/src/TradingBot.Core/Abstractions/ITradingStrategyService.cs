using System.Threading;
using System.Threading.Tasks;

namespace TradingBot.Core.Abstractions;

public interface ITradingStrategyService
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}
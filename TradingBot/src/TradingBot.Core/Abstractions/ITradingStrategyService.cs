namespace TradingBot.Core.Abstractions;

public interface ITradingStrategyService
{
    void ProcessMessage(string message);
}
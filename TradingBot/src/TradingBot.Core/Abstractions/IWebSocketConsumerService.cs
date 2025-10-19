using System;
using System.Threading.Tasks;

namespace TradingBot.Core.Abstractions;

public interface IWebSocketConsumerService
{
    IObservable<string> MessageReceived { get; }
    Task StartAsync();
}
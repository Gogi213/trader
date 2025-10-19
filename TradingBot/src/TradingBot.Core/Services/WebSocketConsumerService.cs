using System;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Abstractions;
using Websocket.Client;

namespace TradingBot.Core.Services;

public class WebSocketConsumerService : IWebSocketConsumerService, IDisposable
{
    private readonly ILogger<WebSocketConsumerService> _logger;
    private readonly WebsocketClient _client;
    
    public IObservable<string> MessageReceived { get; }

    public WebSocketConsumerService(Uri url, ILogger<WebSocketConsumerService> logger)
    {
        _logger = logger;
        _client = new WebsocketClient(url)
        {
            ReconnectTimeout = TimeSpan.FromSeconds(30)
        };

        _client.ReconnectionHappened.Subscribe(info =>
            _logger.LogInformation("Reconnection happened, type: {type}", info.Type));

        _client.DisconnectionHappened.Subscribe(info =>
            _logger.LogWarning("Disconnection happened, type: {type}", info.Type));

        MessageReceived = _client.MessageReceived
            .Select(msg => msg.Text ?? string.Empty)
            .Where(text => !string.IsNullOrEmpty(text));
    }

    public Task StartAsync()
    {
        _logger.LogInformation("Starting WebSocket client...");
        return _client.Start();
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
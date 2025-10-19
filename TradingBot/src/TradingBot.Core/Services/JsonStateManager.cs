using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Abstractions;
using TradingBot.Core.Domain;

namespace TradingBot.Core.Services;

/// <summary>
/// Sprint 5: Управление состоянием с сохранением в JSON
/// </summary>
public sealed class JsonStateManager : IStateManager
{
    private readonly ILogger<JsonStateManager> _logger;
    private readonly string _stateFilePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonStateManager(ILogger<JsonStateManager> logger)
    {
        _logger = logger;
        _stateFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bot_state.json");
    }

    public async Task SaveStateAsync(BotState state, CancellationToken cancellationToken = default)
    {
        try
        {
            state.Timestamp = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(state, JsonOptions);
            await File.WriteAllTextAsync(_stateFilePath, json, cancellationToken);

            _logger.LogInformation("Состояние сохранено в {Path}", _stateFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении состояния");
        }
    }

    public async Task<BotState?> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                _logger.LogInformation("Файл состояния не найден: {Path}", _stateFilePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(_stateFilePath, cancellationToken);
            var state = JsonSerializer.Deserialize<BotState>(json, JsonOptions);

            if (state != null)
            {
                _logger.LogInformation("Состояние загружено из {Path} (сохранено {Timestamp})",
                    _stateFilePath, state.Timestamp);
            }

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке состояния");
            return null;
        }
    }

    public bool HasSavedState()
    {
        return File.Exists(_stateFilePath);
    }
}

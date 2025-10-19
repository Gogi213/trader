using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Abstractions;
using TradingBot.Core.Domain;
using TradingBot.Core.Infrastructure.Json;

namespace TradingBot.Core.Services;

public class TradingStrategyService : ITradingStrategyService
{
    private readonly ILogger<TradingStrategyService> _logger;
    private const decimal SpreadThreshold = 0.5m;

    public TradingStrategyService(ILogger<TradingStrategyService> logger)
    {
        _logger = logger;
    }

    public void ProcessMessage(string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                _logger.LogWarning("Received empty or null message");
                return;
            }

            var package = JsonSerializer.Deserialize(message, JsonContext.Default.SpreadDataPackage);

            if (package == null)
            {
                _logger.LogWarning("Deserialized package is null");
                return;
            }

            if (!package.Data.Any())
            {
                _logger.LogDebug("Received empty data package");
                return;
            }

            _logger.LogDebug("Received package with {count} data rows, {fields} fields",
                package.Data.Count, package.Fields.Count);

            var spreads = ConvertToDto(package).ToList();

            _logger.LogDebug("Converted {count} spread records", spreads.Count);

            foreach (var spread in spreads)
            {
                if (spread.SpreadPercentage > SpreadThreshold)
                {
                    _logger.LogInformation(
                        "TRADING SIGNAL: BUY {Symbol} on {Exchange} | Best Ask: {Price:F8} | Spread: {Spread:F4}% | Volume: {MinVol:F4}-{MaxVol:F4}",
                        spread.Symbol, spread.Exchange, spread.BestAsk, spread.SpreadPercentage,
                        spread.MinVolume, spread.MaxVolume);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize JSON message. Message preview: {preview}",
                message.Length > 200 ? message.Substring(0, 200) + "..." : message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing message");
        }
    }

    private static IEnumerable<SpreadDto> ConvertToDto(SpreadDataPackage package)
    {
        var fieldMap = package.Fields.Select((field, index) => new { field, index })
            .ToDictionary(x => x.field, x => x.index, StringComparer.OrdinalIgnoreCase);

        foreach (var row in package.Data)
        {
            yield return new SpreadDto
            {
                Exchange = GetValue<string>(row, fieldMap, "exchange") ?? string.Empty,
                Symbol = GetValue<string>(row, fieldMap, "symbol") ?? string.Empty,
                BestBid = GetValue<decimal>(row, fieldMap, "bestBid"),
                BestAsk = GetValue<decimal>(row, fieldMap, "bestAsk"),
                SpreadPercentage = GetValue<decimal>(row, fieldMap, "spreadPercentage"),
                MinVolume = GetValue<decimal>(row, fieldMap, "minVolume"),
                MaxVolume = GetValue<decimal>(row, fieldMap, "maxVolume")
            };
        }
    }

    private static T? GetValue<T>(IReadOnlyList<object> row, IReadOnlyDictionary<string, int> fieldMap, string fieldName)
    {
        try
        {
            if (!fieldMap.TryGetValue(fieldName, out var index))
            {
                return default;
            }

            if (index >= row.Count)
            {
                return default;
            }

            var value = row[index];
            if (value == null)
            {
                return default;
            }

            if (value is JsonElement element)
            {
                return element.Deserialize<T>();
            }

            if (value is T typedValue)
            {
                return typedValue;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }
}
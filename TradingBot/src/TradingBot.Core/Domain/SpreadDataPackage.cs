using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TradingBot.Core.Domain;

public class SpreadDataPackage
{
    [JsonPropertyName("Fields")]
    public List<string> Fields { get; set; } = new();

    [JsonPropertyName("Data")]
    public List<List<object>> Data { get; set; } = new();
}
using System.Collections.Generic;
using System.Text.Json.Serialization;
using TradingBot.Core.Domain;

namespace TradingBot.Core.Infrastructure.Json;

[JsonSerializable(typeof(SpreadDataPackage))]
internal partial class JsonContext : JsonSerializerContext
{
}
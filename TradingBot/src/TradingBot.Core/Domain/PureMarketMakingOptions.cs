namespace TradingBot.Core.Domain;

public class PureMarketMakingOptions
{
    public const string SectionName = "PureMarketMaking";

    public string Exchange { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public decimal BidSpread { get; set; }
    public decimal AskSpread { get; set; }
    public decimal OrderAmount { get; set; }
    public double OrderRefreshTime { get; set; }
    public double OrderRefreshTolerancePct { get; set; }
    public string PriceType { get; set; } = "mid_price";

    // Sprint 4: Advanced features
    public int OrderLevels { get; set; } = 1;
    public decimal OrderLevelSpread { get; set; } = 0.1m;
    public decimal? PriceCeiling { get; set; }
    public decimal? PriceFloor { get; set; }
    public double MaxOrderAge { get; set; } = 0;

    // Sprint 4: Inventory Skew
    public bool InventorySkewEnabled { get; set; } = false;
    public decimal InventoryTargetBasePct { get; set; } = 50m;
    public decimal InventoryRangeMultiplier { get; set; } = 1m;

    // Sprint 4: Ping-Pong
    public bool PingPongEnabled { get; set; } = false;

    // Sprint 4: Filled Order Delay
    public double FilledOrderDelay { get; set; } = 0;
}
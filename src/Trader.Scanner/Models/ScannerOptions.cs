namespace Trader.Scanner.Models;

public class ScannerOptions
{
    public const string SectionName = "Scanner";

    public decimal Min24hVolumeUsdt { get; set; }
    public decimal Min15mVolumeUsdt { get; set; }
    public decimal MinSpreadPercentage { get; set; }
}
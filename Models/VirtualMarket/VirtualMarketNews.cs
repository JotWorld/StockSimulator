namespace StockExchangeSimulator.Models.VirtualMarket
{
    public class VirtualMarketNews
    {
        public DateTime Time { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public decimal ImpactPercent { get; set; }
        public int RemainingTicks { get; set; }
        public bool IsPositive => ImpactPercent > 0m;
    }
}

namespace StockExchangeSimulator.Models.VirtualMarket
{
    public class VirtualOrderBookSnapshot
    {
        public string Ticker { get; set; } = string.Empty;
        public List<VirtualOrderBookLevel> Bids { get; set; } = new();
        public List<VirtualOrderBookLevel> Asks { get; set; } = new();
        public decimal BestBid => Bids.FirstOrDefault()?.Price ?? 0m;
        public decimal BestAsk => Asks.FirstOrDefault()?.Price ?? 0m;
        public decimal Spread => BestBid > 0m && BestAsk > 0m ? BestAsk - BestBid : 0m;
    }
}

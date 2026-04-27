namespace StockExchangeSimulator.Models.VirtualMarket
{
    public class VirtualMarketSnapshot
    {
        public IReadOnlyList<VirtualStockState> Stocks { get; }
        public IReadOnlyList<VirtualMarketNews> News { get; }
        public int Tick { get; }
        public DateTime SimulatedTime { get; }
        public VirtualMarketMode MarketMode { get; }
        public double MarketSentiment { get; }

        public VirtualMarketSnapshot(
            IReadOnlyList<VirtualStockState> stocks,
            IReadOnlyList<VirtualMarketNews> news,
            int tick,
            DateTime simulatedTime,
            VirtualMarketMode marketMode,
            double marketSentiment)
        {
            Stocks = stocks;
            News = news;
            Tick = tick;
            SimulatedTime = simulatedTime;
            MarketMode = marketMode;
            MarketSentiment = marketSentiment;
        }

        public double GetNewsSignal(string ticker)
        {
            double total = 0;

            foreach (var item in News.Where(n =>
                         n.RemainingTicks > 0 &&
                         n.Ticker.Equals(ticker, StringComparison.OrdinalIgnoreCase)))
            {
                double raw = (double)item.ImpactPercent / 100.0;
                double decay = Math.Clamp(item.RemainingTicks / 35.0, 0.20, 1.0);
                total += raw * decay;
            }

            return Math.Clamp(total, -0.18, 0.18);
        }
    }
}

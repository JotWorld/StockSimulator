namespace StockExchangeSimulator.Models
{
    public class RealMarketSettings
    {
        public bool AutoUpdateEnabled { get; set; } = true;
        public int RefreshIntervalSeconds { get; set; } = 15;
    }
}
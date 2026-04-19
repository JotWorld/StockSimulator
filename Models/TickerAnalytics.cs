namespace StockExchangeSimulator.Models
{
    public class TickerAnalytics
    {
        public string Ticker { get; set; } = string.Empty;

        public int BuyTrades { get; set; }
        public int SellTrades { get; set; }

        public int BoughtQuantity { get; set; }
        public int SoldQuantity { get; set; }
        public int OpenQuantity { get; set; }

        public decimal BuyVolume { get; set; }
        public decimal SellVolume { get; set; }

        public decimal Fees { get; set; }
        public decimal RealizedPnL { get; set; }
    }
}
namespace StockExchangeSimulator.Models.VirtualMarket
{
    public class VirtualStockState
    {
        public string Ticker { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal PreviousPrice { get; set; }
        public decimal FairPrice { get; set; }
        public double Volatility { get; set; }
        public double Trend { get; set; }
        public long Volume { get; set; }
        public decimal Change => Price - PreviousPrice;
        public decimal ChangePercent => PreviousPrice <= 0m ? 0m : Change / PreviousPrice * 100m;
    }
}

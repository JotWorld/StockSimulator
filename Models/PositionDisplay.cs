namespace StockExchangeSimulator.Models
{
    public class PositionDisplay
    {
        public string Ticker { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal MarketValue { get; set; }
        public decimal PnL { get; set; }
        public decimal PnLPercent { get; set; }
    }
}
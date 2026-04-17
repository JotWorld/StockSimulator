namespace StockExchangeSimulator.Models
{
    public class Position
    {
        public string Ticker { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal AveragePrice { get; set; }
    }
}
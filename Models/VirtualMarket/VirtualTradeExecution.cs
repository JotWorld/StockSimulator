namespace StockExchangeSimulator.Models.VirtualMarket
{
    public class VirtualTradeExecution
    {
        public DateTime Time { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Buyer { get; set; } = string.Empty;
        public string Seller { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}

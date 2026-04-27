namespace StockExchangeSimulator.Models.VirtualMarket
{
    public class VirtualMarketTrade
    {
        public DateTime Time { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}

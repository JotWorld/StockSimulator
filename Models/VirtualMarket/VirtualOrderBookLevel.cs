namespace StockExchangeSimulator.Models.VirtualMarket
{
    public class VirtualOrderBookLevel
    {
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public int Orders { get; set; }
    }
}

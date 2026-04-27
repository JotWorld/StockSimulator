namespace StockExchangeSimulator.Models.VirtualMarket
{
    public class VirtualOrder
    {
        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Owner { get; set; } = string.Empty;
        public string Ticker { get; set; } = string.Empty;
        public VirtualOrderSide Side { get; set; }
        public decimal LimitPrice { get; set; }
        public int OriginalQuantity { get; set; }
        public int RemainingQuantity { get; set; }
        public string Reason { get; set; } = string.Empty;

        public bool IsFilled => RemainingQuantity <= 0;
    }
}

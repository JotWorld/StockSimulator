namespace StockExchangeSimulator.Models
{
    public class Asset
    {
        public string Ticker { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal CurrentPrice { get; set; }
        public bool IsVirtual { get; set; }
        public decimal Change { get; set; }
        public decimal ChangePercent { get; set; }
    }
}
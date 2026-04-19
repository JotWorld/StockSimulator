namespace StockExchangeSimulator.Models
{
    public class TickerValidationResult
    {
        public string Ticker { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public Asset? Asset { get; set; }
    }
}
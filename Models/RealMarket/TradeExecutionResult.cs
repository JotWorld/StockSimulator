namespace StockExchangeSimulator.Models
{
    public class TradeExecutionResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;

        public decimal GrossAmount { get; set; }
        public decimal Fee { get; set; }
        public decimal NetAmount { get; set; }
        public decimal RealizedPnL { get; set; }
    }
}
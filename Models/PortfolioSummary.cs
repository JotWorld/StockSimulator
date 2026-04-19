namespace StockExchangeSimulator.Models
{
    public class PortfolioSummary
    {
        public decimal Balance { get; set; }
        public decimal PositionsValue { get; set; }
        public decimal TotalValue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public decimal RealizedPnL { get; set; }
        public decimal TotalFees { get; set; }

        public decimal TotalPnL => UnrealizedPnL + RealizedPnL;
    }
}
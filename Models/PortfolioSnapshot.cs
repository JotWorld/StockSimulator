using System;

namespace StockExchangeSimulator.Models
{
    public class PortfolioSnapshot
    {
        public DateTime Timestamp { get; set; }
        public string Source { get; set; } = string.Empty;

        public decimal Balance { get; set; }
        public decimal PositionsValue { get; set; }
        public decimal TotalValue { get; set; }

        public decimal UnrealizedPnL { get; set; }
        public decimal RealizedPnL { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal TotalFees { get; set; }
    }
}
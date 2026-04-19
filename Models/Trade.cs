using System;
using StockExchangeSimulator.Enums;

namespace StockExchangeSimulator.Models
{
    public class Trade
    {
        public DateTime Timestamp { get; set; }
        public string Ticker { get; set; } = string.Empty;
        public TradeType TradeType { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Fee { get; set; }
        public decimal RealizedPnL { get; set; }
        public MarketMode MarketMode { get; set; }

        public decimal TotalAmount => Quantity * Price;
        public decimal GrossAmount => Quantity * Price;

        public decimal NetAmount =>
            TradeType == TradeType.Buy
                ? GrossAmount + Fee
                : GrossAmount - Fee;
    }
}
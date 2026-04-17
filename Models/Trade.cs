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
        public MarketMode MarketMode { get; set; }

        public decimal TotalAmount => Quantity * Price;
    }
}
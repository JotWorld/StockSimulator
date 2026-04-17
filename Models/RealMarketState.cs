using System.Collections.Generic;

namespace StockExchangeSimulator.Models
{
    public class RealMarketState
    {
        public int Version { get; set; } = 1;
        public decimal Balance { get; set; }
        public List<Position> Positions { get; set; } = new();
        public List<Trade> Trades { get; set; } = new();
        public List<string> TrackedTickers { get; set; } = new();
        public RealMarketSettings Settings { get; set; } = new();
    }
}
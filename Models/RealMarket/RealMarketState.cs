using System;
using System.Collections.Generic;

namespace StockExchangeSimulator.Models
{
    public class RealMarketState
    {
        public int Version { get; set; } = 3;
        public decimal Balance { get; set; }
        public List<Position> Positions { get; set; } = new();
        public List<Trade> Trades { get; set; } = new();
        public List<string> TrackedTickers { get; set; } = new();
        public RealMarketSettings Settings { get; set; } = new();

        public DateTime? LastSuccessfulUpdateUtc { get; set; }
        public string LastRefreshStatus { get; set; } = string.Empty;

        public List<PortfolioSnapshot> Snapshots { get; set; } = new();
    }
}
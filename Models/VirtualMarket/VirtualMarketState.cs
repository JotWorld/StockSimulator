using System;
using System.Collections.Generic;
using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.Models.VirtualMarket
{
    public class VirtualMarketState
    {
        public int Version { get; set; } = 1;
        public decimal Balance { get; set; } = 10000m;
        public List<Position> Positions { get; set; } = new();
        public List<Trade> Trades { get; set; } = new();
        public List<PortfolioSnapshot> Snapshots { get; set; } = new();
        public double GameSpeed { get; set; } = 1.0;
        public VirtualMarketMode MarketMode { get; set; } = VirtualMarketMode.Normal;
        public string LastStatus { get; set; } = "Состояние виртуального рынка загружено.";
        public DateTime? LastSavedUtc { get; set; }
    }
}

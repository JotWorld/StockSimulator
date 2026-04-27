using System.Collections.Generic;
using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.Services
{
    public class VirtualMarketService
    {
        public List<Asset> GetAssets()
        {
            return new List<Asset>
            {
                new Asset { Ticker = "ORB", Name = "Orbital Dynamics", CurrentPrice = 95.50m, IsVirtual = true },
                new Asset { Ticker = "NEX", Name = "Nexora Tech", CurrentPrice = 143.20m, IsVirtual = true },
                new Asset { Ticker = "GLX", Name = "Galaxy Trade", CurrentPrice = 67.80m, IsVirtual = true },
                new Asset { Ticker = "ZEN", Name = "ZenBio Labs", CurrentPrice = 210.00m, IsVirtual = true }

            };
        }
    }
}
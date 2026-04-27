using System.Collections.Generic;
using System.Linq;

namespace StockExchangeSimulator.Models
{
    public class Portfolio
    {
        public decimal Balance { get; set; }
        public List<Position> Positions { get; set; } = new();

        public decimal GetTotalValue(List<Asset> assets)
        {
            decimal positionsValue = Positions.Sum(position =>
            {
                var asset = assets.FirstOrDefault(a => a.Ticker == position.Ticker);
                return asset == null ? 0 : asset.CurrentPrice * position.Quantity;
            });

            return Balance + positionsValue;
        }
    }
}
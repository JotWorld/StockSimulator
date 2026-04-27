using StockExchangeSimulator.Services.VirtualMarket.Strategies;

namespace StockExchangeSimulator.Models.VirtualMarket
{
    public class VirtualBot
    {
        public string Name { get; set; } = string.Empty;
        public decimal Cash { get; set; }
        public Dictionary<string, int> Positions { get; set; } = new();
        public ITradingStrategy Strategy { get; set; } = null!;
    }
}

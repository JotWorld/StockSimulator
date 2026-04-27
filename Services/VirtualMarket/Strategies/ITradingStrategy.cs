using StockExchangeSimulator.Models.VirtualMarket;

namespace StockExchangeSimulator.Services.VirtualMarket.Strategies
{
    public interface ITradingStrategy
    {
        string Name { get; }

        IEnumerable<VirtualBotOrder> CreateOrders(
            VirtualBot bot,
            VirtualMarketSnapshot snapshot,
            Random random);
    }
}

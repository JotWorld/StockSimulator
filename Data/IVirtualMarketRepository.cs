using StockExchangeSimulator.Models.VirtualMarket;

namespace StockExchangeSimulator.Data
{
    public interface IVirtualMarketRepository
    {
        VirtualMarketState LoadState();
        void SaveState(VirtualMarketState state);
    }
}

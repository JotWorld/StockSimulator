using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.Data
{
    public interface IRealMarketRepository
    {
        RealMarketState LoadState();
        void SaveState(RealMarketState state);
    }
}
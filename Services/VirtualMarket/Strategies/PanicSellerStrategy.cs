using StockExchangeSimulator.Models.VirtualMarket;

namespace StockExchangeSimulator.Services.VirtualMarket.Strategies
{
    public class PanicSellerStrategy : ITradingStrategy
    {
        public string Name => "Panic Seller";

        public IEnumerable<VirtualBotOrder> CreateOrders(VirtualBot bot, VirtualMarketSnapshot snapshot, Random random)
        {
            foreach (var stock in snapshot.Stocks)
            {
                if (stock.ChangePercent < -1.25m && random.NextDouble() < 0.47)
                {
                    yield return new VirtualBotOrder
                    {
                        BotName = bot.Name,
                        Ticker = stock.Ticker,
                        Side = VirtualOrderSide.Sell,
                        Quantity = random.Next(5, 26),
                        LimitPrice = Math.Round(stock.Price * (1m - 0.006m - (decimal)random.NextDouble() * 0.025m), 2),
                        Reason = Name
                    };
                }
            }
        }
    }
}

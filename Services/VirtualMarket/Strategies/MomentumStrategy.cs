using StockExchangeSimulator.Models.VirtualMarket;

namespace StockExchangeSimulator.Services.VirtualMarket.Strategies
{
    public class MomentumStrategy : ITradingStrategy
    {
        public string Name => "Momentum";

        public IEnumerable<VirtualBotOrder> CreateOrders(VirtualBot bot, VirtualMarketSnapshot snapshot, Random random)
        {
            foreach (var stock in snapshot.Stocks)
            {
                if (stock.ChangePercent > 0.55m && random.NextDouble() < 0.27)
                {
                    yield return new VirtualBotOrder
                    {
                        BotName = bot.Name,
                        Ticker = stock.Ticker,
                        Side = VirtualOrderSide.Buy,
                        Quantity = random.Next(2, 14),
                        LimitPrice = Math.Round(stock.Price * (1m + 0.002m + (decimal)random.NextDouble() * 0.014m), 2),
                        Reason = Name
                    };
                }

                if (stock.ChangePercent < -0.75m && random.NextDouble() < 0.19)
                {
                    yield return new VirtualBotOrder
                    {
                        BotName = bot.Name,
                        Ticker = stock.Ticker,
                        Side = VirtualOrderSide.Sell,
                        Quantity = random.Next(1, 11),
                        LimitPrice = Math.Round(stock.Price * (1m - 0.002m - (decimal)random.NextDouble() * 0.014m), 2),
                        Reason = Name
                    };
                }
            }
        }
    }
}

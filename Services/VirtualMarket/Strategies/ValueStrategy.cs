using StockExchangeSimulator.Models.VirtualMarket;

namespace StockExchangeSimulator.Services.VirtualMarket.Strategies
{
    public class ValueStrategy : ITradingStrategy
    {
        public string Name => "Value";

        public IEnumerable<VirtualBotOrder> CreateOrders(VirtualBot bot, VirtualMarketSnapshot snapshot, Random random)
        {
            foreach (var stock in snapshot.Stocks)
            {
                if (stock.Price < stock.FairPrice * 0.95m && random.NextDouble() < 0.24)
                {
                    yield return new VirtualBotOrder
                    {
                        BotName = bot.Name,
                        Ticker = stock.Ticker,
                        Side = VirtualOrderSide.Buy,
                        Quantity = random.Next(3, 16),
                        LimitPrice = Math.Round(stock.Price * (1m + (decimal)random.NextDouble() * 0.009m), 2),
                        Reason = Name
                    };
                }

                if (stock.Price > stock.FairPrice * 1.07m && random.NextDouble() < 0.20)
                {
                    yield return new VirtualBotOrder
                    {
                        BotName = bot.Name,
                        Ticker = stock.Ticker,
                        Side = VirtualOrderSide.Sell,
                        Quantity = random.Next(2, 13),
                        LimitPrice = Math.Round(stock.Price * (1m - (decimal)random.NextDouble() * 0.009m), 2),
                        Reason = Name
                    };
                }
            }
        }
    }
}

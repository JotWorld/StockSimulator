using StockExchangeSimulator.Models.VirtualMarket;

namespace StockExchangeSimulator.Services.VirtualMarket.Strategies
{
    public class RandomStrategy : ITradingStrategy
    {
        public string Name => "Random";

        public IEnumerable<VirtualBotOrder> CreateOrders(VirtualBot bot, VirtualMarketSnapshot snapshot, Random random)
        {
            if (snapshot.Stocks.Count == 0 || random.NextDouble() > 0.13)
                yield break;

            var stock = snapshot.Stocks[random.Next(snapshot.Stocks.Count)];
            var side = random.NextDouble() > 0.5 ? VirtualOrderSide.Buy : VirtualOrderSide.Sell;
            decimal limitPrice = CreateLimitPrice(stock.Price, side, random, 0.012m, 0.008m);

            yield return new VirtualBotOrder
            {
                BotName = bot.Name,
                Ticker = stock.Ticker,
                Side = side,
                Quantity = random.Next(1, 9),
                LimitPrice = limitPrice,
                Reason = Name
            };
        }

        private static decimal CreateLimitPrice(decimal price, VirtualOrderSide side, Random random, decimal spread, decimal aggressiveness)
        {
            decimal offset = spread * (decimal)random.NextDouble();
            decimal aggressive = aggressiveness * (decimal)random.NextDouble();
            decimal multiplier = side == VirtualOrderSide.Buy
                ? 1m - offset + aggressive
                : 1m + offset - aggressive;

            return Math.Round(Math.Max(0.01m, price * multiplier), 2);
        }
    }
}

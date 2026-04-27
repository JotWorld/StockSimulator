using StockExchangeSimulator.Models.VirtualMarket;

namespace StockExchangeSimulator.Services.VirtualMarket.Strategies
{
    public class NewsStrategy : ITradingStrategy
    {
        public string Name => "News";

        public IEnumerable<VirtualBotOrder> CreateOrders(VirtualBot bot, VirtualMarketSnapshot snapshot, Random random)
        {
            foreach (var stock in snapshot.Stocks)
            {
                double signal = snapshot.GetNewsSignal(stock.Ticker);
                double strength = Math.Abs(signal);

                if (strength < 0.006)
                    continue;

                double reactionChance = Math.Clamp(0.16 + strength * 5.5, 0.18, 0.82);
                if (random.NextDouble() > reactionChance)
                    continue;

                var side = signal > 0 ? VirtualOrderSide.Buy : VirtualOrderSide.Sell;

                decimal strengthDecimal = (decimal)strength;
                decimal randomAggression = (decimal)random.NextDouble() * 0.012m;
                decimal aggression = 0.003m + strengthDecimal * 0.55m + randomAggression;

                // News bots deliberately cross the spread more often. This makes the news move price
                // through the order book instead of directly editing the stock price.
                decimal limitPrice = side == VirtualOrderSide.Buy
                    ? stock.Price * (1m + aggression)
                    : stock.Price * (1m - aggression);

                int quantity = Math.Clamp(
                    random.Next(5, 24) + (int)(strength * 320),
                    3,
                    90);

                yield return new VirtualBotOrder
                {
                    BotName = bot.Name,
                    Ticker = stock.Ticker,
                    Side = side,
                    Quantity = quantity,
                    LimitPrice = Math.Round(Math.Max(0.01m, limitPrice), 2),
                    Reason = signal > 0 ? "Positive News" : "Negative News"
                };

                if (strength > 0.06 && random.NextDouble() < 0.22)
                {
                    decimal followThroughAggression = aggression + 0.006m + (decimal)random.NextDouble() * 0.010m;
                    decimal followThroughPrice = side == VirtualOrderSide.Buy
                        ? stock.Price * (1m + followThroughAggression)
                        : stock.Price * (1m - followThroughAggression);

                    yield return new VirtualBotOrder
                    {
                        BotName = bot.Name,
                        Ticker = stock.Ticker,
                        Side = side,
                        Quantity = Math.Clamp(quantity / 2 + random.Next(2, 12), 1, 70),
                        LimitPrice = Math.Round(Math.Max(0.01m, followThroughPrice), 2),
                        Reason = signal > 0 ? "News Follow-through Buy" : "News Follow-through Sell"
                    };
                }
            }
        }
    }
}

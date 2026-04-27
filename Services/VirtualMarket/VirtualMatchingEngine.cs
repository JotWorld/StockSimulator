using StockExchangeSimulator.Models.VirtualMarket;

namespace StockExchangeSimulator.Services.VirtualMarket
{
    public class VirtualMatchingEngine
    {
        private readonly Dictionary<string, VirtualOrderBook> _books = new(StringComparer.OrdinalIgnoreCase);
        private long _nextOrderId = 1;

        public IReadOnlyDictionary<string, VirtualOrderBook> Books => _books;

        public void EnsureBook(string ticker)
        {
            if (!_books.ContainsKey(ticker))
                _books[ticker] = new VirtualOrderBook(ticker);
        }

        public void Clear()
        {
            _books.Clear();
            _nextOrderId = 1;
        }

        public VirtualOrder SubmitOrder(
            string ticker,
            VirtualOrderSide side,
            decimal limitPrice,
            int quantity,
            string owner,
            string reason,
            DateTime createdAt)
        {
            EnsureBook(ticker);

            var order = new VirtualOrder
            {
                Id = _nextOrderId++,
                CreatedAt = createdAt,
                Owner = owner,
                Ticker = ticker,
                Side = side,
                LimitPrice = Math.Round(Math.Max(0.01m, limitPrice), 2),
                OriginalQuantity = quantity,
                RemainingQuantity = quantity,
                Reason = reason
            };

            _books[ticker].AddOrder(order);
            return order;
        }

        public List<VirtualTradeExecution> Match(string ticker, DateTime time, int maxTrades = 10_000)
        {
            var executions = new List<VirtualTradeExecution>();

            if (!_books.TryGetValue(ticker, out var book))
                return executions;

            int guard = 0;
            while (guard++ < maxTrades)
            {
                var bestBuy = book.GetBestBuy();
                var bestSell = book.GetBestSell();

                if (bestBuy == null || bestSell == null)
                    break;

                if (bestBuy.LimitPrice < bestSell.LimitPrice)
                    break;

                int quantity = Math.Min(bestBuy.RemainingQuantity, bestSell.RemainingQuantity);

                // Standard price rule: resting side gets priority. If buy arrived later than sell,
                // trade at sell price. If sell arrived later than buy, trade at buy price.
                decimal executionPrice = bestBuy.Id > bestSell.Id
                    ? bestSell.LimitPrice
                    : bestBuy.LimitPrice;

                bestBuy.RemainingQuantity -= quantity;
                bestSell.RemainingQuantity -= quantity;

                executions.Add(new VirtualTradeExecution
                {
                    Time = time,
                    Ticker = ticker,
                    Price = executionPrice,
                    Quantity = quantity,
                    Buyer = bestBuy.Owner,
                    Seller = bestSell.Owner,
                    Reason = BuildReason(bestBuy, bestSell)
                });

                book.RemoveFilledOrders();
            }

            return executions;
        }

        public void RemoveOldOrders(DateTime now, TimeSpan maxAge)
        {
            foreach (var book in _books.Values)
                book.RemoveOldOrders(now, maxAge);
        }

        public VirtualOrderBookSnapshot GetSnapshot(string ticker, int depth = 10)
        {
            EnsureBook(ticker);
            return _books[ticker].ToSnapshot(depth);
        }

        private static string BuildReason(VirtualOrder buy, VirtualOrder sell)
        {
            if (buy.Owner == "Player" || sell.Owner == "Player")
                return buy.Owner == "Player" ? "Player Buy" : "Player Sell";

            if (buy.Reason == sell.Reason)
                return buy.Reason;

            if (buy.Reason.Contains("News", StringComparison.OrdinalIgnoreCase) ||
                sell.Reason.Contains("News", StringComparison.OrdinalIgnoreCase))
                return "News";

            if (buy.Reason.Contains("Market Maker", StringComparison.OrdinalIgnoreCase) ||
                sell.Reason.Contains("Market Maker", StringComparison.OrdinalIgnoreCase))
                return "Liquidity";

            return $"{buy.Reason}/{sell.Reason}";
        }
    }
}

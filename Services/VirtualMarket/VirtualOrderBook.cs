using StockExchangeSimulator.Models.VirtualMarket;

namespace StockExchangeSimulator.Services.VirtualMarket
{
    public class VirtualOrderBook
    {
        private readonly List<VirtualOrder> _buyOrders = new();
        private readonly List<VirtualOrder> _sellOrders = new();

        public string Ticker { get; }
        public IReadOnlyList<VirtualOrder> BuyOrders => _buyOrders;
        public IReadOnlyList<VirtualOrder> SellOrders => _sellOrders;

        public VirtualOrderBook(string ticker)
        {
            Ticker = ticker;
        }

        public void AddOrder(VirtualOrder order)
        {
            if (order.RemainingQuantity <= 0 || order.LimitPrice <= 0m)
                return;

            if (order.Side == VirtualOrderSide.Buy)
                _buyOrders.Add(order);
            else
                _sellOrders.Add(order);
        }

        public void RemoveFilledOrders()
        {
            _buyOrders.RemoveAll(o => o.IsFilled);
            _sellOrders.RemoveAll(o => o.IsFilled);
        }

        public void RemoveOldOrders(DateTime now, TimeSpan maxAge)
        {
            _buyOrders.RemoveAll(o => now - o.CreatedAt > maxAge || o.RemainingQuantity <= 0);
            _sellOrders.RemoveAll(o => now - o.CreatedAt > maxAge || o.RemainingQuantity <= 0);
        }

        public VirtualOrder? GetBestBuy()
        {
            return _buyOrders
                .Where(o => !o.IsFilled)
                .OrderByDescending(o => o.LimitPrice)
                .ThenBy(o => o.Id)
                .FirstOrDefault();
        }

        public VirtualOrder? GetBestSell()
        {
            return _sellOrders
                .Where(o => !o.IsFilled)
                .OrderBy(o => o.LimitPrice)
                .ThenBy(o => o.Id)
                .FirstOrDefault();
        }

        public VirtualOrderBookSnapshot ToSnapshot(int depth = 10)
        {
            return new VirtualOrderBookSnapshot
            {
                Ticker = Ticker,
                Bids = _buyOrders
                    .Where(o => !o.IsFilled)
                    .GroupBy(o => o.LimitPrice)
                    .OrderByDescending(g => g.Key)
                    .Take(depth)
                    .Select(g => new VirtualOrderBookLevel
                    {
                        Price = g.Key,
                        Quantity = g.Sum(o => o.RemainingQuantity),
                        Orders = g.Count()
                    })
                    .ToList(),
                Asks = _sellOrders
                    .Where(o => !o.IsFilled)
                    .GroupBy(o => o.LimitPrice)
                    .OrderBy(g => g.Key)
                    .Take(depth)
                    .Select(g => new VirtualOrderBookLevel
                    {
                        Price = g.Key,
                        Quantity = g.Sum(o => o.RemainingQuantity),
                        Orders = g.Count()
                    })
                    .ToList()
            };
        }
    }
}

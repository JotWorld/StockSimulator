using StockExchangeSimulator.Models;
using StockExchangeSimulator.Models.VirtualMarket;
using StockExchangeSimulator.Services.VirtualMarket.Strategies;

namespace StockExchangeSimulator.Services.VirtualMarket
{
    public class VirtualMarketEngine
    {
        private readonly Random _random = new();
        private readonly List<VirtualStockState> _stocks = new();
        private readonly List<VirtualBot> _bots = new();
        private readonly List<VirtualMarketNews> _news = new();
        private readonly List<VirtualMarketTrade> _marketTrades = new();
        private readonly VirtualMatchingEngine _matchingEngine = new();
        private readonly Dictionary<string, decimal> _referencePrices = new(StringComparer.OrdinalIgnoreCase);

        private TimeSpan _accumulatedTime = TimeSpan.Zero;

        public IReadOnlyList<VirtualStockState> Stocks => _stocks;
        public IReadOnlyList<VirtualBot> Bots => _bots;
        public IReadOnlyList<VirtualMarketNews> News => _news;
        public IReadOnlyList<VirtualMarketTrade> MarketTrades => _marketTrades;

        public int Tick { get; private set; }
        public DateTime SimulatedTime { get; private set; } = new(2026, 1, 1, 9, 30, 0);
        public TimeSpan FixedMarketTick { get; set; } = TimeSpan.FromSeconds(1);
        public double GameSpeedMultiplier { get; set; } = 1.0;
        public bool IsRunning { get; private set; }
        public VirtualMarketMode MarketMode { get; set; } = VirtualMarketMode.Normal;
        public double MarketSentiment { get; private set; }

        public event Action? MarketUpdated;

        public VirtualMarketEngine()
        {
            InitializeStocks();
            InitializeOrderBooks();
            InitializeBots(350);
        }

        public void Start() => IsRunning = true;
        public void Pause() => IsRunning = false;

        public void Step()
        {
            ProcessTick();
            MarketUpdated?.Invoke();
        }

        public void Reset()
        {
            _stocks.Clear();
            _bots.Clear();
            _news.Clear();
            _marketTrades.Clear();
            _referencePrices.Clear();
            _matchingEngine.Clear();

            Tick = 0;
            MarketSentiment = 0;
            _accumulatedTime = TimeSpan.Zero;
            SimulatedTime = new DateTime(2026, 1, 1, 9, 30, 0);

            InitializeStocks();
            InitializeOrderBooks();
            InitializeBots(350);
            MarketUpdated?.Invoke();
        }

        public void Advance(TimeSpan realDelta)
        {
            if (!IsRunning || GameSpeedMultiplier <= 0)
                return;

            var simulatedDelta = TimeSpan.FromTicks((long)(realDelta.Ticks * GameSpeedMultiplier));
            _accumulatedTime += simulatedDelta;

            int safetyLimit = 300;
            int processedTicks = 0;

            while (_accumulatedTime >= FixedMarketTick && processedTicks < safetyLimit)
            {
                ProcessTick();
                _accumulatedTime -= FixedMarketTick;
                processedTicks++;
            }

            if (processedTicks > 0)
                MarketUpdated?.Invoke();
        }

        public void ApplyPlayerBuy(string ticker, int quantity)
        {
            SubmitPlayerOrder(ticker, VirtualOrderSide.Buy, quantity);
            MarketUpdated?.Invoke();
        }

        public void ApplyPlayerSell(string ticker, int quantity)
        {
            SubmitPlayerOrder(ticker, VirtualOrderSide.Sell, quantity);
            MarketUpdated?.Invoke();
        }

        public VirtualOrderBookSnapshot GetOrderBookSnapshot(string ticker, int depth = 10)
        {
            return _matchingEngine.GetSnapshot(ticker, depth);
        }

        public List<Asset> ToAssets()
        {
            return _stocks
                .OrderBy(s => s.Ticker)
                .Select(s => new Asset
                {
                    Ticker = s.Ticker,
                    Name = s.Name,
                    CurrentPrice = s.Price,
                    Change = s.Change,
                    ChangePercent = s.ChangePercent,
                    IsVirtual = true,
                    LastUpdatedUtc = SimulatedTime
                })
                .ToList();
        }

        private void ProcessTick()
        {
            Tick++;
            SimulatedTime = SimulatedTime.Add(FixedMarketTick);

            foreach (var stock in _stocks)
            {
                stock.PreviousPrice = stock.Price;
                stock.Volume = 0;
            }

            UpdateMarketSentiment();
            MaybeGenerateNews();
            _matchingEngine.RemoveOldOrders(SimulatedTime, TimeSpan.FromSeconds(45));

            UpdateReferencePrices();
            SeedLiquidityOrders();

            var snapshot = new VirtualMarketSnapshot(_stocks, _news, Tick, SimulatedTime, MarketMode, MarketSentiment);
            SubmitBotOrders(CollectBotOrders(snapshot));
            MatchAllBooks();
            ApplyNoTradePriceDrift();
            UpdateNewsLifetime();
        }

        private List<VirtualBotOrder> CollectBotOrders(VirtualMarketSnapshot snapshot)
        {
            var orders = new List<VirtualBotOrder>();

            foreach (var bot in _bots)
            {
                foreach (var order in bot.Strategy.CreateOrders(bot, snapshot, _random))
                {
                    if (order.Quantity > 0)
                        orders.Add(order);
                }
            }

            return orders;
        }

        private void SubmitBotOrders(List<VirtualBotOrder> orders)
        {
            foreach (var order in orders)
            {
                var stock = _stocks.FirstOrDefault(s => s.Ticker.Equals(order.Ticker, StringComparison.OrdinalIgnoreCase));
                if (stock == null)
                    continue;

                decimal limitPrice = order.LimitPrice > 0m
                    ? order.LimitPrice
                    : CreateAggressiveLimitPrice(stock.Price, order.Side, 0.012m);

                _matchingEngine.SubmitOrder(
                    stock.Ticker,
                    order.Side,
                    limitPrice,
                    order.Quantity,
                    order.BotName,
                    order.Reason,
                    SimulatedTime);
            }
        }

        private void SubmitPlayerOrder(string ticker, VirtualOrderSide side, int quantity)
        {
            var stock = _stocks.FirstOrDefault(s => s.Ticker.Equals(ticker, StringComparison.OrdinalIgnoreCase));
            if (stock == null || quantity <= 0)
                return;

            // Player orders are aggressive limit orders. They still go through the real order book,
            // but are priced far enough from the current price to execute immediately if liquidity exists.
            decimal limitPrice = CreateAggressiveLimitPrice(stock.Price, side, 0.05m);

            _matchingEngine.SubmitOrder(
                stock.Ticker,
                side,
                limitPrice,
                quantity,
                "Player",
                side == VirtualOrderSide.Buy ? "Player Buy" : "Player Sell",
                SimulatedTime);

            MatchTicker(stock);
        }

        private void MatchAllBooks()
        {
            foreach (var stock in _stocks)
                MatchTicker(stock);
        }

        private void MatchTicker(VirtualStockState stock)
        {
            var executions = _matchingEngine.Match(stock.Ticker, SimulatedTime);
            if (executions.Count == 0)
                return;

            decimal weightedValue = executions.Sum(e => e.Price * e.Quantity);
            int totalQuantity = executions.Sum(e => e.Quantity);
            decimal lastPrice = executions[^1].Price;
            decimal averagePrice = totalQuantity <= 0 ? lastPrice : weightedValue / totalQuantity;

            stock.Price = Math.Round(ApplyPriceGuard(stock.Price, lastPrice, stock.Volatility), 2);
            stock.Volume += totalQuantity;

            foreach (var execution in executions)
            {
                AddMarketTrade(
                    execution.Ticker,
                    Math.Round(execution.Price, 2),
                    execution.Quantity,
                    execution.Reason,
                    execution.Buyer,
                    execution.Seller);
            }

            _referencePrices[stock.Ticker] = Math.Round((stock.Price * 0.72m) + (averagePrice * 0.28m), 2);
        }

        private void UpdateReferencePrices()
        {
            foreach (var stock in _stocks)
            {
                double marketDrift = GetMarketModeDrift();
                double newsImpact = GetNewsImpact(stock.Ticker);
                double sentimentImpact = MarketSentiment * 0.0025;
                double fairPricePressure = (double)((stock.FairPrice - stock.Price) / stock.FairPrice) * 0.006;
                double randomShock = NextGaussian() * stock.Volatility * GetMarketModeVolatilityMultiplier() * 0.35;

                double referenceMove = stock.Trend + marketDrift + newsImpact + sentimentImpact + fairPricePressure + randomShock;
                referenceMove = Math.Clamp(referenceMove, -0.035, 0.035);

                decimal referencePrice = stock.Price * (1m + (decimal)referenceMove);
                _referencePrices[stock.Ticker] = Math.Round(Math.Max(1m, referencePrice), 2);
            }
        }

        private void SeedLiquidityOrders()
        {
            foreach (var stock in _stocks)
            {
                decimal reference = _referencePrices.TryGetValue(stock.Ticker, out var value) ? value : stock.Price;
                decimal spread = CalculateSpread(stock);

                int levels = MarketMode == VirtualMarketMode.Crisis ? 4 : 5;
                int baseLiquidity = MarketMode switch
                {
                    VirtualMarketMode.Crisis => 45,
                    VirtualMarketMode.Volatile => 60,
                    _ => 85
                };

                for (int level = 1; level <= levels; level++)
                {
                    decimal levelOffset = spread * level;
                    decimal bidPrice = Math.Round(Math.Max(0.01m, reference * (1m - levelOffset)), 2);
                    decimal askPrice = Math.Round(Math.Max(0.01m, reference * (1m + levelOffset)), 2);
                    int levelLiquidity = Math.Max(5, baseLiquidity + _random.Next(-25, 26) - level * 7);

                    _matchingEngine.SubmitOrder(stock.Ticker, VirtualOrderSide.Buy, bidPrice, levelLiquidity, "MarketMaker", "Market Maker", SimulatedTime);
                    _matchingEngine.SubmitOrder(stock.Ticker, VirtualOrderSide.Sell, askPrice, levelLiquidity, "MarketMaker", "Market Maker", SimulatedTime);
                }

                AddDirectionalLiquidity(stock, reference);
            }
        }

        private void AddDirectionalLiquidity(VirtualStockState stock, decimal reference)
        {
            double pressure = GetDirectionalPressure(stock);
            if (Math.Abs(pressure) < 0.001)
                return;

            bool newsDriven = HasActiveNews(stock.Ticker);
            var side = pressure > 0 ? VirtualOrderSide.Buy : VirtualOrderSide.Sell;

            int quantityMultiplier = newsDriven ? 8200 : 4200;
            int randomExtra = newsDriven ? _random.Next(25, 150) : _random.Next(5, 60);
            int quantity = Math.Clamp((int)(Math.Abs(pressure) * quantityMultiplier) + randomExtra, 10, newsDriven ? 850 : 450);

            decimal aggressionMultiplier = newsDriven ? 1.15m : 0.70m;
            decimal baseAggression = newsDriven ? 0.004m : 0.002m;
            decimal limitPrice = side == VirtualOrderSide.Buy
                ? Math.Round(reference * (1m + (decimal)Math.Abs(pressure) * aggressionMultiplier + baseAggression), 2)
                : Math.Round(reference * (1m - (decimal)Math.Abs(pressure) * aggressionMultiplier - baseAggression), 2);

            _matchingEngine.SubmitOrder(stock.Ticker, side, Math.Max(0.01m, limitPrice), quantity, "MarketFlow", GetFlowReason(stock), SimulatedTime);
        }

        private void ApplyNoTradePriceDrift()
        {
            foreach (var stock in _stocks)
            {
                if (stock.Volume > 0)
                    continue;

                if (!_referencePrices.TryGetValue(stock.Ticker, out var reference))
                    continue;

                decimal driftedPrice = stock.Price + (reference - stock.Price) * 0.08m;
                stock.Price = Math.Round(Math.Max(1m, driftedPrice), 2);
            }
        }

        private double GetDirectionalPressure(VirtualStockState stock)
        {
            double marketPressure = GetMarketModeDrift() * 3.0;
            double newsPressure = GetNewsImpact(stock.Ticker) * 2.15;
            double sentimentPressure = MarketSentiment * 0.003;
            double fairPressure = (double)((stock.FairPrice - stock.Price) / stock.FairPrice) * 0.004;

            return Math.Clamp(marketPressure + newsPressure + sentimentPressure + fairPressure, -0.065, 0.065);
        }

        private void AddImmediateNewsReactionOrders(VirtualStockState stock, VirtualMarketNews news)
        {
            var side = news.ImpactPercent >= 0 ? VirtualOrderSide.Buy : VirtualOrderSide.Sell;
            decimal magnitude = Math.Clamp(Math.Abs(news.ImpactPercent) / 100m, 0.01m, 0.16m);

            int waves = MarketMode switch
            {
                VirtualMarketMode.Crisis => 4,
                VirtualMarketMode.Volatile => 4,
                _ => 3
            };

            for (int i = 0; i < waves; i++)
            {
                decimal aggression = 0.008m + magnitude * 0.85m + (decimal)_random.NextDouble() * 0.012m;
                decimal limitPrice = side == VirtualOrderSide.Buy
                    ? stock.Price * (1m + aggression)
                    : stock.Price * (1m - aggression);

                int quantity = Math.Clamp((int)(magnitude * 2600m) + _random.Next(45, 190), 35, 700);

                _matchingEngine.SubmitOrder(
                    stock.Ticker,
                    side,
                    Math.Round(Math.Max(0.01m, limitPrice), 2),
                    quantity,
                    "NewsFlow",
                    news.ImpactPercent >= 0 ? "Positive News Shock" : "Negative News Shock",
                    SimulatedTime);
            }
        }

        private bool HasActiveNews(string ticker)
        {
            return _news.Any(n =>
                n.RemainingTicks > 0 &&
                n.Ticker.Equals(ticker, StringComparison.OrdinalIgnoreCase));
        }

        private string GetFlowReason(VirtualStockState stock)
        {
            if (_news.Any(n => n.Ticker == stock.Ticker && n.RemainingTicks > 0))
                return "News Flow";

            return MarketMode switch
            {
                VirtualMarketMode.Bull => "Bull Flow",
                VirtualMarketMode.Bear => "Bear Flow",
                VirtualMarketMode.Crisis => "Crisis Flow",
                VirtualMarketMode.Volatile => "Volatility Flow",
                _ => "Market Flow"
            };
        }

        private decimal ApplyPriceGuard(decimal currentPrice, decimal newPrice, double volatility)
        {
            decimal maxMove = MarketMode switch
            {
                VirtualMarketMode.Crisis => 0.13m,
                VirtualMarketMode.Volatile => 0.11m,
                _ => 0.075m
            };

            maxMove += (decimal)Math.Min(0.04, volatility * 2.0);

            decimal lowerBound = currentPrice * (1m - maxMove);
            decimal upperBound = currentPrice * (1m + maxMove);

            return Math.Clamp(newPrice, lowerBound, upperBound);
        }

        private decimal CalculateSpread(VirtualStockState stock)
        {
            decimal baseSpread = MarketMode switch
            {
                VirtualMarketMode.Crisis => 0.0065m,
                VirtualMarketMode.Volatile => 0.0055m,
                VirtualMarketMode.Bear => 0.0045m,
                _ => 0.0035m
            };

            decimal volatilitySpread = (decimal)stock.Volatility * 0.28m;
            return Math.Clamp(baseSpread + volatilitySpread, 0.0025m, 0.018m);
        }

        private decimal CreateAggressiveLimitPrice(decimal price, VirtualOrderSide side, decimal aggressiveness)
        {
            decimal multiplier = side == VirtualOrderSide.Buy
                ? 1m + aggressiveness
                : 1m - aggressiveness;

            return Math.Round(Math.Max(0.01m, price * multiplier), 2);
        }

        private double GetMarketModeDrift()
        {
            return MarketMode switch
            {
                VirtualMarketMode.Bull => 0.0012,
                VirtualMarketMode.Bear => -0.0012,
                VirtualMarketMode.Crisis => -0.0035,
                _ => 0.0
            };
        }

        private double GetMarketModeVolatilityMultiplier()
        {
            return MarketMode switch
            {
                VirtualMarketMode.Bull => 1.05,
                VirtualMarketMode.Bear => 1.2,
                VirtualMarketMode.Crisis => 2.1,
                VirtualMarketMode.Volatile => 2.4,
                _ => 1.0
            };
        }

        private void UpdateMarketSentiment()
        {
            double target = MarketMode switch
            {
                VirtualMarketMode.Bull => 0.55,
                VirtualMarketMode.Bear => -0.45,
                VirtualMarketMode.Crisis => -0.85,
                _ => 0.0
            };

            double noise = (_random.NextDouble() - 0.5) * 0.08;
            MarketSentiment += (target - MarketSentiment) * 0.03 + noise;
            MarketSentiment = Math.Clamp(MarketSentiment, -1.0, 1.0);
        }

        private void MaybeGenerateNews()
        {
            double chance = MarketMode switch
            {
                VirtualMarketMode.Crisis => 0.095,
                VirtualMarketMode.Volatile => 0.085,
                VirtualMarketMode.Bull => 0.055,
                VirtualMarketMode.Bear => 0.055,
                _ => 0.035
            };

            if (_random.NextDouble() > chance)
                return;

            var stock = _stocks[_random.Next(_stocks.Count)];
            decimal impact = GenerateNewsImpact();

            var news = new VirtualMarketNews
            {
                Time = SimulatedTime,
                Ticker = stock.Ticker,
                Title = GenerateNewsTitle(stock.Ticker, impact),
                ImpactPercent = impact,
                RemainingTicks = _random.Next(14, 48)
            };

            _news.Insert(0, news);
            AddImmediateNewsReactionOrders(stock, news);

            if (_news.Count > 80)
                _news.RemoveAt(_news.Count - 1);
        }

        private decimal GenerateNewsImpact()
        {
            decimal baseImpact = MarketMode switch
            {
                VirtualMarketMode.Crisis => (decimal)(_random.NextDouble() * 10.0 + 4.0),
                VirtualMarketMode.Volatile => (decimal)(_random.NextDouble() * 8.0 + 2.5),
                _ => (decimal)(_random.NextDouble() * 6.0 + 1.5)
            };

            bool positive = MarketMode switch
            {
                VirtualMarketMode.Bull => _random.NextDouble() < 0.68,
                VirtualMarketMode.Bear => _random.NextDouble() < 0.38,
                VirtualMarketMode.Crisis => _random.NextDouble() < 0.22,
                _ => _random.NextDouble() < 0.5
            };

            return Math.Round(positive ? baseImpact : -baseImpact, 2);
        }

        private string GenerateNewsTitle(string ticker, decimal impact)
        {
            string[] positive =
            {
                $"{ticker}: сильный квартальный отчёт",
                $"{ticker}: новый крупный контракт",
                $"{ticker}: успешный запуск продукта",
                $"{ticker}: аналитики повысили прогноз",
                $"{ticker}: рост спроса на продукцию"
            };

            string[] negative =
            {
                $"{ticker}: слабый квартальный отчёт",
                $"{ticker}: задержка запуска продукта",
                $"{ticker}: регуляторные риски",
                $"{ticker}: аналитики снизили прогноз",
                $"{ticker}: давление на маржинальность"
            };

            var source = impact >= 0 ? positive : negative;
            return source[_random.Next(source.Length)];
        }

        private double GetNewsImpact(string ticker)
        {
            double total = 0;

            foreach (var news in _news.Where(n => n.Ticker == ticker && n.RemainingTicks > 0))
            {
                double raw = (double)news.ImpactPercent / 100.0;
                double decayed = raw * Math.Clamp(news.RemainingTicks / 30.0, 0.15, 1.0);
                total += decayed * 0.20;
            }

            return Math.Clamp(total, -0.055, 0.055);
        }

        private void UpdateNewsLifetime()
        {
            foreach (var news in _news)
            {
                if (news.RemainingTicks > 0)
                    news.RemainingTicks--;
            }
        }

        private void AddMarketTrade(string ticker, decimal price, int quantity, string reason, string buyer, string seller)
        {
            _marketTrades.Insert(0, new VirtualMarketTrade
            {
                Time = SimulatedTime,
                Ticker = ticker,
                Price = price,
                Quantity = quantity,
                Reason = $"{reason} ({buyer} → {seller})"
            });

            if (_marketTrades.Count > 200)
                _marketTrades.RemoveAt(_marketTrades.Count - 1);
        }

        private double NextGaussian()
        {
            double u1 = 1.0 - _random.NextDouble();
            double u2 = 1.0 - _random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        private void InitializeStocks()
        {
            _stocks.AddRange(new[]
            {
                CreateStock("ORB", "Orbital Dynamics", 95.50m, 0.0065, 0.00015),
                CreateStock("NEX", "Nexora Tech", 143.20m, 0.0100, 0.00025),
                CreateStock("GLX", "Galaxy Trade", 67.80m, 0.0080, -0.00005),
                CreateStock("ZEN", "ZenBio Labs", 210.00m, 0.0140, 0.00020),
                CreateStock("AUR", "Aurum Robotics", 121.40m, 0.0120, 0.00012),
                CreateStock("VLT", "Voltix Energy", 58.90m, 0.0130, -0.00010),
                CreateStock("MED", "Medion Labs", 184.70m, 0.0085, 0.00010),
                CreateStock("QNT", "Quantum Foods", 76.30m, 0.0065, 0.00000),
                CreateStock("CYB", "CyberGrid Systems", 132.60m, 0.0115, 0.00018),
                CreateStock("HZN", "Horizon Motors", 88.40m, 0.0105, -0.00004),
                CreateStock("LUM", "Lumen Cloud", 156.90m, 0.0120, 0.00022),
                CreateStock("BIO", "Biomatrix Health", 47.20m, 0.0160, 0.00005)
            });
        }

        private void InitializeOrderBooks()
        {
            foreach (var stock in _stocks)
            {
                _matchingEngine.EnsureBook(stock.Ticker);
                _referencePrices[stock.Ticker] = stock.Price;
            }

            SeedLiquidityOrders();
        }

        private VirtualStockState CreateStock(string ticker, string name, decimal price, double volatility, double trend)
        {
            return new VirtualStockState
            {
                Ticker = ticker,
                Name = name,
                Price = price,
                PreviousPrice = price,
                FairPrice = Math.Round(price * (decimal)(0.88 + _random.NextDouble() * 0.28), 2),
                Volatility = volatility,
                Trend = trend,
                Volume = 0
            };
        }

        private void InitializeBots(int count)
        {
            ITradingStrategy[] strategies =
            {
                new RandomStrategy(),
                new RandomStrategy(),
                new MomentumStrategy(),
                new MomentumStrategy(),
                new ValueStrategy(),
                new PanicSellerStrategy(),
                new NewsStrategy(),
                new NewsStrategy(),
                new NewsStrategy()
            };

            for (int i = 1; i <= count; i++)
            {
                _bots.Add(new VirtualBot
                {
                    Name = $"Bot #{i}",
                    Cash = _random.Next(5_000, 100_000),
                    Strategy = strategies[_random.Next(strategies.Length)]
                });
            }
        }
    }
}

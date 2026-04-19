using System;
using System.Collections.Generic;
using System.Linq;
using StockExchangeSimulator.Enums;
using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.Services
{
    public class TradingService
    {
        private readonly decimal _commissionPercent;
        private readonly decimal _fixedCommission;
        private readonly decimal _minCommission;
        private readonly int _maxQuoteAgeSeconds;

        public TradingService()
        {
            _commissionPercent = EnvService.GetDecimal("REAL_MARKET_COMMISSION_PERCENT", 0.1m);
            _fixedCommission = EnvService.GetDecimal("REAL_MARKET_FIXED_COMMISSION", 0m);
            _minCommission = EnvService.GetDecimal("REAL_MARKET_MIN_COMMISSION", 0m);
            _maxQuoteAgeSeconds = EnvService.GetInt("REAL_MARKET_MAX_QUOTE_AGE_SECONDS", 120);
        }

        public List<Trade> Trades { get; private set; } = new();

        public void SetTrades(IEnumerable<Trade>? trades)
        {
            Trades = trades?.OrderBy(t => t.Timestamp).ToList() ?? new List<Trade>();
        }

        public void ClearTrades()
        {
            Trades.Clear();
        }

        public TradeExecutionResult ExecuteBuyAsset(Portfolio portfolio, Asset asset, int quantity, MarketMode marketMode)
        {
            if (portfolio == null)
                return Fail("Портфель не найден.");

            if (asset == null)
                return Fail("Актив не выбран.");

            if (quantity <= 0)
                return Fail("Количество должно быть больше 0.");

            if (asset.CurrentPrice <= 0)
                return Fail("Некорректная цена актива.");

            if (marketMode == MarketMode.Real && IsQuoteStale(asset))
                return Fail("Котировка устарела. Сначала обнови рынок.");

            decimal grossAmount = asset.CurrentPrice * quantity;
            decimal fee = CalculateFee(grossAmount);
            decimal netAmount = grossAmount + fee;

            if (portfolio.Balance < netAmount)
            {
                return Fail($"Недостаточно средств. Нужно {netAmount:F2}, доступно {portfolio.Balance:F2}.");
            }

            var position = portfolio.Positions.FirstOrDefault(p => p.Ticker == asset.Ticker);

            portfolio.Balance -= netAmount;

            if (position == null)
            {
                portfolio.Positions.Add(new Position
                {
                    Ticker = asset.Ticker,
                    Quantity = quantity,
                    AveragePrice = asset.CurrentPrice
                });
            }
            else
            {
                decimal oldCost = position.AveragePrice * position.Quantity;
                decimal newCost = asset.CurrentPrice * quantity;
                int newQuantity = position.Quantity + quantity;

                position.AveragePrice = (oldCost + newCost) / newQuantity;
                position.Quantity = newQuantity;
            }

            AddTrade(asset.Ticker, TradeType.Buy, quantity, asset.CurrentPrice, fee, 0m, marketMode);

            return new TradeExecutionResult
            {
                IsSuccess = true,
                GrossAmount = grossAmount,
                Fee = fee,
                NetAmount = netAmount,
                RealizedPnL = 0m,
                Message = $"Куплено {quantity} шт. {asset.Ticker} по {asset.CurrentPrice:F2}. Комиссия: {fee:F2}."
            };
        }

        public TradeExecutionResult ExecuteSellAsset(Portfolio portfolio, Asset asset, int quantity, MarketMode marketMode)
        {
            if (portfolio == null)
                return Fail("Портфель не найден.");

            if (asset == null)
                return Fail("Актив не выбран.");

            if (quantity <= 0)
                return Fail("Количество должно быть больше 0.");

            if (asset.CurrentPrice <= 0)
                return Fail("Некорректная цена актива.");

            if (marketMode == MarketMode.Real && IsQuoteStale(asset))
                return Fail("Котировка устарела. Сначала обнови рынок.");

            var position = portfolio.Positions.FirstOrDefault(p => p.Ticker == asset.Ticker);

            if (position == null || position.Quantity < quantity)
                return Fail("Недостаточно акций для продажи.");

            decimal grossAmount = asset.CurrentPrice * quantity;
            decimal fee = CalculateFee(grossAmount);
            decimal netAmount = grossAmount - fee;
            decimal realizedPnL = ((asset.CurrentPrice - position.AveragePrice) * quantity) - fee;

            portfolio.Balance += netAmount;
            position.Quantity -= quantity;

            if (position.Quantity == 0)
            {
                portfolio.Positions.Remove(position);
            }

            AddTrade(asset.Ticker, TradeType.Sell, quantity, asset.CurrentPrice, fee, realizedPnL, marketMode);

            return new TradeExecutionResult
            {
                IsSuccess = true,
                GrossAmount = grossAmount,
                Fee = fee,
                NetAmount = netAmount,
                RealizedPnL = realizedPnL,
                Message = $"Продано {quantity} шт. {asset.Ticker} по {asset.CurrentPrice:F2}. Комиссия: {fee:F2}. Realized PnL: {realizedPnL:F2}."
            };
        }

        public bool BuyAsset(Portfolio portfolio, Asset asset, int quantity, MarketMode marketMode, out string message)
        {
            var result = ExecuteBuyAsset(portfolio, asset, quantity, marketMode);
            message = result.Message;
            return result.IsSuccess;
        }

        public bool SellAsset(Portfolio portfolio, Asset asset, int quantity, MarketMode marketMode, out string message)
        {
            var result = ExecuteSellAsset(portfolio, asset, quantity, marketMode);
            message = result.Message;
            return result.IsSuccess;
        }

        private void AddTrade(
            string ticker,
            TradeType tradeType,
            int quantity,
            decimal price,
            decimal fee,
            decimal realizedPnL,
            MarketMode marketMode)
        {
            Trades.Add(new Trade
            {
                Timestamp = DateTime.Now,
                Ticker = ticker,
                TradeType = tradeType,
                Quantity = quantity,
                Price = price,
                Fee = fee,
                RealizedPnL = realizedPnL,
                MarketMode = marketMode
            });
        }

        private decimal CalculateFee(decimal amount)
        {
            decimal percentFee = amount * (_commissionPercent / 100m);
            decimal totalFee = percentFee + _fixedCommission;

            if (totalFee < _minCommission)
                totalFee = _minCommission;

            return Math.Round(totalFee, 2, MidpointRounding.AwayFromZero);
        }

        private bool IsQuoteStale(Asset asset)
        {
            if (asset.LastUpdatedUtc == default)
                return false;

            return DateTime.UtcNow - asset.LastUpdatedUtc > TimeSpan.FromSeconds(_maxQuoteAgeSeconds);
        }

        private static TradeExecutionResult Fail(string message)
        {
            return new TradeExecutionResult
            {
                IsSuccess = false,
                Message = message
            };
        }
    }
}
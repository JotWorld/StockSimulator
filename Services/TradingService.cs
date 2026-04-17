using System;
using System.Collections.Generic;
using System.Linq;
using StockExchangeSimulator.Enums;
using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.Services
{
    public class TradingService
    {
        public List<Trade> Trades { get; private set; } = new();

        public void SetTrades(IEnumerable<Trade>? trades)
        {
            Trades = trades?.OrderBy(t => t.Timestamp).ToList() ?? new List<Trade>();
        }

        public bool BuyAsset(Portfolio portfolio, Asset asset, int quantity, MarketMode marketMode, out string message)
        {
            if (portfolio == null)
            {
                message = "Портфель не найден.";
                return false;
            }

            if (asset == null)
            {
                message = "Актив не выбран.";
                return false;
            }

            if (quantity <= 0)
            {
                message = "Количество должно быть больше 0.";
                return false;
            }

            if (asset.CurrentPrice <= 0)
            {
                message = "Некорректная цена актива.";
                return false;
            }

            decimal totalCost = asset.CurrentPrice * quantity;

            if (portfolio.Balance < totalCost)
            {
                message = "Недостаточно средств.";
                return false;
            }

            var position = portfolio.Positions.FirstOrDefault(p => p.Ticker == asset.Ticker);

            portfolio.Balance -= totalCost;

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

            AddTrade(asset.Ticker, TradeType.Buy, quantity, asset.CurrentPrice, marketMode);

            message = $"Куплено {quantity} шт. {asset.Ticker} по {asset.CurrentPrice:F2}.";
            return true;
        }

        public bool SellAsset(Portfolio portfolio, Asset asset, int quantity, MarketMode marketMode, out string message)
        {
            if (portfolio == null)
            {
                message = "Портфель не найден.";
                return false;
            }

            if (asset == null)
            {
                message = "Актив не выбран.";
                return false;
            }

            if (quantity <= 0)
            {
                message = "Количество должно быть больше 0.";
                return false;
            }

            if (asset.CurrentPrice <= 0)
            {
                message = "Некорректная цена актива.";
                return false;
            }

            var position = portfolio.Positions.FirstOrDefault(p => p.Ticker == asset.Ticker);

            if (position == null || position.Quantity < quantity)
            {
                message = "Недостаточно акций для продажи.";
                return false;
            }

            decimal totalRevenue = asset.CurrentPrice * quantity;
            portfolio.Balance += totalRevenue;
            position.Quantity -= quantity;

            if (position.Quantity == 0)
                portfolio.Positions.Remove(position);

            AddTrade(asset.Ticker, TradeType.Sell, quantity, asset.CurrentPrice, marketMode);

            message = $"Продано {quantity} шт. {asset.Ticker} по {asset.CurrentPrice:F2}.";
            return true;
        }

        private void AddTrade(string ticker, TradeType tradeType, int quantity, decimal price, MarketMode marketMode)
        {
            Trades.Add(new Trade
            {
                Timestamp = DateTime.Now,
                Ticker = ticker,
                TradeType = tradeType,
                Quantity = quantity,
                Price = price,
                MarketMode = marketMode
            });
        }
    }
}
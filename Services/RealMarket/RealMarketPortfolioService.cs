using System;
using System.Collections.Generic;
using System.Linq;
using StockExchangeSimulator.Enums;
using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.Services
{
    public class RealMarketPortfolioService
    {
        public PortfolioSummary BuildSummary(Portfolio portfolio, IEnumerable<Asset> assets, IEnumerable<Trade> trades)
        {
            var assetMap = assets
                .GroupBy(a => a.Ticker)
                .ToDictionary(g => g.Key, g => g.First());

            decimal positionsValue = 0m;
            decimal totalCost = 0m;

            foreach (var position in portfolio.Positions)
            {
                totalCost += position.AveragePrice * position.Quantity;

                if (assetMap.TryGetValue(position.Ticker, out var asset))
                    positionsValue += asset.CurrentPrice * position.Quantity;
            }

            decimal realizedPnL = trades
                .Where(t => t.MarketMode == MarketMode.Real && t.TradeType == TradeType.Sell)
                .Sum(t => t.RealizedPnL);

            decimal totalFees = trades
                .Where(t => t.MarketMode == MarketMode.Real)
                .Sum(t => t.Fee);

            return new PortfolioSummary
            {
                Balance = portfolio.Balance,
                PositionsValue = positionsValue,
                TotalValue = portfolio.Balance + positionsValue,
                TotalCost = totalCost,
                UnrealizedPnL = positionsValue - totalCost,
                RealizedPnL = realizedPnL,
                TotalFees = totalFees
            };
        }

        public List<PositionDisplay> BuildPositionDisplays(Portfolio portfolio, IEnumerable<Asset> assets)
        {
            var assetMap = assets
                .GroupBy(a => a.Ticker)
                .ToDictionary(g => g.Key, g => g.First());

            decimal totalPortfolioValue = portfolio.Balance;

            foreach (var position in portfolio.Positions)
            {
                if (assetMap.TryGetValue(position.Ticker, out var asset))
                    totalPortfolioValue += asset.CurrentPrice * position.Quantity;
            }

            return portfolio.Positions
                .Select(position =>
                {
                    assetMap.TryGetValue(position.Ticker, out var asset);

                    decimal currentPrice = asset?.CurrentPrice ?? 0m;
                    decimal marketValue = currentPrice * position.Quantity;
                    decimal costBasis = position.AveragePrice * position.Quantity;
                    decimal pnl = marketValue - costBasis;
                    decimal pnlPercent = costBasis == 0m ? 0m : pnl / costBasis * 100m;
                    decimal portfolioSharePercent = totalPortfolioValue == 0m ? 0m : marketValue / totalPortfolioValue * 100m;

                    return new PositionDisplay
                    {
                        Ticker = position.Ticker,
                        Quantity = position.Quantity,
                        AveragePrice = position.AveragePrice,
                        CurrentPrice = currentPrice,
                        MarketValue = marketValue,
                        CostBasis = costBasis,
                        PortfolioSharePercent = portfolioSharePercent,
                        PnL = pnl,
                        PnLPercent = pnlPercent
                    };
                })
                .OrderByDescending(p => p.MarketValue)
                .ToList();
        }

        public TradeStats BuildTradeStats(IEnumerable<Trade> trades, IEnumerable<Position> positions)
        {
            var realTrades = trades
                .Where(t => t.MarketMode == MarketMode.Real)
                .OrderByDescending(t => t.Timestamp)
                .ToList();

            var sellTrades = realTrades
                .Where(t => t.TradeType == TradeType.Sell)
                .ToList();

            int winningSellTrades = sellTrades.Count(t => t.RealizedPnL > 0m);
            int losingSellTrades = sellTrades.Count(t => t.RealizedPnL < 0m);

            decimal bestSellPnL = sellTrades.Any() ? sellTrades.Max(t => t.RealizedPnL) : 0m;
            decimal worstSellPnL = sellTrades.Any() ? sellTrades.Min(t => t.RealizedPnL) : 0m;

            decimal averageWin = winningSellTrades == 0 ? 0m : sellTrades.Where(t => t.RealizedPnL > 0m).Average(t => t.RealizedPnL);
            decimal averageLoss = losingSellTrades == 0 ? 0m : sellTrades.Where(t => t.RealizedPnL < 0m).Average(t => t.RealizedPnL);

            decimal grossProfit = sellTrades.Where(t => t.RealizedPnL > 0m).Sum(t => t.RealizedPnL);
            decimal grossLossAbs = Math.Abs(sellTrades.Where(t => t.RealizedPnL < 0m).Sum(t => t.RealizedPnL));
            decimal profitFactor = grossLossAbs == 0m ? (grossProfit > 0m ? grossProfit : 0m) : grossProfit / grossLossAbs;

            decimal averageFee = realTrades.Count == 0 ? 0m : realTrades.Average(t => t.Fee);

            var tickerAnalytics = BuildTickerAnalytics(realTrades, positions);

            var bestTicker = tickerAnalytics
                .OrderByDescending(t => t.RealizedPnL)
                .FirstOrDefault();

            var worstTicker = tickerAnalytics
                .OrderBy(t => t.RealizedPnL)
                .FirstOrDefault();

            return new TradeStats
            {
                TotalTrades = realTrades.Count,
                BuyTrades = realTrades.Count(t => t.TradeType == TradeType.Buy),
                SellTrades = sellTrades.Count,
                TotalBuyVolume = realTrades.Where(t => t.TradeType == TradeType.Buy).Sum(t => t.TotalAmount),
                TotalSellVolume = realTrades.Where(t => t.TradeType == TradeType.Sell).Sum(t => t.TotalAmount),
                TotalFees = realTrades.Sum(t => t.Fee),
                TotalRealizedPnL = sellTrades.Sum(t => t.RealizedPnL),
                WinningSellTrades = winningSellTrades,
                LosingSellTrades = losingSellTrades,
                WinRatePercent = sellTrades.Count == 0 ? 0m : (decimal)winningSellTrades / sellTrades.Count * 100m,
                BestSellPnL = bestSellPnL,
                WorstSellPnL = worstSellPnL,
                HasSellTrades = sellTrades.Any(),
                AverageWin = averageWin,
                AverageLoss = averageLoss,
                ProfitFactor = profitFactor,
                AverageFee = averageFee,
                BestTicker = bestTicker?.Ticker ?? "-",
                WorstTicker = worstTicker?.Ticker ?? "-",
                BestTickerPnL = bestTicker?.RealizedPnL ?? 0m,
                WorstTickerPnL = worstTicker?.RealizedPnL ?? 0m
            };
        }

        public List<TickerAnalytics> BuildTickerAnalytics(IEnumerable<Trade> trades, IEnumerable<Position> positions)
        {
            var realTrades = trades
                .Where(t => t.MarketMode == MarketMode.Real)
                .ToList();

            var openQuantityMap = positions
                .GroupBy(p => p.Ticker)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var tickerAnalytics = realTrades
                .GroupBy(t => t.Ticker)
                .Select(g => new TickerAnalytics
                {
                    Ticker = g.Key,
                    BuyTrades = g.Count(t => t.TradeType == TradeType.Buy),
                    SellTrades = g.Count(t => t.TradeType == TradeType.Sell),
                    BoughtQuantity = g.Where(t => t.TradeType == TradeType.Buy).Sum(t => t.Quantity),
                    SoldQuantity = g.Where(t => t.TradeType == TradeType.Sell).Sum(t => t.Quantity),
                    BuyVolume = g.Where(t => t.TradeType == TradeType.Buy).Sum(t => t.TotalAmount),
                    SellVolume = g.Where(t => t.TradeType == TradeType.Sell).Sum(t => t.TotalAmount),
                    Fees = g.Sum(t => t.Fee),
                    RealizedPnL = g.Where(t => t.TradeType == TradeType.Sell).Sum(t => t.RealizedPnL),
                    OpenQuantity = openQuantityMap.TryGetValue(g.Key, out int qty) ? qty : 0
                })
                .OrderByDescending(t => t.RealizedPnL)
                .ThenBy(t => t.Ticker)
                .ToList();

            foreach (var position in positions)
            {
                if (!tickerAnalytics.Any(t => t.Ticker == position.Ticker))
                {
                    tickerAnalytics.Add(new TickerAnalytics
                    {
                        Ticker = position.Ticker,
                        OpenQuantity = position.Quantity
                    });
                }
            }

            return tickerAnalytics
                .OrderByDescending(t => t.RealizedPnL)
                .ThenBy(t => t.Ticker)
                .ToList();
        }

        public PortfolioSnapshot BuildSnapshot(
            Portfolio portfolio,
            IEnumerable<Asset> assets,
            IEnumerable<Trade> trades,
            string source)
        {
            var summary = BuildSummary(portfolio, assets, trades);

            return new PortfolioSnapshot
            {
                Timestamp = DateTime.Now,
                Source = source,
                Balance = summary.Balance,
                PositionsValue = summary.PositionsValue,
                TotalValue = summary.TotalValue,
                UnrealizedPnL = summary.UnrealizedPnL,
                RealizedPnL = summary.RealizedPnL,
                TotalPnL = summary.TotalPnL,
                TotalFees = summary.TotalFees
            };
        }
    }
}
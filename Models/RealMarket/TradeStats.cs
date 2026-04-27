namespace StockExchangeSimulator.Models
{
    public class TradeStats
    {
        public int TotalTrades { get; set; }
        public int BuyTrades { get; set; }
        public int SellTrades { get; set; }

        public decimal TotalBuyVolume { get; set; }
        public decimal TotalSellVolume { get; set; }
        public decimal TotalFees { get; set; }
        public decimal TotalRealizedPnL { get; set; }

        public int WinningSellTrades { get; set; }
        public int LosingSellTrades { get; set; }
        public decimal WinRatePercent { get; set; }

        public decimal BestSellPnL { get; set; }
        public decimal WorstSellPnL { get; set; }
        public bool HasSellTrades { get; set; }

        public decimal AverageWin { get; set; }
        public decimal AverageLoss { get; set; }
        public decimal ProfitFactor { get; set; }
        public decimal AverageFee { get; set; }

        public string BestTicker { get; set; } = "-";
        public string WorstTicker { get; set; } = "-";
        public decimal BestTickerPnL { get; set; }
        public decimal WorstTickerPnL { get; set; }
    }
}
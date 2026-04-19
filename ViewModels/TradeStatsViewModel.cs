using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.ViewModels
{
    public class TradeStatsViewModel : ViewModelBase
    {
        private int _totalTrades;
        public int TotalTrades
        {
            get => _totalTrades;
            set => SetProperty(ref _totalTrades, value);
        }

        private int _buyTrades;
        public int BuyTrades
        {
            get => _buyTrades;
            set => SetProperty(ref _buyTrades, value);
        }

        private int _sellTrades;
        public int SellTrades
        {
            get => _sellTrades;
            set => SetProperty(ref _sellTrades, value);
        }

        private decimal _totalBuyVolume;
        public decimal TotalBuyVolume
        {
            get => _totalBuyVolume;
            set => SetProperty(ref _totalBuyVolume, value);
        }

        private decimal _totalSellVolume;
        public decimal TotalSellVolume
        {
            get => _totalSellVolume;
            set => SetProperty(ref _totalSellVolume, value);
        }

        private decimal _totalFees;
        public decimal TotalFees
        {
            get => _totalFees;
            set => SetProperty(ref _totalFees, value);
        }

        private decimal _totalRealizedPnL;
        public decimal TotalRealizedPnL
        {
            get => _totalRealizedPnL;
            set
            {
                if (SetProperty(ref _totalRealizedPnL, value))
                {
                    OnPropertyChanged(nameof(IsProfit));
                    OnPropertyChanged(nameof(IsLoss));
                }
            }
        }

        private int _winningSellTrades;
        public int WinningSellTrades
        {
            get => _winningSellTrades;
            set => SetProperty(ref _winningSellTrades, value);
        }

        private int _losingSellTrades;
        public int LosingSellTrades
        {
            get => _losingSellTrades;
            set => SetProperty(ref _losingSellTrades, value);
        }

        private decimal _winRatePercent;
        public decimal WinRatePercent
        {
            get => _winRatePercent;
            set => SetProperty(ref _winRatePercent, value);
        }

        private decimal _bestSellPnL;
        public decimal BestSellPnL
        {
            get => _bestSellPnL;
            set => SetProperty(ref _bestSellPnL, value);
        }

        private decimal _worstSellPnL;
        public decimal WorstSellPnL
        {
            get => _worstSellPnL;
            set => SetProperty(ref _worstSellPnL, value);
        }

        private bool _hasSellTrades;
        public bool HasSellTrades
        {
            get => _hasSellTrades;
            set => SetProperty(ref _hasSellTrades, value);
        }

        private decimal _averageWin;
        public decimal AverageWin
        {
            get => _averageWin;
            set => SetProperty(ref _averageWin, value);
        }

        private decimal _averageLoss;
        public decimal AverageLoss
        {
            get => _averageLoss;
            set => SetProperty(ref _averageLoss, value);
        }

        private decimal _profitFactor;
        public decimal ProfitFactor
        {
            get => _profitFactor;
            set => SetProperty(ref _profitFactor, value);
        }

        private decimal _averageFee;
        public decimal AverageFee
        {
            get => _averageFee;
            set => SetProperty(ref _averageFee, value);
        }

        private string _bestTicker = "-";
        public string BestTicker
        {
            get => _bestTicker;
            set => SetProperty(ref _bestTicker, value);
        }

        private string _worstTicker = "-";
        public string WorstTicker
        {
            get => _worstTicker;
            set => SetProperty(ref _worstTicker, value);
        }

        private decimal _bestTickerPnL;
        public decimal BestTickerPnL
        {
            get => _bestTickerPnL;
            set => SetProperty(ref _bestTickerPnL, value);
        }

        private decimal _worstTickerPnL;
        public decimal WorstTickerPnL
        {
            get => _worstTickerPnL;
            set => SetProperty(ref _worstTickerPnL, value);
        }

        public bool IsProfit => TotalRealizedPnL > 0;
        public bool IsLoss => TotalRealizedPnL < 0;

        public void UpdateFromModel(TradeStats model)
        {
            TotalTrades = model.TotalTrades;
            BuyTrades = model.BuyTrades;
            SellTrades = model.SellTrades;
            TotalBuyVolume = model.TotalBuyVolume;
            TotalSellVolume = model.TotalSellVolume;
            TotalFees = model.TotalFees;
            TotalRealizedPnL = model.TotalRealizedPnL;
            WinningSellTrades = model.WinningSellTrades;
            LosingSellTrades = model.LosingSellTrades;
            WinRatePercent = model.WinRatePercent;
            BestSellPnL = model.BestSellPnL;
            WorstSellPnL = model.WorstSellPnL;
            HasSellTrades = model.HasSellTrades;
            AverageWin = model.AverageWin;
            AverageLoss = model.AverageLoss;
            ProfitFactor = model.ProfitFactor;
            AverageFee = model.AverageFee;
            BestTicker = model.BestTicker;
            WorstTicker = model.WorstTicker;
            BestTickerPnL = model.BestTickerPnL;
            WorstTickerPnL = model.WorstTickerPnL;
        }
    }
}
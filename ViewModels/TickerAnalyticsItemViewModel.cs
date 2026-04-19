using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.ViewModels
{
    public class TickerAnalyticsItemViewModel : ViewModelBase
    {
        private string _ticker = string.Empty;
        public string Ticker
        {
            get => _ticker;
            set => SetProperty(ref _ticker, value);
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

        private int _boughtQuantity;
        public int BoughtQuantity
        {
            get => _boughtQuantity;
            set => SetProperty(ref _boughtQuantity, value);
        }

        private int _soldQuantity;
        public int SoldQuantity
        {
            get => _soldQuantity;
            set => SetProperty(ref _soldQuantity, value);
        }

        private int _openQuantity;
        public int OpenQuantity
        {
            get => _openQuantity;
            set => SetProperty(ref _openQuantity, value);
        }

        private decimal _buyVolume;
        public decimal BuyVolume
        {
            get => _buyVolume;
            set => SetProperty(ref _buyVolume, value);
        }

        private decimal _sellVolume;
        public decimal SellVolume
        {
            get => _sellVolume;
            set => SetProperty(ref _sellVolume, value);
        }

        private decimal _fees;
        public decimal Fees
        {
            get => _fees;
            set => SetProperty(ref _fees, value);
        }

        private decimal _realizedPnL;
        public decimal RealizedPnL
        {
            get => _realizedPnL;
            set
            {
                if (SetProperty(ref _realizedPnL, value))
                {
                    OnPropertyChanged(nameof(IsProfit));
                    OnPropertyChanged(nameof(IsLoss));
                }
            }
        }

        public bool IsProfit => RealizedPnL > 0;
        public bool IsLoss => RealizedPnL < 0;

        public static TickerAnalyticsItemViewModel FromModel(TickerAnalytics model)
        {
            return new TickerAnalyticsItemViewModel
            {
                Ticker = model.Ticker,
                BuyTrades = model.BuyTrades,
                SellTrades = model.SellTrades,
                BoughtQuantity = model.BoughtQuantity,
                SoldQuantity = model.SoldQuantity,
                OpenQuantity = model.OpenQuantity,
                BuyVolume = model.BuyVolume,
                SellVolume = model.SellVolume,
                Fees = model.Fees,
                RealizedPnL = model.RealizedPnL
            };
        }
    }
}
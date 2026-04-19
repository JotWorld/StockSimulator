using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.ViewModels
{
    public class AssetItemViewModel : ViewModelBase
    {
        private string _ticker = string.Empty;
        public string Ticker
        {
            get => _ticker;
            set => SetProperty(ref _ticker, value);
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private decimal _currentPrice;
        public decimal CurrentPrice
        {
            get => _currentPrice;
            set => SetProperty(ref _currentPrice, value);
        }

        private decimal _change;
        public decimal Change
        {
            get => _change;
            set
            {
                if (SetProperty(ref _change, value))
                {
                    OnPropertyChanged(nameof(IsPositiveChange));
                    OnPropertyChanged(nameof(IsNegativeChange));
                }
            }
        }

        private decimal _changePercent;
        public decimal ChangePercent
        {
            get => _changePercent;
            set
            {
                if (SetProperty(ref _changePercent, value))
                {
                    OnPropertyChanged(nameof(IsPositiveChangePercent));
                    OnPropertyChanged(nameof(IsNegativeChangePercent));
                }
            }
        }

        private bool _isVirtual;
        public bool IsVirtual
        {
            get => _isVirtual;
            set => SetProperty(ref _isVirtual, value);
        }

        private System.DateTime _lastUpdatedUtc;
        public System.DateTime LastUpdatedUtc
        {
            get => _lastUpdatedUtc;
            set => SetProperty(ref _lastUpdatedUtc, value);
        }

        public bool IsPositiveChange => Change > 0;
        public bool IsNegativeChange => Change < 0;
        public bool IsPositiveChangePercent => ChangePercent > 0;
        public bool IsNegativeChangePercent => ChangePercent < 0;

        public static AssetItemViewModel FromModel(Asset model)
        {
            return new AssetItemViewModel
            {
                Ticker = model.Ticker,
                Name = model.Name,
                CurrentPrice = model.CurrentPrice,
                Change = model.Change,
                ChangePercent = model.ChangePercent,
                IsVirtual = model.IsVirtual,
                LastUpdatedUtc = model.LastUpdatedUtc
            };
        }

        public Asset ToModel()
        {
            return new Asset
            {
                Ticker = Ticker,
                Name = Name,
                CurrentPrice = CurrentPrice,
                Change = Change,
                ChangePercent = ChangePercent,
                IsVirtual = IsVirtual,
                LastUpdatedUtc = LastUpdatedUtc
            };
        }
    }
}
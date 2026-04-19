using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.ViewModels
{
    public class PositionDisplayItemViewModel : ViewModelBase
    {
        private string _ticker = string.Empty;
        public string Ticker
        {
            get => _ticker;
            set => SetProperty(ref _ticker, value);
        }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        private decimal _averagePrice;
        public decimal AveragePrice
        {
            get => _averagePrice;
            set => SetProperty(ref _averagePrice, value);
        }

        private decimal _currentPrice;
        public decimal CurrentPrice
        {
            get => _currentPrice;
            set => SetProperty(ref _currentPrice, value);
        }

        private decimal _marketValue;
        public decimal MarketValue
        {
            get => _marketValue;
            set => SetProperty(ref _marketValue, value);
        }

        private decimal _costBasis;
        public decimal CostBasis
        {
            get => _costBasis;
            set => SetProperty(ref _costBasis, value);
        }

        private decimal _portfolioSharePercent;
        public decimal PortfolioSharePercent
        {
            get => _portfolioSharePercent;
            set => SetProperty(ref _portfolioSharePercent, value);
        }

        private decimal _pnL;
        public decimal PnL
        {
            get => _pnL;
            set
            {
                if (SetProperty(ref _pnL, value))
                {
                    OnPropertyChanged(nameof(IsProfit));
                    OnPropertyChanged(nameof(IsLoss));
                }
            }
        }

        private decimal _pnLPercent;
        public decimal PnLPercent
        {
            get => _pnLPercent;
            set => SetProperty(ref _pnLPercent, value);
        }

        public bool IsProfit => PnL > 0;
        public bool IsLoss => PnL < 0;

        public static PositionDisplayItemViewModel FromModel(PositionDisplay model)
        {
            return new PositionDisplayItemViewModel
            {
                Ticker = model.Ticker,
                Quantity = model.Quantity,
                AveragePrice = model.AveragePrice,
                CurrentPrice = model.CurrentPrice,
                MarketValue = model.MarketValue,
                CostBasis = model.CostBasis,
                PortfolioSharePercent = model.PortfolioSharePercent,
                PnL = model.PnL,
                PnLPercent = model.PnLPercent
            };
        }
    }
}
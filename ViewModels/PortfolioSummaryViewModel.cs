using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.ViewModels
{
    public class PortfolioSummaryViewModel : ViewModelBase
    {
        private decimal _balance;
        public decimal Balance
        {
            get => _balance;
            set => SetProperty(ref _balance, value);
        }

        private decimal _positionsValue;
        public decimal PositionsValue
        {
            get => _positionsValue;
            set => SetProperty(ref _positionsValue, value);
        }

        private decimal _totalValue;
        public decimal TotalValue
        {
            get => _totalValue;
            set => SetProperty(ref _totalValue, value);
        }

        private decimal _totalCost;
        public decimal TotalCost
        {
            get => _totalCost;
            set => SetProperty(ref _totalCost, value);
        }

        private decimal _unrealizedPnL;
        public decimal UnrealizedPnL
        {
            get => _unrealizedPnL;
            set
            {
                if (SetProperty(ref _unrealizedPnL, value))
                {
                    OnPropertyChanged(nameof(IsUnrealizedProfit));
                    OnPropertyChanged(nameof(IsUnrealizedLoss));
                }
            }
        }

        private decimal _realizedPnL;
        public decimal RealizedPnL
        {
            get => _realizedPnL;
            set
            {
                if (SetProperty(ref _realizedPnL, value))
                {
                    OnPropertyChanged(nameof(IsRealizedProfit));
                    OnPropertyChanged(nameof(IsRealizedLoss));
                }
            }
        }

        private decimal _totalFees;
        public decimal TotalFees
        {
            get => _totalFees;
            set => SetProperty(ref _totalFees, value);
        }

        public decimal TotalPnL => UnrealizedPnL + RealizedPnL;

        public bool IsUnrealizedProfit => UnrealizedPnL > 0;
        public bool IsUnrealizedLoss => UnrealizedPnL < 0;

        public bool IsRealizedProfit => RealizedPnL > 0;
        public bool IsRealizedLoss => RealizedPnL < 0;

        public bool IsTotalProfit => TotalPnL > 0;
        public bool IsTotalLoss => TotalPnL < 0;

        public void UpdateFromModel(PortfolioSummary model)
        {
            Balance = model.Balance;
            PositionsValue = model.PositionsValue;
            TotalValue = model.TotalValue;
            TotalCost = model.TotalCost;
            UnrealizedPnL = model.UnrealizedPnL;
            RealizedPnL = model.RealizedPnL;
            TotalFees = model.TotalFees;

            OnPropertyChanged(nameof(TotalPnL));
            OnPropertyChanged(nameof(IsTotalProfit));
            OnPropertyChanged(nameof(IsTotalLoss));
        }
    }
}
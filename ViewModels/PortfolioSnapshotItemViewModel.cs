using System;
using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.ViewModels
{
    public class PortfolioSnapshotItemViewModel : ViewModelBase
    {
        private DateTime _timestamp;
        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }

        private string _source = string.Empty;
        public string Source
        {
            get => _source;
            set => SetProperty(ref _source, value);
        }

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

        private decimal _unrealizedPnL;
        public decimal UnrealizedPnL
        {
            get => _unrealizedPnL;
            set => SetProperty(ref _unrealizedPnL, value);
        }

        private decimal _realizedPnL;
        public decimal RealizedPnL
        {
            get => _realizedPnL;
            set => SetProperty(ref _realizedPnL, value);
        }

        private decimal _totalPnL;
        public decimal TotalPnL
        {
            get => _totalPnL;
            set
            {
                if (SetProperty(ref _totalPnL, value))
                {
                    OnPropertyChanged(nameof(IsProfit));
                    OnPropertyChanged(nameof(IsLoss));
                }
            }
        }

        private decimal _totalFees;
        public decimal TotalFees
        {
            get => _totalFees;
            set => SetProperty(ref _totalFees, value);
        }

        public bool IsProfit => TotalPnL > 0;
        public bool IsLoss => TotalPnL < 0;

        public static PortfolioSnapshotItemViewModel FromModel(PortfolioSnapshot model)
        {
            return new PortfolioSnapshotItemViewModel
            {
                Timestamp = model.Timestamp,
                Source = model.Source,
                Balance = model.Balance,
                PositionsValue = model.PositionsValue,
                TotalValue = model.TotalValue,
                UnrealizedPnL = model.UnrealizedPnL,
                RealizedPnL = model.RealizedPnL,
                TotalPnL = model.TotalPnL,
                TotalFees = model.TotalFees
            };
        }
    }
}
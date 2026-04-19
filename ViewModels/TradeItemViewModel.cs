using System;
using StockExchangeSimulator.Enums;
using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.ViewModels
{
    public class TradeItemViewModel : ViewModelBase
    {
        private DateTime _timestamp;
        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }

        private string _ticker = string.Empty;
        public string Ticker
        {
            get => _ticker;
            set => SetProperty(ref _ticker, value);
        }

        private TradeType _tradeType;
        public TradeType TradeType
        {
            get => _tradeType;
            set
            {
                if (SetProperty(ref _tradeType, value))
                {
                    OnPropertyChanged(nameof(TradeTypeText));
                    OnPropertyChanged(nameof(IsBuy));
                    OnPropertyChanged(nameof(IsSell));
                }
            }
        }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        private decimal _price;
        public decimal Price
        {
            get => _price;
            set => SetProperty(ref _price, value);
        }

        private decimal _fee;
        public decimal Fee
        {
            get => _fee;
            set => SetProperty(ref _fee, value);
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

        private MarketMode _marketMode;
        public MarketMode MarketMode
        {
            get => _marketMode;
            set => SetProperty(ref _marketMode, value);
        }

        public decimal TotalAmount => Quantity * Price;

        public decimal NetAmount =>
            TradeType == TradeType.Buy
                ? TotalAmount + Fee
                : TotalAmount - Fee;

        public string TradeTypeText => TradeType == TradeType.Buy ? "BUY" : "SELL";
        public bool IsBuy => TradeType == TradeType.Buy;
        public bool IsSell => TradeType == TradeType.Sell;
        public bool IsProfit => RealizedPnL > 0;
        public bool IsLoss => RealizedPnL < 0;

        public static TradeItemViewModel FromModel(Trade model)
        {
            return new TradeItemViewModel
            {
                Timestamp = model.Timestamp,
                Ticker = model.Ticker,
                TradeType = model.TradeType,
                Quantity = model.Quantity,
                Price = model.Price,
                Fee = model.Fee,
                RealizedPnL = model.RealizedPnL,
                MarketMode = model.MarketMode
            };
        }

        public Trade ToModel()
        {
            return new Trade
            {
                Timestamp = Timestamp,
                Ticker = Ticker,
                TradeType = TradeType,
                Quantity = Quantity,
                Price = Price,
                Fee = Fee,
                RealizedPnL = RealizedPnL,
                MarketMode = MarketMode
            };
        }
    }
}
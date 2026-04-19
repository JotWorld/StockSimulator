using System;
using System.Globalization;
using System.Windows.Data;
using StockExchangeSimulator.Enums;

namespace StockExchangeSimulator.Converters
{
    public class TradeTypeToTextConverter : IValueConverter
    {
        public string BuyText { get; set; } = "BUY";
        public string SellText { get; set; } = "SELL";
        public string UnknownText { get; set; } = "-";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TradeType tradeType)
            {
                return tradeType == TradeType.Buy ? BuyText : SellText;
            }

            return UnknownText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
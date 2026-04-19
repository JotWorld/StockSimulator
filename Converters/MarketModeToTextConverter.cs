using System;
using System.Globalization;
using System.Windows.Data;
using StockExchangeSimulator.Enums;

namespace StockExchangeSimulator.Converters
{
    public class MarketModeToTextConverter : IValueConverter
    {
        public string RealText { get; set; } = "Real";
        public string VirtualText { get; set; } = "Virtual";
        public string UnknownText { get; set; } = "-";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MarketMode marketMode)
            {
                return marketMode == MarketMode.Real ? RealText : VirtualText;
            }

            return UnknownText;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
using System;
using System.Globalization;
using System.Windows.Data;

namespace StockExchangeSimulator.Converters
{
    public class PositiveDecimalToBoolConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return false;

            if (decimal.TryParse(value.ToString(), out var number))
            {
                var result = number > 0;
                return Invert ? !result : result;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
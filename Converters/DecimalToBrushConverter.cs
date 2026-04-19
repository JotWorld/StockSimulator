using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StockExchangeSimulator.Converters
{
    public class DecimalToSignedBrushConverter : IValueConverter
    {
        public Brush PositiveBrush { get; set; } = Brushes.LimeGreen;
        public Brush NegativeBrush { get; set; } = Brushes.Red;
        public Brush NeutralBrush { get; set; } = Brushes.Gray;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return NeutralBrush;

            if (decimal.TryParse(value.ToString(), out var number))
            {
                if (number > 0) return PositiveBrush;
                if (number < 0) return NegativeBrush;
            }

            return NeutralBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
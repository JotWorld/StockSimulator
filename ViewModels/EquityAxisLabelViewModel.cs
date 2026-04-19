namespace StockExchangeSimulator.ViewModels
{
    public class EquityAxisLabelViewModel : ViewModelBase
    {
        private double _x;
        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        private double _y;
        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        private string _text = string.Empty;
        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }
    }
}
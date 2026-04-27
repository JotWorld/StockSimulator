using StockExchangeSimulator.Models.VirtualMarket;

namespace StockExchangeSimulator.ViewModels
{
    public class VirtualStockItemViewModel : ViewModelBase
    {
        private readonly VirtualStockState _stock;

        public VirtualStockItemViewModel(VirtualStockState stock)
        {
            _stock = stock;
        }

        public string Ticker => _stock.Ticker;
        public string Name => _stock.Name;
        public decimal Price => _stock.Price;
        public decimal Change => _stock.Change;
        public decimal ChangePercent => _stock.ChangePercent;
        public long Volume => _stock.Volume;

        public void Refresh()
        {
            OnPropertyChanged(nameof(Price));
            OnPropertyChanged(nameof(Change));
            OnPropertyChanged(nameof(ChangePercent));
            OnPropertyChanged(nameof(Volume));
        }
    }
}
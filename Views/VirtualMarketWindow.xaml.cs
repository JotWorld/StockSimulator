using System.Windows;
using StockExchangeSimulator.ViewModels;

namespace StockExchangeSimulator.Views
{
    public partial class VirtualMarketWindow : Window
    {
        public VirtualMarketWindow()
        {
            InitializeComponent();
            DataContext = new VirtualMarketViewModel();
        }
    }
}

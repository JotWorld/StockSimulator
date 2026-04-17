using System.Windows;
using StockExchangeSimulator.Views;

namespace StockExchangeSimulator
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OpenRealMarket_Click(object sender, RoutedEventArgs e)
        {
            var window = new RealMarketWindow();
            window.Show();
        }

        private void OpenVirtualMarket_Click(object sender, RoutedEventArgs e)
        {
            var window = new VirtualMarketWindow();
            window.Show();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
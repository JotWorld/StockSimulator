using System.Windows;
using StockExchangeSimulator.Data;
using StockExchangeSimulator.Services;
using StockExchangeSimulator.ViewModels;

namespace StockExchangeSimulator.Views
{
    public partial class RealMarketWindow : Window
    {
        public RealMarketWindow()
        {
            InitializeComponent();

            var dialogService = new DialogService();
            var dataService = new RealMarketDataService();
            var tradingService = new TradingService();
            var portfolioService = new RealMarketPortfolioService();
            var stateService = new RealMarketStateService();
            var repository = new SqliteRealMarketRepository(new RealMarketDbService());

            DataContext = new RealMarketViewModel(
                dataService,
                tradingService,
                portfolioService,
                stateService,
                repository,
                dialogService);
        }
    }
}
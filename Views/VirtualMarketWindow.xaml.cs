using System.Collections.Generic;
using System.Windows;
using StockExchangeSimulator.Enums;
using StockExchangeSimulator.Models;
using StockExchangeSimulator.Services;

namespace StockExchangeSimulator.Views
{
    public partial class VirtualMarketWindow : Window
    {
        private readonly VirtualMarketService _marketService = new();
        private readonly TradingService _tradingService = new();
        private readonly Portfolio _portfolio = new() { Balance = 10000m };
        private List<Asset> _assets = new();

        public VirtualMarketWindow()
        {
            InitializeComponent();
            LoadAssets();
            UpdateBalance();
        }

        private void LoadAssets()
        {
            _assets = _marketService.GetAssets();
            AssetsGrid.ItemsSource = _assets;
        }

        private void UpdateBalance()
        {
            BalanceText.Text = $"Баланс: {_portfolio.Balance}";
        }

        private Asset? GetSelectedAsset()
        {
            return AssetsGrid.SelectedItem as Asset;
        }

        private bool TryGetQuantity(out int quantity)
        {
            return int.TryParse(QuantityTextBox.Text, out quantity) && quantity > 0;
        }

        private void Buy_Click(object sender, RoutedEventArgs e)
        {
            var asset = GetSelectedAsset();

            if (asset == null)
            {
                MessageBox.Show("Выбери акцию.");
                return;
            }

            if (!TryGetQuantity(out int quantity))
            {
                MessageBox.Show("Введи корректное количество.");
                return;
            }

            _tradingService.BuyAsset(_portfolio, asset, quantity, MarketMode.Virtual, out string message);
            UpdateBalance();
            MessageBox.Show(message);
        }

        private void Sell_Click(object sender, RoutedEventArgs e)
        {
            var asset = GetSelectedAsset();

            if (asset == null)
            {
                MessageBox.Show("Выбери акцию.");
                return;
            }

            if (!TryGetQuantity(out int quantity))
            {
                MessageBox.Show("Введи корректное количество.");
                return;
            }

            _tradingService.SellAsset(_portfolio, asset, quantity, MarketMode.Virtual, out string message);
            UpdateBalance();
            MessageBox.Show(message);
        }
    }
}
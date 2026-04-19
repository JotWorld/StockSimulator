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
            AssetsGrid.ItemsSource = null;
            AssetsGrid.ItemsSource = _assets;
        }

        private void UpdateBalance()
        {
            BalanceText.Text = $"Баланс: {_portfolio.Balance:F2}";
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

            var result = _tradingService.ExecuteBuyAsset(_portfolio, asset, quantity, MarketMode.Virtual);

            UpdateBalance();

            if (result.IsSuccess)
            {
                QuantityTextBox.Clear();
            }

            MessageBox.Show(result.Message);
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

            var result = _tradingService.ExecuteSellAsset(_portfolio, asset, quantity, MarketMode.Virtual);

            UpdateBalance();

            if (result.IsSuccess)
            {
                QuantityTextBox.Clear();
            }

            MessageBox.Show(result.Message);
        }
    }
}
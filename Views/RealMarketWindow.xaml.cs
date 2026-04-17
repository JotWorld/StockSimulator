using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StockExchangeSimulator.Enums;
using StockExchangeSimulator.Models;
using StockExchangeSimulator.Services;

namespace StockExchangeSimulator.Views
{
    public partial class RealMarketWindow : Window
    {
        private readonly RealMarketDataService _marketDataService = new();
        private readonly TradingService _tradingService = new();
        private readonly RealMarketStateService _stateService = new();
        private readonly Portfolio _portfolio = new();
        private readonly List<string> _trackedTickers = new();

        private List<Asset> _assets = new();
        private DispatcherTimer? _timer;
        private bool _isRefreshing;
        private bool _isInitialized;

        public RealMarketWindow()
        {
            InitializeComponent();
            Loaded += RealMarketWindow_Loaded;
            Closed += RealMarketWindow_Closed;
        }

        private async void RealMarketWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadState();
            ApplySettingsToUi();
            ConfigureTimer();

            await RefreshAllAsync();

            if (_portfolio.Balance <= 0 && !_portfolio.Positions.Any() && !_tradingService.Trades.Any())
            {
                _portfolio.Balance = 10000m;
            }

            RefreshSummary();
            UpdateTradeButtons();
            _isInitialized = true;
        }

        private void RealMarketWindow_Closed(object? sender, EventArgs e)
        {
            _timer?.Stop();
            SaveState();
        }

        private void LoadState()
        {
            var state = _stateService.Load();

            _portfolio.Balance = state.Balance;
            _portfolio.Positions = state.Positions ?? new List<Position>();

            _trackedTickers.Clear();
            _trackedTickers.AddRange(state.TrackedTickers ?? new List<string>());

            _tradingService.SetTrades(state.Trades);
            CurrentSettings = state.Settings ?? new RealMarketSettings();
        }

        private void SaveState()
        {
            ReadSettingsFromUi();

            var state = new RealMarketState
            {
                Balance = _portfolio.Balance,
                Positions = _portfolio.Positions,
                Trades = _tradingService.Trades,
                TrackedTickers = _trackedTickers,
                Settings = CurrentSettings
            };

            _stateService.Save(state);
        }

        private RealMarketSettings CurrentSettings { get; set; } = new();

        private void ApplySettingsToUi()
        {
            AutoUpdateCheckBox.IsChecked = CurrentSettings.AutoUpdateEnabled;

            foreach (ComboBoxItem item in RefreshIntervalComboBox.Items)
            {
                if (item.Content?.ToString() == CurrentSettings.RefreshIntervalSeconds.ToString())
                {
                    RefreshIntervalComboBox.SelectedItem = item;
                    break;
                }
            }

            if (RefreshIntervalComboBox.SelectedItem == null && RefreshIntervalComboBox.Items.Count > 0)
            {
                RefreshIntervalComboBox.SelectedIndex = 2; // 15
            }
        }

        private void ReadSettingsFromUi()
        {
            CurrentSettings.AutoUpdateEnabled = AutoUpdateCheckBox.IsChecked == true;
            CurrentSettings.RefreshIntervalSeconds = GetSelectedRefreshIntervalSeconds();
        }

        private void ConfigureTimer()
        {
            _timer?.Stop();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(GetSelectedRefreshIntervalSeconds())
            };

            _timer.Tick += async (_, _) =>
            {
                if (AutoUpdateCheckBox.IsChecked == true)
                    await RefreshAllAsync();
            };

            _timer.Start();
        }

        private int GetSelectedRefreshIntervalSeconds()
        {
            if (RefreshIntervalComboBox.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Content?.ToString(), out int seconds) &&
                seconds > 0)
            {
                return seconds;
            }

            return 15;
        }

        private async Task RefreshAllAsync()
        {
            if (_isRefreshing)
                return;

            try
            {
                _isRefreshing = true;
                SetStatus("Обновление данных...");

                await LoadAssetsAsync();
                RefreshPositionsGrid();
                RefreshTradesGrid();
                RefreshSummary();

                LastUpdateText.Text = $"Последнее обновление: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                SetStatus("Данные обновлены");
            }
            catch (Exception ex)
            {
                SetStatus($"Ошибка: {ex.Message}");
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async Task LoadAssetsAsync()
        {
            var selectedTicker = GetSelectedAsset()?.Ticker;

            var loadedAssets = await _marketDataService.GetAssetsAsync(_trackedTickers);

            if (loadedAssets.Count == 0)
            {
                SetStatus("Не удалось загрузить котировки");
                return;
            }

            _assets = loadedAssets
                .OrderBy(a => a.Ticker)
                .ToList();

            AssetsGrid.ItemsSource = null;
            AssetsGrid.ItemsSource = _assets;

            if (!string.IsNullOrWhiteSpace(selectedTicker))
            {
                var selectedAsset = _assets.FirstOrDefault(a => a.Ticker == selectedTicker);
                if (selectedAsset != null)
                    AssetsGrid.SelectedItem = selectedAsset;
            }

            UpdateTradeButtons();
        }

        private void RefreshSummary()
        {
            decimal positionsValue = CalculatePositionsValue();
            decimal totalValue = _portfolio.Balance + positionsValue;
            decimal totalCost = _portfolio.Positions.Sum(p => p.AveragePrice * p.Quantity);
            decimal totalPnL = positionsValue - totalCost;

            BalanceText.Text = $"Баланс: {_portfolio.Balance:F2}";
            PositionsValueText.Text = $"Позиции: {positionsValue:F2}";
            TotalValueText.Text = $"Итого: {totalValue:F2}";
            TotalPnLText.Text = $"PnL: {totalPnL:F2}";
        }

        private decimal CalculatePositionsValue()
        {
            return _portfolio.Positions.Sum(position =>
            {
                var asset = _assets.FirstOrDefault(a => a.Ticker == position.Ticker);
                return asset == null ? 0m : asset.CurrentPrice * position.Quantity;
            });
        }

        private void RefreshPositionsGrid()
        {
            var positions = _portfolio.Positions
                .Select(position =>
                {
                    var asset = _assets.FirstOrDefault(a => a.Ticker == position.Ticker);

                    decimal currentPrice = asset?.CurrentPrice ?? 0m;
                    decimal marketValue = currentPrice * position.Quantity;
                    decimal cost = position.AveragePrice * position.Quantity;
                    decimal pnl = marketValue - cost;
                    decimal pnlPercent = cost == 0 ? 0m : pnl / cost * 100m;

                    return new PositionDisplay
                    {
                        Ticker = position.Ticker,
                        Quantity = position.Quantity,
                        AveragePrice = position.AveragePrice,
                        CurrentPrice = currentPrice,
                        MarketValue = marketValue,
                        PnL = pnl,
                        PnLPercent = pnlPercent
                    };
                })
                .OrderBy(p => p.Ticker)
                .ToList();

            PositionsGrid.ItemsSource = null;
            PositionsGrid.ItemsSource = positions;
        }

        private void RefreshTradesGrid()
        {
            var trades = _tradingService.Trades
                .Where(t => t.MarketMode == MarketMode.Real)
                .OrderByDescending(t => t.Timestamp)
                .ToList();

            TradesGrid.ItemsSource = null;
            TradesGrid.ItemsSource = trades;
        }

        private Asset? GetSelectedAsset()
        {
            return AssetsGrid.SelectedItem as Asset;
        }

        private bool TryGetQuantity(out int quantity)
        {
            return int.TryParse(QuantityTextBox.Text, out quantity) && quantity > 0;
        }

        private void SetStatus(string text)
        {
            if (StatusText != null)
                StatusText.Text = text;
        }

        private void UpdateTradeButtons()
        {
            bool hasSelectedAsset = GetSelectedAsset() != null;

            if (BuyButton != null)
                BuyButton.IsEnabled = hasSelectedAsset;

            if (SellButton != null)
                SellButton.IsEnabled = hasSelectedAsset;
        }

        private async void RefreshNow_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAllAsync();
        }

        private void AutoUpdateCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
                return;

            ReadSettingsFromUi();
            SaveState();
            SetStatus("Автообновление включено");
        }

        private void AutoUpdateCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
                return;

            ReadSettingsFromUi();
            SaveState();
            SetStatus("Автообновление выключено");
        }

        private void RefreshIntervalComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized)
                return;

            ReadSettingsFromUi();
            ConfigureTimer();
            SaveState();
            SetStatus($"Интервал обновления: {CurrentSettings.RefreshIntervalSeconds} сек.");
        }

        private void AssetsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTradeButtons();
        }

        private async void AddTicker_Click(object sender, RoutedEventArgs e)
        {
            string ticker = TickerTextBox.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(ticker))
            {
                MessageBox.Show("Введи тикер.");
                return;
            }

            if (_trackedTickers.Contains(ticker))
            {
                MessageBox.Show("Этот тикер уже добавлен.");
                return;
            }

            SetStatus("Проверка тикера...");

            bool isValid = await _marketDataService.IsValidTickerAsync(ticker);
            if (!isValid)
            {
                MessageBox.Show("Тикер не найден или API не вернул данные.");
                SetStatus("Ошибка проверки тикера");
                return;
            }

            _trackedTickers.Add(ticker);
            _trackedTickers.Sort();

            SaveState();
            TickerTextBox.Clear();

            await RefreshAllAsync();
        }

        private async void RemoveTicker_Click(object sender, RoutedEventArgs e)
        {
            var asset = GetSelectedAsset();

            if (asset == null)
            {
                MessageBox.Show("Выбери тикер для удаления.");
                return;
            }

            bool hasOpenPosition = _portfolio.Positions.Any(p => p.Ticker == asset.Ticker && p.Quantity > 0);
            if (hasOpenPosition)
            {
                MessageBox.Show("Нельзя удалить тикер, пока по нему есть открытая позиция.");
                return;
            }

            _trackedTickers.Remove(asset.Ticker);
            SaveState();

            await RefreshAllAsync();
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

            bool success = _tradingService.BuyAsset(_portfolio, asset, quantity, MarketMode.Real, out string message);

            RefreshPositionsGrid();
            RefreshTradesGrid();
            RefreshSummary();

            if (success)
            {
                SaveState();
                QuantityTextBox.Clear();
            }

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

            bool success = _tradingService.SellAsset(_portfolio, asset, quantity, MarketMode.Real, out string message);

            RefreshPositionsGrid();
            RefreshTradesGrid();
            RefreshSummary();

            if (success)
            {
                SaveState();
                QuantityTextBox.Clear();
            }

            MessageBox.Show(message);
        }
    }
}
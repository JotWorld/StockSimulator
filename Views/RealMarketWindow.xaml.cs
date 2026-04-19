using Microsoft.Win32;
using StockExchangeSimulator.Data;
using StockExchangeSimulator.Enums;
using StockExchangeSimulator.Models;
using StockExchangeSimulator.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace StockExchangeSimulator.Views
{
    public partial class RealMarketWindow : Window
    {
        private readonly RealMarketDataService _marketDataService = new();
        private readonly TradingService _tradingService = new();
        private readonly IRealMarketRepository _repository = new SqliteRealMarketRepository(new RealMarketDbService());
        private readonly RealMarketPortfolioService _portfolioService = new();
        private readonly Portfolio _portfolio = new();
        private readonly List<string> _trackedTickers = new();

        private readonly decimal _defaultBalance = 10000m;

        private List<Asset> _assets = new();
        private List<PortfolioSnapshot> _snapshots = new();

        private DispatcherTimer? _timer;
        private bool _isRefreshing;
        private bool _isInitialized;
        private DateTime? _lastSuccessfulUpdateUtc;
        private string _lastRefreshStatus = string.Empty;

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

            await RefreshAllAsync(captureSnapshot: true, snapshotSource: "startup");

            if (_portfolio.Balance <= 0 && !_portfolio.Positions.Any() && !_tradingService.Trades.Any())
            {
                _portfolio.Balance = _defaultBalance;
            }

            RefreshPositionsGrid();
            RefreshTradesGrid();
            RefreshSummary();
            RefreshTradeStats();
            RefreshTickerAnalyticsGrid();
            RefreshSnapshotsGrid();
            RefreshEquityCurve();
            UpdateTradeButtons();
            UpdateLastUpdateText();

            _isInitialized = true;
        }

        private void RealMarketWindow_Closed(object? sender, EventArgs e)
        {
            _timer?.Stop();
            SaveState();
        }

        private void LoadState()
        {
            var state = _repository.LoadState();

            _portfolio.Balance = state.Balance;
            _portfolio.Positions = state.Positions ?? new List<Position>();

            _trackedTickers.Clear();
            _trackedTickers.AddRange(state.TrackedTickers ?? new List<string>());

            _tradingService.SetTrades(state.Trades);
            CurrentSettings = state.Settings ?? new RealMarketSettings();

            _lastSuccessfulUpdateUtc = state.LastSuccessfulUpdateUtc;
            _lastRefreshStatus = state.LastRefreshStatus ?? string.Empty;
            _snapshots = state.Snapshots ?? new List<PortfolioSnapshot>();
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
                Settings = CurrentSettings,
                LastSuccessfulUpdateUtc = _lastSuccessfulUpdateUtc,
                LastRefreshStatus = _lastRefreshStatus,
                Snapshots = _snapshots
            };

            _repository.SaveState(state);
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
                RefreshIntervalComboBox.SelectedIndex = 2;
            }

            if (TradeTypeFilterComboBox.Items.Count > 0 && TradeTypeFilterComboBox.SelectedIndex < 0)
            {
                TradeTypeFilterComboBox.SelectedIndex = 0;
            }

            if (TradeDateFilterComboBox.Items.Count > 0 && TradeDateFilterComboBox.SelectedIndex < 0)
            {
                TradeDateFilterComboBox.SelectedIndex = 0;
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
                    await RefreshAllAsync(captureSnapshot: true, snapshotSource: "auto refresh");
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

        private async System.Threading.Tasks.Task RefreshAllAsync(bool captureSnapshot, string snapshotSource)
        {
            if (_isRefreshing)
                return;

            try
            {
                _isRefreshing = true;
                SetStatus("Обновление данных...");

                bool assetsUpdated = await LoadAssetsAsync();

                RefreshPositionsGrid();
                RefreshTradesGrid();
                RefreshSummary();
                RefreshTradeStats();
                RefreshTickerAnalyticsGrid();
                RefreshSnapshotsGrid();

                if (assetsUpdated)
                {
                    _lastSuccessfulUpdateUtc = DateTime.UtcNow;
                    UpdateLastUpdateText();

                    if (captureSnapshot)
                    {
                        CaptureSnapshot(snapshotSource);
                    }
                }

                RefreshEquityCurve();
                SaveState();
            }
            catch (Exception ex)
            {
                _lastRefreshStatus = $"Ошибка обновления: {ex.Message}";
                SetStatus(_lastRefreshStatus);
                RefreshStatsText.Text = "Обновление завершилось с ошибкой.";
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private async System.Threading.Tasks.Task<bool> LoadAssetsAsync()
        {
            string? selectedTicker = GetSelectedAsset()?.Ticker;
            var fetchResult = await _marketDataService.GetAssetsAsync(_trackedTickers);

            RefreshStatsText.Text =
                $"Запрошено: {fetchResult.RequestedCount} | Успешно: {fetchResult.SuccessCount} | Ошибок: {fetchResult.ErrorCount}";

            if (!fetchResult.HasSuccess)
            {
                _lastRefreshStatus = fetchResult.BuildUserMessage();
                SetStatus(_lastRefreshStatus);
                UpdateTradeButtons();
                return false;
            }

            _assets = fetchResult.Assets
                .OrderBy(a => a.Ticker)
                .ToList();

            RefreshAssetsGrid(selectedTicker);

            _lastRefreshStatus = fetchResult.BuildUserMessage();
            SetStatus(_lastRefreshStatus);
            UpdateTradeButtons();

            return true;
        }

        private void RefreshAssetsGrid(string? selectedTicker = null)
        {
            string filter = AssetFilterTextBox?.Text?.Trim().ToUpperInvariant() ?? string.Empty;

            var filteredAssets = _assets
                .Where(a =>
                    string.IsNullOrWhiteSpace(filter) ||
                    a.Ticker.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Ticker)
                .ToList();

            AssetsGrid.ItemsSource = null;
            AssetsGrid.ItemsSource = filteredAssets;

            RestoreSelectedAsset(selectedTicker);
            ApplyAssetsGridCellColors();
        }

        private void RestoreSelectedAsset(string? selectedTicker)
        {
            if (string.IsNullOrWhiteSpace(selectedTicker))
                return;

            if (AssetsGrid.ItemsSource is not IEnumerable<Asset> items)
                return;

            var selectedAsset = items.FirstOrDefault(a => a.Ticker == selectedTicker);
            if (selectedAsset != null)
            {
                AssetsGrid.SelectedItem = selectedAsset;
            }
        }

        private void RefreshSummary()
        {
            var summary = _portfolioService.BuildSummary(_portfolio, _assets, _tradingService.Trades);

            BalanceText.Text = $"Баланс: {summary.Balance:F2}";
            PositionsValueText.Text = $"Позиции: {summary.PositionsValue:F2}";
            TotalValueText.Text = $"Итого: {summary.TotalValue:F2}";
            UnrealizedPnLText.Text = $"Unrealized PnL: {summary.UnrealizedPnL:F2}";
            RealizedPnLText.Text = $"Realized PnL: {summary.RealizedPnL:F2}";
            FeesText.Text = $"Комиссии: {summary.TotalFees:F2}";
            TotalPnLText.Text = $"Total PnL: {summary.TotalPnL:F2}";

            ApplySignedColor(UnrealizedPnLText, summary.UnrealizedPnL);
            ApplySignedColor(RealizedPnLText, summary.RealizedPnL);
            ApplySignedColor(TotalPnLText, summary.TotalPnL);
            FeesText.Foreground = Brushes.DarkOrange;
        }

        private void RefreshPositionsGrid()
        {
            var positions = _portfolioService.BuildPositionDisplays(_portfolio, _assets);

            PositionsGrid.ItemsSource = null;
            PositionsGrid.ItemsSource = positions;
        }

        private void RefreshTradesGrid()
        {
            var trades = GetFilteredRealTrades();

            TradesGrid.ItemsSource = null;
            TradesGrid.ItemsSource = trades;
        }

        private void RefreshTradeStats()
        {
            var stats = _portfolioService.BuildTradeStats(GetFilteredRealTrades(), _portfolio.Positions);

            StatsTotalTradesText.Text = $"Всего сделок: {stats.TotalTrades}";
            StatsBuyTradesText.Text = $"Buy: {stats.BuyTrades}";
            StatsSellTradesText.Text = $"Sell: {stats.SellTrades}";
            StatsWinRateText.Text = $"Win rate: {stats.WinRatePercent:F2}%";
            StatsBuyVolumeText.Text = $"Объём Buy: {stats.TotalBuyVolume:F2}";
            StatsSellVolumeText.Text = $"Объём Sell: {stats.TotalSellVolume:F2}";
            StatsAverageWinText.Text = $"Average win: {stats.AverageWin:F2}";
            StatsAverageLossText.Text = $"Average loss: {stats.AverageLoss:F2}";
            StatsProfitFactorText.Text = $"Profit factor: {stats.ProfitFactor:F2}";
            StatsAverageFeeText.Text = $"Средняя комиссия: {stats.AverageFee:F2}";
            StatsBestTickerText.Text = $"Best ticker: {stats.BestTicker} ({stats.BestTickerPnL:F2})";
            StatsWorstTickerText.Text = $"Worst ticker: {stats.WorstTicker} ({stats.WorstTickerPnL:F2})";

            ApplySignedColor(StatsAverageWinText, stats.AverageWin);
            ApplySignedColor(StatsAverageLossText, stats.AverageLoss);
            ApplySignedColor(StatsBestTickerText, stats.BestTickerPnL);
            ApplySignedColor(StatsWorstTickerText, stats.WorstTickerPnL);
        }

        private void RefreshTickerAnalyticsGrid()
        {
            var analytics = _portfolioService.BuildTickerAnalytics(_tradingService.Trades, _portfolio.Positions);

            TickerAnalyticsGrid.ItemsSource = null;
            TickerAnalyticsGrid.ItemsSource = analytics;
        }

        private void RefreshSnapshotsGrid()
        {
            var snapshots = _snapshots
                .OrderByDescending(s => s.Timestamp)
                .ToList();

            SnapshotsGrid.ItemsSource = null;
            SnapshotsGrid.ItemsSource = snapshots;
        }

        private List<Trade> GetFilteredRealTrades()
        {
            var trades = _tradingService.Trades
                .Where(t => t.MarketMode == MarketMode.Real)
                .OrderByDescending(t => t.Timestamp)
                .ToList();

            string tickerFilter = TradeTickerFilterTextBox?.Text?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(tickerFilter))
            {
                trades = trades
                    .Where(t => t.Ticker.Contains(tickerFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            string tradeTypeFilter = (TradeTypeFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все";
            if (tradeTypeFilter == "Buy")
            {
                trades = trades.Where(t => t.TradeType == TradeType.Buy).ToList();
            }
            else if (tradeTypeFilter == "Sell")
            {
                trades = trades.Where(t => t.TradeType == TradeType.Sell).ToList();
            }

            string dateFilter = (TradeDateFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все";
            DateTime now = DateTime.Now;

            if (dateFilter == "Сегодня")
            {
                trades = trades.Where(t => t.Timestamp.Date == now.Date).ToList();
            }
            else if (dateFilter == "7 дней")
            {
                DateTime from = now.AddDays(-7);
                trades = trades.Where(t => t.Timestamp >= from).ToList();
            }
            else if (dateFilter == "30 дней")
            {
                DateTime from = now.AddDays(-30);
                trades = trades.Where(t => t.Timestamp >= from).ToList();
            }

            return trades;
        }

        private void CaptureSnapshot(string source)
        {
            var snapshot = _portfolioService.BuildSnapshot(_portfolio, _assets, _tradingService.Trades, source);
            _snapshots.Add(snapshot);

            int maxSnapshots = EnvService.GetInt("REAL_MARKET_MAX_SNAPSHOTS", 500);
            if (maxSnapshots <= 0)
                maxSnapshots = 500;

            _snapshots = _snapshots
                .OrderByDescending(s => s.Timestamp)
                .Take(maxSnapshots)
                .OrderBy(s => s.Timestamp)
                .ToList();
        }

        private void RefreshEquityCurve()
        {
            if (EquityCanvas == null)
                return;

            EquityCanvas.Children.Clear();

            var points = _snapshots
                .OrderBy(s => s.Timestamp)
                .ToList();

            ChartPointsText.Text = $"Точек: {points.Count}";

            if (points.Count < 2 || EquityCanvas.ActualWidth <= 20 || EquityCanvas.ActualHeight <= 20)
            {
                EquityEmptyText.Visibility = Visibility.Visible;
                ChartMinText.Text = "Min: -";
                ChartMaxText.Text = "Max: -";
                ChartLatestText.Text = "Latest: -";
                return;
            }

            EquityEmptyText.Visibility = Visibility.Collapsed;

            decimal minValue = points.Min(p => p.TotalValue);
            decimal maxValue = points.Max(p => p.TotalValue);
            decimal latestValue = points.Last().TotalValue;

            ChartMinText.Text = $"Min: {minValue:F2}";
            ChartMaxText.Text = $"Max: {maxValue:F2}";
            ChartLatestText.Text = $"Latest: {latestValue:F2}";
            ApplySignedColor(ChartLatestText, latestValue - _defaultBalance);

            double width = EquityCanvas.ActualWidth;
            double height = EquityCanvas.ActualHeight;

            double leftPadding = 55;
            double rightPadding = 15;
            double topPadding = 15;
            double bottomPadding = 30;

            double plotWidth = Math.Max(10, width - leftPadding - rightPadding);
            double plotHeight = Math.Max(10, height - topPadding - bottomPadding);

            decimal range = maxValue - minValue;
            if (range == 0m)
                range = 1m;

            DrawChartAxes(leftPadding, topPadding, plotWidth, plotHeight, minValue, maxValue);

            var polyline = new Polyline
            {
                Stroke = Brushes.SteelBlue,
                StrokeThickness = 2
            };

            for (int i = 0; i < points.Count; i++)
            {
                double x = leftPadding + (plotWidth * i / Math.Max(1, points.Count - 1));
                double normalizedY = (double)((points[i].TotalValue - minValue) / range);
                double y = topPadding + plotHeight - (normalizedY * plotHeight);

                polyline.Points.Add(new Point(x, y));

                var dot = new Ellipse
                {
                    Width = 5,
                    Height = 5,
                    Fill = Brushes.SteelBlue
                };

                Canvas.SetLeft(dot, x - 2.5);
                Canvas.SetTop(dot, y - 2.5);
                EquityCanvas.Children.Add(dot);
            }

            EquityCanvas.Children.Add(polyline);

            DrawLatestValueLabel(points.Last(), leftPadding, topPadding, plotWidth, plotHeight, minValue, range);
            DrawTimeLabels(points, leftPadding, topPadding, plotWidth, plotHeight);
        }

        private void DrawChartAxes(double leftPadding, double topPadding, double plotWidth, double plotHeight, decimal minValue, decimal maxValue)
        {
            var yAxis = new Line
            {
                X1 = leftPadding,
                Y1 = topPadding,
                X2 = leftPadding,
                Y2 = topPadding + plotHeight,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };

            var xAxis = new Line
            {
                X1 = leftPadding,
                Y1 = topPadding + plotHeight,
                X2 = leftPadding + plotWidth,
                Y2 = topPadding + plotHeight,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };

            EquityCanvas.Children.Add(yAxis);
            EquityCanvas.Children.Add(xAxis);

            AddAxisLabel(minValue.ToString("F2", CultureInfo.InvariantCulture), 5, topPadding + plotHeight - 10);
            AddAxisLabel(maxValue.ToString("F2", CultureInfo.InvariantCulture), 5, topPadding - 5);
        }

        private void DrawLatestValueLabel(PortfolioSnapshot snapshot, double leftPadding, double topPadding, double plotWidth, double plotHeight, decimal minValue, decimal range)
        {
            double x = leftPadding + plotWidth;
            double normalizedY = (double)((snapshot.TotalValue - minValue) / range);
            double y = topPadding + plotHeight - (normalizedY * plotHeight);

            var label = new TextBlock
            {
                Text = snapshot.TotalValue.ToString("F2"),
                Foreground = Brushes.SteelBlue,
                FontWeight = FontWeights.SemiBold,
                Background = Brushes.White
            };

            Canvas.SetLeft(label, Math.Max(0, x - 55));
            Canvas.SetTop(label, Math.Max(0, y - 22));
            EquityCanvas.Children.Add(label);
        }

        private void DrawTimeLabels(List<PortfolioSnapshot> points, double leftPadding, double topPadding, double plotWidth, double plotHeight)
        {
            var first = points.First();
            var last = points.Last();

            AddAxisLabel(first.Timestamp.ToString("dd.MM HH:mm"), leftPadding, topPadding + plotHeight + 4);
            AddAxisLabel(last.Timestamp.ToString("dd.MM HH:mm"), leftPadding + plotWidth - 75, topPadding + plotHeight + 4);
        }

        private void AddAxisLabel(string text, double x, double y)
        {
            var label = new TextBlock
            {
                Text = text,
                Foreground = Brushes.Gray,
                FontSize = 11
            };

            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            EquityCanvas.Children.Add(label);
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

            BuyButton.IsEnabled = hasSelectedAsset;
            SellButton.IsEnabled = hasSelectedAsset;
        }

        private void UpdateLastUpdateText()
        {
            if (_lastSuccessfulUpdateUtc.HasValue)
            {
                LastUpdateText.Text = $"Последнее успешное обновление: {_lastSuccessfulUpdateUtc.Value.ToLocalTime():dd.MM.yyyy HH:mm:ss}";
            }
            else
            {
                LastUpdateText.Text = "Последнее успешное обновление: нет";
            }
        }

        private async void RefreshNow_Click(object sender, RoutedEventArgs e)
        {
            await RefreshAllAsync(captureSnapshot: true, snapshotSource: "manual refresh");
        }

        private void AutoUpdateCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
                return;

            ReadSettingsFromUi();
            SaveState();
            SetStatus("Автообновление включено.");
        }

        private void AutoUpdateCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
                return;

            ReadSettingsFromUi();
            SaveState();
            SetStatus("Автообновление выключено.");
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

        private void AssetFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string? selectedTicker = GetSelectedAsset()?.Ticker;
            RefreshAssetsGrid(selectedTicker);
        }

        private async void AddTicker_Click(object sender, RoutedEventArgs e)
        {
            string ticker = TickerTextBox.Text.Trim().ToUpperInvariant();

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

            var validation = await _marketDataService.ValidateTickerAsync(ticker);

            if (!validation.IsValid)
            {
                MessageBox.Show(validation.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetStatus($"Ошибка проверки тикера {validation.Ticker}");
                return;
            }

            _trackedTickers.Add(ticker);
            _trackedTickers.Sort();

            SaveState();
            TickerTextBox.Clear();

            await RefreshAllAsync(captureSnapshot: false, snapshotSource: "ticker added");
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

            var confirm = MessageBox.Show(
                $"Удалить тикер {asset.Ticker} из отслеживания?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            _trackedTickers.Remove(asset.Ticker);
            _assets = _assets.Where(a => a.Ticker != asset.Ticker).ToList();

            RefreshAssetsGrid();
            SaveState();
            await RefreshAllAsync(captureSnapshot: false, snapshotSource: "ticker removed");
        }

        private void Buy_Click(object sender, RoutedEventArgs e)
        {
            ExecuteTradeWithQuantityFromTextBox(isBuy: true);
        }

        private void Sell_Click(object sender, RoutedEventArgs e)
        {
            ExecuteTradeWithQuantityFromTextBox(isBuy: false);
        }

        private void QuickBuy1_Click(object sender, RoutedEventArgs e)
        {
            ExecuteQuickBuy(1);
        }

        private void QuickBuy5_Click(object sender, RoutedEventArgs e)
        {
            ExecuteQuickBuy(5);
        }

        private void QuickBuy10_Click(object sender, RoutedEventArgs e)
        {
            ExecuteQuickBuy(10);
        }

        private void SellAll_Click(object sender, RoutedEventArgs e)
        {
            var asset = GetSelectedAsset();

            if (asset == null)
            {
                MessageBox.Show("Выбери актив.");
                return;
            }

            var position = _portfolio.Positions.FirstOrDefault(p => p.Ticker == asset.Ticker);
            if (position == null || position.Quantity <= 0)
            {
                MessageBox.Show("По этому тикеру нет открытой позиции.");
                return;
            }

            ExecuteSell(asset, position.Quantity);
        }

        private void ExecuteTradeWithQuantityFromTextBox(bool isBuy)
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

            if (isBuy)
                ExecuteBuy(asset, quantity);
            else
                ExecuteSell(asset, quantity);
        }

        private void ExecuteQuickBuy(int quantity)
        {
            var asset = GetSelectedAsset();

            if (asset == null)
            {
                MessageBox.Show("Выбери актив.");
                return;
            }

            ExecuteBuy(asset, quantity);
        }

        private void ExecuteBuy(Asset asset, int quantity)
        {
            var result = _tradingService.ExecuteBuyAsset(_portfolio, asset, quantity, MarketMode.Real);

            RefreshPositionsGrid();
            RefreshTradesGrid();
            RefreshSummary();
            RefreshTradeStats();
            RefreshTickerAnalyticsGrid();

            if (result.IsSuccess)
            {
                CaptureSnapshot("buy");
                SaveState();
                QuantityTextBox.Clear();
                SetStatus("Покупка выполнена.");
                RefreshSnapshotsGrid();
                RefreshEquityCurve();
            }

            MessageBox.Show(result.Message);
        }

        private void ExecuteSell(Asset asset, int quantity)
        {
            var result = _tradingService.ExecuteSellAsset(_portfolio, asset, quantity, MarketMode.Real);

            RefreshPositionsGrid();
            RefreshTradesGrid();
            RefreshSummary();
            RefreshTradeStats();
            RefreshTickerAnalyticsGrid();

            if (result.IsSuccess)
            {
                CaptureSnapshot("sell");
                SaveState();
                QuantityTextBox.Clear();
                SetStatus("Продажа выполнена.");
                RefreshSnapshotsGrid();
                RefreshEquityCurve();
            }

            MessageBox.Show(result.Message);
        }

        private async void ResetPortfolio_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Сбросить портфель? Это удалит все позиции и историю сделок Real Market.",
                "Подтверждение сброса",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            _portfolio.Balance = _defaultBalance;
            _portfolio.Positions.Clear();
            _tradingService.ClearTrades();
            _snapshots.Clear();

            RefreshPositionsGrid();
            RefreshTradesGrid();
            RefreshSummary();
            RefreshTradeStats();
            RefreshTickerAnalyticsGrid();

            CaptureSnapshot("reset");
            RefreshSnapshotsGrid();
            RefreshEquityCurve();

            SaveState();
            SetStatus("Портфель сброшен.");

            await RefreshAllAsync(captureSnapshot: false, snapshotSource: "reset refresh");
        }

        private void TradeFilter_Changed(object sender, EventArgs e)
        {
            if (!_isInitialized)
                return;

            RefreshTradesGrid();
            RefreshTradeStats();
        }

        private void ExportTrades_Click(object sender, RoutedEventArgs e)
        {
            var trades = TradesGrid.ItemsSource as IEnumerable<Trade>;
            if (trades == null || !trades.Any())
            {
                MessageBox.Show("Нет сделок для экспорта.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Экспорт истории сделок",
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"real_market_trades_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Timestamp,Ticker,TradeType,Quantity,Price,TotalAmount,Fee,RealizedPnL,MarketMode");

                foreach (var trade in trades)
                {
                    sb.AppendLine(string.Join(",",
                        EscapeCsv(trade.Timestamp.ToString("dd.MM.yyyy HH:mm:ss")),
                        EscapeCsv(trade.Ticker),
                        EscapeCsv(trade.TradeType.ToString()),
                        trade.Quantity,
                        trade.Price.ToString("F2"),
                        trade.TotalAmount.ToString("F2"),
                        trade.Fee.ToString("F2"),
                        trade.RealizedPnL.ToString("F2"),
                        EscapeCsv(trade.MarketMode.ToString())));
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("История сделок экспортирована.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}");
            }
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private void SignedValueTextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBlock textBlock)
                return;

            if (textBlock.DataContext is PositionDisplay positionDisplay)
            {
                if (decimal.TryParse(textBlock.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out decimal parsedCurrentCulture) ||
                    decimal.TryParse(textBlock.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedCurrentCulture))
                {
                    ApplySignedColor(textBlock, parsedCurrentCulture);
                    return;
                }

                ApplySignedColor(textBlock, positionDisplay.PnL);
                return;
            }

            if (textBlock.DataContext is Trade trade)
            {
                ApplySignedColor(textBlock, trade.RealizedPnL);
            }
        }

        private void FeeTextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                textBlock.Foreground = Brushes.DarkOrange;
            }
        }

        private void ApplySignedColor(TextBlock textBlock, decimal value)
        {
            if (value > 0m)
            {
                textBlock.Foreground = Brushes.ForestGreen;
            }
            else if (value < 0m)
            {
                textBlock.Foreground = Brushes.IndianRed;
            }
            else
            {
                textBlock.Foreground = Brushes.Black;
            }
        }

        private void ApplyAssetsGridCellColors()
        {
            AssetsGrid.LoadingRow -= AssetsGrid_LoadingRow;
            AssetsGrid.LoadingRow += AssetsGrid_LoadingRow;
        }

        private void AssetsGrid_LoadingRow(object? sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is not Asset asset)
                return;

            e.Row.Loaded += (_, _) =>
            {
                var changeTextBlock = FindTextBlockInCell(e.Row, 3);
                var changePercentTextBlock = FindTextBlockInCell(e.Row, 4);

                if (changeTextBlock != null)
                    ApplySignedColor(changeTextBlock, asset.Change);

                if (changePercentTextBlock != null)
                    ApplySignedColor(changePercentTextBlock, asset.ChangePercent);
            };
        }

        private void EquityCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isInitialized)
            {
                RefreshEquityCurve();
            }
        }

        private void AnalyticsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized)
                return;

            if (AnalyticsTabControl.SelectedItem is TabItem tab && tab.Header?.ToString() == "Equity Curve")
            {
                RefreshEquityCurve();
            }
        }

        private static TextBlock? FindTextBlockInCell(DataGridRow row, int columnIndex)
        {
            var presenter = FindVisualChild<DataGridCellsPresenter>(row);
            if (presenter == null)
                return null;

            var cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
            if (cell == null)
                return null;

            return FindVisualChild<TextBlock>(cell);
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
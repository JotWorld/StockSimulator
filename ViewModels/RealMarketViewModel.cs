using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using StockExchangeSimulator.Data;
using StockExchangeSimulator.Enums;
using StockExchangeSimulator.Models;
using StockExchangeSimulator.Services;

namespace StockExchangeSimulator.ViewModels
{
    public class RealMarketViewModel : ViewModelBase
    {
        private readonly RealMarketDataService _dataService;
        private readonly TradingService _tradingService;
        private readonly RealMarketPortfolioService _portfolioService;
        private readonly RealMarketStateService _stateService;
        private readonly IRealMarketRepository _repository;
        private readonly IDialogService _dialogService;
        private readonly DispatcherTimer _timer;

        private readonly Portfolio _portfolio = new();
        private readonly List<string> _trackedTickers = new();
        private readonly List<Asset> _assets = new();
        private readonly List<PortfolioSnapshot> _snapshots = new();

        private DateTime? _lastSuccessfulUpdateUtc;
        private string _lastRefreshStatus = string.Empty;

        public ObservableCollection<AssetItemViewModel> Assets { get; } = new();
        public ObservableCollection<PositionDisplayItemViewModel> Positions { get; } = new();
        public ObservableCollection<TradeItemViewModel> Trades { get; } = new();
        public ObservableCollection<TickerAnalyticsItemViewModel> TickerAnalytics { get; } = new();
        public ObservableCollection<PortfolioSnapshotItemViewModel> Snapshots { get; } = new();

        public PortfolioSummaryViewModel Summary { get; } = new();
        public TradeStatsViewModel Stats { get; } = new();

        private AssetItemViewModel? _selectedAsset;
        public AssetItemViewModel? SelectedAsset
        {
            get => _selectedAsset;
            set
            {
                if (SetProperty(ref _selectedAsset, value))
                {
                    OnPropertyChanged(nameof(SelectedTicker));
                    UpdateTradeCommands();
                }
            }
        }

        public string SelectedTicker => SelectedAsset?.Ticker ?? "-";

        private string _quantityText = "1";
        public string QuantityText
        {
            get => _quantityText;
            set
            {
                if (SetProperty(ref _quantityText, value))
                {
                    OnPropertyChanged(nameof(TradeQuantity));
                    UpdateTradeCommands();
                }
            }
        }

        public int TradeQuantity
        {
            get
            {
                return int.TryParse(QuantityText, out int value) && value > 0
                    ? value
                    : 0;
            }
        }

        private string _newTickerText = string.Empty;
        public string NewTickerText
        {
            get => _newTickerText;
            set => SetProperty(ref _newTickerText, value);
        }

        private string _assetFilterText = string.Empty;
        public string AssetFilterText
        {
            get => _assetFilterText;
            set
            {
                if (SetProperty(ref _assetFilterText, value))
                {
                    RefreshAssetsCollection();
                }
            }
        }

        private bool _autoUpdateEnabled;
        public bool AutoUpdateEnabled
        {
            get => _autoUpdateEnabled;
            set
            {
                if (SetProperty(ref _autoUpdateEnabled, value))
                {
                    ConfigureTimer();
                }
            }
        }

        private int _refreshIntervalSeconds = 15;
        public int RefreshIntervalSeconds
        {
            get => _refreshIntervalSeconds;
            set
            {
                if (SetProperty(ref _refreshIntervalSeconds, value))
                {
                    ConfigureTimer();
                }
            }
        }

        private string _status = "Готов";
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private string _refreshStats = string.Empty;
        public string RefreshStats
        {
            get => _refreshStats;
            set => SetProperty(ref _refreshStats, value);
        }

        private string _lastUpdateText = "Последнее обновление: —";
        public string LastUpdateText
        {
            get => _lastUpdateText;
            set => SetProperty(ref _lastUpdateText, value);
        }

        public IEnumerable<int> RefreshIntervalOptions { get; } = new[] { 5, 10, 15, 30, 60 };

        public ICommand RefreshCommand { get; }
        public ICommand AddTickerCommand { get; }
        public ICommand RemoveTickerCommand { get; }
        public ICommand BuyCommand { get; }
        public ICommand SellCommand { get; }
        public ICommand QuickBuy1Command { get; }
        public ICommand QuickBuy5Command { get; }
        public ICommand QuickBuy10Command { get; }
        public ICommand SellAllCommand { get; }
        public ICommand ResetPortfolioCommand { get; }
        public ICommand ExportTradesCommand { get; }

        public RealMarketViewModel(
            RealMarketDataService dataService,
            TradingService tradingService,
            RealMarketPortfolioService portfolioService,
            RealMarketStateService stateService,
            IRealMarketRepository repository,
            IDialogService dialogService)
        {
            _dataService = dataService;
            _tradingService = tradingService;
            _portfolioService = portfolioService;
            _stateService = stateService;
            _repository = repository;
            _dialogService = dialogService;

            _timer = new DispatcherTimer();
            _timer.Tick += async (_, _) => await RefreshAllAsync(true, "Auto");

            RefreshCommand = new AsyncRelayCommand(() => RefreshAllAsync(true, "Manual"));
            AddTickerCommand = new AsyncRelayCommand(AddTickerAsync);
            RemoveTickerCommand = new AsyncRelayCommand(RemoveSelectedTickerAsync, CanRemoveSelectedTicker);
            BuyCommand = new RelayCommand(() => ExecuteBuy(TradeQuantity), CanTrade);
            SellCommand = new RelayCommand(() => ExecuteSell(TradeQuantity), CanTrade);
            QuickBuy1Command = new RelayCommand(() => ExecuteBuy(1), CanTradeWithSelectedAsset);
            QuickBuy5Command = new RelayCommand(() => ExecuteBuy(5), CanTradeWithSelectedAsset);
            QuickBuy10Command = new RelayCommand(() => ExecuteBuy(10), CanTradeWithSelectedAsset);
            SellAllCommand = new RelayCommand(SellAll, CanSellAll);
            ResetPortfolioCommand = new RelayCommand(ResetPortfolio);
            ExportTradesCommand = new RelayCommand(ExportTrades, () => Trades.Count > 0);

            LoadState();
            _ = RefreshAllAsync(false, "Initial");
        }

        private void LoadState()
        {
            var state = _repository.LoadState();

            _portfolio.Balance = state.Balance;
            _portfolio.Positions = state.Positions ?? new List<Position>();

            _trackedTickers.Clear();
            _trackedTickers.AddRange(state.TrackedTickers ?? new List<string>());

            _snapshots.Clear();
            _snapshots.AddRange(state.Snapshots ?? new List<PortfolioSnapshot>());

            _lastSuccessfulUpdateUtc = state.LastSuccessfulUpdateUtc;
            _lastRefreshStatus = state.LastRefreshStatus ?? string.Empty;

            _tradingService.SetTrades(state.Trades ?? new List<Trade>());

            AutoUpdateEnabled = state.Settings?.AutoUpdateEnabled ?? true;
            RefreshIntervalSeconds = state.Settings?.RefreshIntervalSeconds ?? 15;

            RefreshTradesCollection();
            RefreshSnapshotsCollection();
            RefreshDerivedCollections();
            UpdateLastUpdateText();

            Status = string.IsNullOrWhiteSpace(_lastRefreshStatus)
                ? "Состояние загружено"
                : _lastRefreshStatus;
        }

        private void SaveState()
        {
            var state = new RealMarketState
            {
                Balance = _portfolio.Balance,
                Positions = _portfolio.Positions
                    .Select(p => new Position
                    {
                        Ticker = p.Ticker,
                        Quantity = p.Quantity,
                        AveragePrice = p.AveragePrice
                    })
                    .ToList(),
                Trades = _tradingService.Trades
                    .Select(t => new Trade
                    {
                        Timestamp = t.Timestamp,
                        Ticker = t.Ticker,
                        TradeType = t.TradeType,
                        Quantity = t.Quantity,
                        Price = t.Price,
                        Fee = t.Fee,
                        RealizedPnL = t.RealizedPnL,
                        MarketMode = t.MarketMode
                    })
                    .ToList(),
                TrackedTickers = _trackedTickers.ToList(),
                Settings = new RealMarketSettings
                {
                    AutoUpdateEnabled = AutoUpdateEnabled,
                    RefreshIntervalSeconds = RefreshIntervalSeconds
                },
                LastSuccessfulUpdateUtc = _lastSuccessfulUpdateUtc,
                LastRefreshStatus = _lastRefreshStatus,
                Snapshots = _snapshots
                    .Select(s => new PortfolioSnapshot
                    {
                        Timestamp = s.Timestamp,
                        Source = s.Source,
                        Balance = s.Balance,
                        PositionsValue = s.PositionsValue,
                        TotalValue = s.TotalValue,
                        UnrealizedPnL = s.UnrealizedPnL,
                        RealizedPnL = s.RealizedPnL,
                        TotalPnL = s.TotalPnL,
                        TotalFees = s.TotalFees
                    })
                    .ToList()
            };

            _repository.SaveState(state);
            _stateService.Save(state);
        }

        private void ConfigureTimer()
        {
            _timer.Stop();

            if (!AutoUpdateEnabled)
                return;

            if (RefreshIntervalSeconds <= 0)
                RefreshIntervalSeconds = 15;

            _timer.Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds);
            _timer.Start();
        }

        private async Task RefreshAllAsync(bool captureSnapshot, string snapshotSource)
        {
            Status = "Обновление рынка...";
            RefreshStats = "Загрузка котировок...";

            var batch = await _dataService.GetAssetsAsync(_trackedTickers);

            _assets.Clear();
            _assets.AddRange(batch.Assets.OrderBy(a => a.Ticker));

            RefreshAssetsCollection();
            RefreshDerivedCollections();

            if (batch.Errors.Count == 0)
            {
                _lastSuccessfulUpdateUtc = DateTime.Now;
                _lastRefreshStatus = $"Обновлено {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                RefreshStats = $"Успешно: {batch.Assets.Count}";
            }
            else
            {
                _lastRefreshStatus = $"Частично обновлено: {batch.Assets.Count} OK / {batch.Errors.Count} ошибок";
                RefreshStats = _lastRefreshStatus;
            }

            UpdateLastUpdateText();
            Status = _lastRefreshStatus;

            if (captureSnapshot)
            {
                CaptureSnapshot(snapshotSource);
            }

            SaveState();
        }

        private async Task AddTickerAsync()
        {
            string ticker = (NewTickerText ?? string.Empty).Trim().ToUpperInvariant();

            if (string.IsNullOrWhiteSpace(ticker))
            {
                _dialogService.ShowWarning("Введи тикер.");
                return;
            }

            if (_trackedTickers.Contains(ticker))
            {
                _dialogService.ShowInfo("Этот тикер уже отслеживается.");
                return;
            }

            Status = $"Проверка тикера {ticker}...";

            var validation = await _dataService.ValidateTickerAsync(ticker);
            if (!validation.IsValid || validation.Asset == null)
            {
                _dialogService.ShowWarning(validation.Message);
                Status = validation.Message;
                return;
            }

            _trackedTickers.Add(ticker);
            _trackedTickers.Sort(StringComparer.OrdinalIgnoreCase);
            NewTickerText = string.Empty;

            await RefreshAllAsync(false, "AddTicker");
            SelectedAsset = Assets.FirstOrDefault(a => a.Ticker == ticker);

            SaveState();
        }

        private async Task RemoveSelectedTickerAsync()
        {
            if (SelectedAsset == null)
                return;

            string ticker = SelectedAsset.Ticker;

            bool hasOpenPosition = _portfolio.Positions.Any(p => p.Ticker == ticker && p.Quantity > 0);
            if (hasOpenPosition)
            {
                _dialogService.ShowWarning("Нельзя удалить тикер с открытой позицией.");
                return;
            }

            _trackedTickers.Remove(ticker);
            _assets.RemoveAll(a => a.Ticker == ticker);

            RefreshAssetsCollection();
            RefreshDerivedCollections();

            await Task.CompletedTask;
            SaveState();
        }

        private void ExecuteBuy(int quantity)
        {
            if (SelectedAsset == null)
                return;

            var asset = SelectedAsset.ToModel();
            var result = _tradingService.ExecuteBuyAsset(_portfolio, asset, quantity, MarketMode.Real);

            if (!result.IsSuccess)
            {
                _dialogService.ShowWarning(result.Message);
                Status = result.Message;
                return;
            }

            RefreshTradesCollection();
            RefreshDerivedCollections();
            CaptureSnapshot("Buy");
            SaveState();

            Status = result.Message;
        }

        private void ExecuteSell(int quantity)
        {
            if (SelectedAsset == null)
                return;

            var asset = SelectedAsset.ToModel();
            var result = _tradingService.ExecuteSellAsset(_portfolio, asset, quantity, MarketMode.Real);

            if (!result.IsSuccess)
            {
                _dialogService.ShowWarning(result.Message);
                Status = result.Message;
                return;
            }

            RefreshTradesCollection();
            RefreshDerivedCollections();
            CaptureSnapshot("Sell");
            SaveState();

            Status = result.Message;
        }

        private void SellAll()
        {
            if (SelectedAsset == null)
                return;

            var position = _portfolio.Positions.FirstOrDefault(p => p.Ticker == SelectedAsset.Ticker);
            if (position == null || position.Quantity <= 0)
                return;

            ExecuteSell(position.Quantity);
        }

        private void ResetPortfolio()
        {
            bool confirmed = _dialogService.Confirm(
                "Сбросить реальный портфель, сделки, снапшоты и вернуть стартовый баланс?",
                "Сброс портфеля");

            if (!confirmed)
                return;

            _portfolio.Balance = 10000m;
            _portfolio.Positions.Clear();
            _tradingService.ClearTrades();
            _snapshots.Clear();

            RefreshTradesCollection();
            RefreshSnapshotsCollection();
            RefreshDerivedCollections();

            Status = "Портфель сброшен.";
            SaveState();
        }

        private void ExportTrades()
        {
            string? filePath = _dialogService.SaveFile(
                "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                $"real_market_trades_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            if (string.IsNullOrWhiteSpace(filePath))
                return;

            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Ticker,TradeType,Quantity,Price,Fee,RealizedPnL,MarketMode");

            foreach (var trade in _tradingService.Trades.OrderBy(t => t.Timestamp))
            {
                sb.AppendLine(string.Join(",",
                    EscapeCsv(trade.Timestamp.ToString("O")),
                    EscapeCsv(trade.Ticker),
                    EscapeCsv(trade.TradeType.ToString()),
                    trade.Quantity,
                    trade.Price.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    trade.Fee.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    trade.RealizedPnL.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    EscapeCsv(trade.MarketMode.ToString())));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            Status = $"Экспортировано: {filePath}";
        }

        private void RefreshAssetsCollection()
        {
            string filter = (AssetFilterText ?? string.Empty).Trim();

            string? selectedTicker = SelectedAsset?.Ticker;

            var filtered = _assets
                .Where(a =>
                    string.IsNullOrWhiteSpace(filter) ||
                    a.Ticker.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Ticker)
                .Select(AssetItemViewModel.FromModel)
                .ToList();

            Assets.Clear();
            foreach (var item in filtered)
                Assets.Add(item);

            if (!string.IsNullOrWhiteSpace(selectedTicker))
            {
                SelectedAsset = Assets.FirstOrDefault(a => a.Ticker == selectedTicker);
            }
        }

        private void RefreshTradesCollection()
        {
            Trades.Clear();

            foreach (var trade in _tradingService.Trades
                         .OrderByDescending(t => t.Timestamp)
                         .Select(TradeItemViewModel.FromModel))
            {
                Trades.Add(trade);
            }

            if (ExportTradesCommand is RelayCommand exportCommand)
                exportCommand.RaiseCanExecuteChanged();
        }

        private void RefreshSnapshotsCollection()
        {
            Snapshots.Clear();

            foreach (var snapshot in _snapshots
                         .OrderByDescending(s => s.Timestamp)
                         .Select(PortfolioSnapshotItemViewModel.FromModel))
            {
                Snapshots.Add(snapshot);
            }
        }

        private void RefreshDerivedCollections()
        {
            var summary = _portfolioService.BuildSummary(_portfolio, _assets, _tradingService.Trades);
            Summary.UpdateFromModel(summary);

            var positions = _portfolioService.BuildPositionDisplays(_portfolio, _assets);
            Positions.Clear();
            foreach (var item in positions.Select(PositionDisplayItemViewModel.FromModel))
                Positions.Add(item);

            var stats = _portfolioService.BuildTradeStats(_tradingService.Trades, _portfolio.Positions);
            Stats.UpdateFromModel(stats);

            var analytics = _portfolioService.BuildTickerAnalytics(_tradingService.Trades, _portfolio.Positions);
            TickerAnalytics.Clear();
            foreach (var item in analytics.Select(TickerAnalyticsItemViewModel.FromModel))
                TickerAnalytics.Add(item);

            if (QuickBuy1Command is RelayCommand q1) q1.RaiseCanExecuteChanged();
            if (QuickBuy5Command is RelayCommand q5) q5.RaiseCanExecuteChanged();
            if (QuickBuy10Command is RelayCommand q10) q10.RaiseCanExecuteChanged();
            if (SellAllCommand is RelayCommand sellAll) sellAll.RaiseCanExecuteChanged();
        }

        private void CaptureSnapshot(string source)
        {
            var snapshot = _portfolioService.BuildSnapshot(
                _portfolio,
                _assets,
                _tradingService.Trades,
                source);

            _snapshots.Add(snapshot);

            int maxSnapshots = EnvService.GetInt("REAL_MARKET_MAX_SNAPSHOTS", 500);
            if (maxSnapshots <= 0)
                maxSnapshots = 500;

            if (_snapshots.Count > maxSnapshots)
            {
                int removeCount = _snapshots.Count - maxSnapshots;
                _snapshots.RemoveRange(0, removeCount);
            }

            RefreshSnapshotsCollection();
        }

        private void UpdateLastUpdateText()
        {
            LastUpdateText = _lastSuccessfulUpdateUtc.HasValue
                ? $"Последнее обновление: {_lastSuccessfulUpdateUtc.Value:dd.MM.yyyy HH:mm:ss}"
                : "Последнее обновление: —";
        }

        private bool CanTrade()
        {
            return SelectedAsset != null && TradeQuantity > 0;
        }

        private bool CanTradeWithSelectedAsset()
        {
            return SelectedAsset != null;
        }

        private bool CanSellAll()
        {
            if (SelectedAsset == null)
                return false;

            return _portfolio.Positions.Any(p => p.Ticker == SelectedAsset.Ticker && p.Quantity > 0);
        }

        private bool CanRemoveSelectedTicker()
        {
            return SelectedAsset != null;
        }

        private void UpdateTradeCommands()
        {
            if (BuyCommand is RelayCommand buyCommand) buyCommand.RaiseCanExecuteChanged();
            if (SellCommand is RelayCommand sellCommand) sellCommand.RaiseCanExecuteChanged();
            if (QuickBuy1Command is RelayCommand q1) q1.RaiseCanExecuteChanged();
            if (QuickBuy5Command is RelayCommand q5) q5.RaiseCanExecuteChanged();
            if (QuickBuy10Command is RelayCommand q10) q10.RaiseCanExecuteChanged();
            if (SellAllCommand is RelayCommand sellAll) sellAll.RaiseCanExecuteChanged();
            if (RemoveTickerCommand is AsyncRelayCommand removeTicker) removeTicker.RaiseCanExecuteChanged();
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }
    }
}
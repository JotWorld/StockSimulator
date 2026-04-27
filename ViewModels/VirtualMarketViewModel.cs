using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using StockExchangeSimulator.Data;
using StockExchangeSimulator.Enums;
using StockExchangeSimulator.Models;
using StockExchangeSimulator.Models.VirtualMarket;
using StockExchangeSimulator.Services;
using StockExchangeSimulator.Services.VirtualMarket;

namespace StockExchangeSimulator.ViewModels
{
    public class VirtualMarketViewModel : ViewModelBase
    {
        private readonly VirtualMarketEngine _engine = new();
        private readonly TradingService _tradingService = new();
        private readonly RealMarketPortfolioService _portfolioService = new();
        private readonly IDialogService _dialogService = new DialogService();
        private readonly DispatcherTimer _uiTimer;
        private readonly IVirtualMarketRepository _stateRepository =
            new SqliteVirtualMarketRepository(new VirtualMarketDbService());

        private readonly Portfolio _portfolio = new() { Balance = 10000m };
        private readonly List<Asset> _assets = new();
        private readonly List<PortfolioSnapshot> _snapshots = new();

        private AssetItemViewModel? _selectedAsset;
        private string _quantityText = "1";
        private string _assetFilterText = string.Empty;
        private string _status = "Виртуальный рынок готов.";
        private string _refreshStats = string.Empty;
        private string _lastUpdateText = "Последний тик: —";

        public ObservableCollection<AssetItemViewModel> Assets { get; } = new();
        public ObservableCollection<PositionDisplayItemViewModel> Positions { get; } = new();
        public ObservableCollection<TradeItemViewModel> Trades { get; } = new();
        public ObservableCollection<TickerAnalyticsItemViewModel> TickerAnalytics { get; } = new();
        public ObservableCollection<PortfolioSnapshotItemViewModel> Snapshots { get; } = new();
        public ObservableCollection<VirtualMarketNews> News { get; } = new();
        public ObservableCollection<VirtualMarketTrade> MarketTrades { get; } = new();

        public PortfolioSummaryViewModel Summary { get; } = new();
        public TradeStatsViewModel Stats { get; } = new();

        public IEnumerable<double> SpeedOptions { get; } = new[] { 0.5, 1, 2, 5, 10, 25 };
        public IEnumerable<VirtualMarketMode> MarketModeOptions { get; } = Enum.GetValues<VirtualMarketMode>();

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

        public int TradeQuantity => int.TryParse(QuantityText, out int value) && value > 0 ? value : 0;

        public string AssetFilterText
        {
            get => _assetFilterText;
            set
            {
                if (SetProperty(ref _assetFilterText, value))
                    RefreshAssetsCollection();
            }
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string RefreshStats
        {
            get => _refreshStats;
            set => SetProperty(ref _refreshStats, value);
        }

        public string LastUpdateText
        {
            get => _lastUpdateText;
            set => SetProperty(ref _lastUpdateText, value);
        }

        public bool IsRunning => _engine.IsRunning;
        public int Tick => _engine.Tick;
        public int BotCount => _engine.Bots.Count;
        public DateTime SimulatedTime => _engine.SimulatedTime;
        public double MarketSentiment => _engine.MarketSentiment;

        public double GameSpeed
        {
            get => _engine.GameSpeedMultiplier;
            set
            {
                if (value <= 0)
                    return;

                _engine.GameSpeedMultiplier = value;
                OnPropertyChanged();
                Status = $"Скорость симуляции: x{value:0.##}";
                SaveState();
            }
        }

        public VirtualMarketMode SelectedMarketMode
        {
            get => _engine.MarketMode;
            set
            {
                _engine.MarketMode = value;
                OnPropertyChanged();
                Status = $"Режим рынка: {value}";
                SaveState();
            }
        }

        public ICommand StartCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand StepCommand { get; }
        public ICommand ResetMarketCommand { get; }
        public ICommand BuyCommand { get; }
        public ICommand SellCommand { get; }
        public ICommand QuickBuy1Command { get; }
        public ICommand QuickBuy5Command { get; }
        public ICommand QuickBuy10Command { get; }
        public ICommand SellAllCommand { get; }
        public ICommand ResetPortfolioCommand { get; }
        public ICommand ExportTradesCommand { get; }

        public VirtualMarketViewModel()
        {
            StartCommand = new RelayCommand(StartMarket);
            PauseCommand = new RelayCommand(PauseMarket);
            StepCommand = new RelayCommand(StepMarket);
            ResetMarketCommand = new RelayCommand(ResetMarket);
            BuyCommand = new RelayCommand(() => ExecuteBuy(TradeQuantity), CanTrade);
            SellCommand = new RelayCommand(() => ExecuteSell(TradeQuantity), CanTrade);
            QuickBuy1Command = new RelayCommand(() => ExecuteBuy(1), CanTradeWithSelectedAsset);
            QuickBuy5Command = new RelayCommand(() => ExecuteBuy(5), CanTradeWithSelectedAsset);
            QuickBuy10Command = new RelayCommand(() => ExecuteBuy(10), CanTradeWithSelectedAsset);
            SellAllCommand = new RelayCommand(SellAll, CanSellAll);
            ResetPortfolioCommand = new RelayCommand(ResetPortfolio);
            ExportTradesCommand = new RelayCommand(ExportTrades, () => Trades.Count > 0);
            LoadState();

            _engine.MarketUpdated += RefreshFromEngine;

            _uiTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _uiTimer.Tick += (_, _) => _engine.Advance(_uiTimer.Interval);
            _uiTimer.Start();

            RefreshFromEngine();
            if (_snapshots.Count == 0)
                CaptureSnapshot("Initial");
            else
                RefreshSnapshotsCollection();
        }

        private void StartMarket()
        {
            _engine.Start();
            Status = "Симуляция запущена.";
            RefreshHeader();
        }

        private void PauseMarket()
        {
            _engine.Pause();
            Status = "Симуляция на паузе.";
            RefreshHeader();
        }

        private void StepMarket()
        {
            _engine.Step();
            Status = "Выполнен один тик симуляции.";
        }

        private void ResetMarket()
        {
            bool confirmed = _dialogService.Confirm(
                "Сбросить виртуальный рынок? Портфель игрока останется без изменений.",
                "Сброс рынка");

            if (!confirmed)
                return;

            _engine.Reset();
            Status = "Виртуальный рынок сброшен.";
            CaptureSnapshot("Market Reset");
            SaveState();
        }

        private void ExecuteBuy(int quantity)
        {
            if (SelectedAsset == null)
                return;

            var asset = SelectedAsset.ToModel();
            var result = _tradingService.ExecuteBuyAsset(_portfolio, asset, quantity, MarketMode.Virtual);

            if (!result.IsSuccess)
            {
                _dialogService.ShowWarning(result.Message);
                Status = result.Message;
                return;
            }

            _engine.ApplyPlayerBuy(asset.Ticker, quantity);
            RefreshTradesCollection();
            RefreshDerivedCollections();
            CaptureSnapshot("Buy");

            QuantityText = "1";
            Status = result.Message;
            SaveState();
        }

        private void ExecuteSell(int quantity)
        {
            if (SelectedAsset == null)
                return;

            var asset = SelectedAsset.ToModel();
            var result = _tradingService.ExecuteSellAsset(_portfolio, asset, quantity, MarketMode.Virtual);

            if (!result.IsSuccess)
            {
                _dialogService.ShowWarning(result.Message);
                Status = result.Message;
                return;
            }

            _engine.ApplyPlayerSell(asset.Ticker, quantity);
            RefreshTradesCollection();
            RefreshDerivedCollections();
            CaptureSnapshot("Sell");

            QuantityText = "1";
            Status = result.Message;
            SaveState();
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
                "Сбросить виртуальный портфель, сделки и снапшоты?",
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
            CaptureSnapshot("Portfolio Reset");

            Status = "Виртуальный портфель сброшен.";
            SaveState();
        }

        private void ExportTrades()
        {
            string? filePath = _dialogService.SaveFile(
                "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                $"virtual_market_trades_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

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

        private void LoadState()
        {
            try
            {
                var state = _stateRepository.LoadState();

                _portfolio.Balance = state.Balance;
                _portfolio.Positions.Clear();
                _portfolio.Positions.AddRange(state.Positions);

                _tradingService.SetTrades(state.Trades);

                _snapshots.Clear();
                _snapshots.AddRange(state.Snapshots);

                _engine.GameSpeedMultiplier = state.GameSpeed;
                _engine.MarketMode = state.MarketMode;

                Status = string.IsNullOrWhiteSpace(state.LastStatus)
                    ? "Состояние виртуального рынка загружено."
                    : state.LastStatus;
            }
            catch (Exception ex)
            {
                Status = $"Не удалось загрузить состояние виртуального рынка: {ex.Message}";
            }
        }

        private void SaveState()
        {
            try
            {
                _stateRepository.SaveState(new VirtualMarketState
                {
                    Version = 1,
                    Balance = _portfolio.Balance,
                    Positions = _portfolio.Positions.ToList(),
                    Trades = _tradingService.Trades
                        .Where(t => t.MarketMode == MarketMode.Virtual)
                        .OrderBy(t => t.Timestamp)
                        .ToList(),
                    Snapshots = _snapshots
                        .OrderBy(s => s.Timestamp)
                        .ToList(),
                    GameSpeed = _engine.GameSpeedMultiplier,
                    MarketMode = _engine.MarketMode,
                    LastStatus = Status
                });
            }
            catch (Exception ex)
            {
                Status = $"Не удалось сохранить состояние виртуального рынка: {ex.Message}";
            }
        }

        private void RefreshFromEngine()
        {
            _assets.Clear();
            _assets.AddRange(_engine.ToAssets());

            RefreshAssetsCollection();
            RefreshNewsCollection();
            RefreshMarketTradesCollection();
            RefreshDerivedCollections();
            RefreshHeader();

            LastUpdateText = $"Последний тик: {_engine.SimulatedTime:dd.MM.yyyy HH:mm:ss}";
            RefreshStats = $"Тик: {_engine.Tick} | Ботов: {_engine.Bots.Count} | Режим: {_engine.MarketMode} | Sentiment: {_engine.MarketSentiment:F2}";
        }

        private void RefreshAssetsCollection()
        {
            string filter = (AssetFilterText ?? string.Empty).Trim();
            string? selectedTicker = SelectedAsset?.Ticker;

            var filtered = _assets
                .Where(a => string.IsNullOrWhiteSpace(filter)
                            || a.Ticker.Contains(filter, StringComparison.OrdinalIgnoreCase)
                            || a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Ticker)
                .Select(AssetItemViewModel.FromModel)
                .ToList();

            Assets.Clear();
            foreach (var item in filtered)
                Assets.Add(item);

            if (!string.IsNullOrWhiteSpace(selectedTicker))
                SelectedAsset = Assets.FirstOrDefault(a => a.Ticker == selectedTicker);

            SelectedAsset ??= Assets.FirstOrDefault();
        }

        private void RefreshNewsCollection()
        {
            News.Clear();
            foreach (var item in _engine.News.Take(40))
                News.Add(item);
        }

        private void RefreshMarketTradesCollection()
        {
            MarketTrades.Clear();
            foreach (var item in _engine.MarketTrades.Take(100))
                MarketTrades.Add(item);
        }

        private void RefreshTradesCollection()
        {
            Trades.Clear();
            foreach (var trade in _tradingService.Trades
                         .Where(t => t.MarketMode == MarketMode.Virtual)
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
            var virtualTrades = _tradingService.Trades.Where(t => t.MarketMode == MarketMode.Virtual).ToList();

            Summary.UpdateFromModel(BuildSummary(virtualTrades));

            var positions = _portfolioService.BuildPositionDisplays(_portfolio, _assets);
            Positions.Clear();
            foreach (var item in positions.Select(PositionDisplayItemViewModel.FromModel))
                Positions.Add(item);

            Stats.UpdateFromModel(BuildTradeStats(virtualTrades));

            var analytics = BuildTickerAnalytics(virtualTrades);
            TickerAnalytics.Clear();
            foreach (var item in analytics.Select(TickerAnalyticsItemViewModel.FromModel))
                TickerAnalytics.Add(item);

            UpdateTradeCommands();
        }

        private PortfolioSummary BuildSummary(IEnumerable<Trade> virtualTrades)
        {
            var assetMap = _assets.GroupBy(a => a.Ticker).ToDictionary(g => g.Key, g => g.First());
            decimal positionsValue = 0m;
            decimal totalCost = 0m;

            foreach (var position in _portfolio.Positions)
            {
                totalCost += position.AveragePrice * position.Quantity;
                if (assetMap.TryGetValue(position.Ticker, out var asset))
                    positionsValue += asset.CurrentPrice * position.Quantity;
            }

            return new PortfolioSummary
            {
                Balance = _portfolio.Balance,
                PositionsValue = positionsValue,
                TotalValue = _portfolio.Balance + positionsValue,
                TotalCost = totalCost,
                UnrealizedPnL = positionsValue - totalCost,
                RealizedPnL = virtualTrades.Where(t => t.TradeType == TradeType.Sell).Sum(t => t.RealizedPnL),
                TotalFees = virtualTrades.Sum(t => t.Fee)
            };
        }

        private TradeStats BuildTradeStats(List<Trade> virtualTrades)
        {
            var sellTrades = virtualTrades.Where(t => t.TradeType == TradeType.Sell).ToList();
            int winningSellTrades = sellTrades.Count(t => t.RealizedPnL > 0m);
            int losingSellTrades = sellTrades.Count(t => t.RealizedPnL < 0m);
            decimal grossProfit = sellTrades.Where(t => t.RealizedPnL > 0m).Sum(t => t.RealizedPnL);
            decimal grossLossAbs = Math.Abs(sellTrades.Where(t => t.RealizedPnL < 0m).Sum(t => t.RealizedPnL));

            var tickerAnalytics = BuildTickerAnalytics(virtualTrades);
            var bestTicker = tickerAnalytics.OrderByDescending(t => t.RealizedPnL).FirstOrDefault();
            var worstTicker = tickerAnalytics.OrderBy(t => t.RealizedPnL).FirstOrDefault();

            return new TradeStats
            {
                TotalTrades = virtualTrades.Count,
                BuyTrades = virtualTrades.Count(t => t.TradeType == TradeType.Buy),
                SellTrades = sellTrades.Count,
                TotalBuyVolume = virtualTrades.Where(t => t.TradeType == TradeType.Buy).Sum(t => t.TotalAmount),
                TotalSellVolume = virtualTrades.Where(t => t.TradeType == TradeType.Sell).Sum(t => t.TotalAmount),
                TotalFees = virtualTrades.Sum(t => t.Fee),
                TotalRealizedPnL = sellTrades.Sum(t => t.RealizedPnL),
                WinningSellTrades = winningSellTrades,
                LosingSellTrades = losingSellTrades,
                WinRatePercent = sellTrades.Count == 0 ? 0m : (decimal)winningSellTrades / sellTrades.Count * 100m,
                BestSellPnL = sellTrades.Any() ? sellTrades.Max(t => t.RealizedPnL) : 0m,
                WorstSellPnL = sellTrades.Any() ? sellTrades.Min(t => t.RealizedPnL) : 0m,
                HasSellTrades = sellTrades.Any(),
                AverageWin = winningSellTrades == 0 ? 0m : sellTrades.Where(t => t.RealizedPnL > 0m).Average(t => t.RealizedPnL),
                AverageLoss = losingSellTrades == 0 ? 0m : sellTrades.Where(t => t.RealizedPnL < 0m).Average(t => t.RealizedPnL),
                ProfitFactor = grossLossAbs == 0m ? grossProfit : grossProfit / grossLossAbs,
                AverageFee = virtualTrades.Count == 0 ? 0m : virtualTrades.Average(t => t.Fee),
                BestTicker = bestTicker?.Ticker ?? "-",
                WorstTicker = worstTicker?.Ticker ?? "-",
                BestTickerPnL = bestTicker?.RealizedPnL ?? 0m,
                WorstTickerPnL = worstTicker?.RealizedPnL ?? 0m
            };
        }

        private List<TickerAnalytics> BuildTickerAnalytics(List<Trade> virtualTrades)
        {
            var openQuantityMap = _portfolio.Positions
                .GroupBy(p => p.Ticker)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var analytics = virtualTrades
                .GroupBy(t => t.Ticker)
                .Select(g => new TickerAnalytics
                {
                    Ticker = g.Key,
                    BuyTrades = g.Count(t => t.TradeType == TradeType.Buy),
                    SellTrades = g.Count(t => t.TradeType == TradeType.Sell),
                    BoughtQuantity = g.Where(t => t.TradeType == TradeType.Buy).Sum(t => t.Quantity),
                    SoldQuantity = g.Where(t => t.TradeType == TradeType.Sell).Sum(t => t.Quantity),
                    BuyVolume = g.Where(t => t.TradeType == TradeType.Buy).Sum(t => t.TotalAmount),
                    SellVolume = g.Where(t => t.TradeType == TradeType.Sell).Sum(t => t.TotalAmount),
                    Fees = g.Sum(t => t.Fee),
                    RealizedPnL = g.Where(t => t.TradeType == TradeType.Sell).Sum(t => t.RealizedPnL),
                    OpenQuantity = openQuantityMap.TryGetValue(g.Key, out int qty) ? qty : 0
                })
                .ToList();

            foreach (var position in _portfolio.Positions)
            {
                if (!analytics.Any(t => t.Ticker == position.Ticker))
                {
                    analytics.Add(new TickerAnalytics
                    {
                        Ticker = position.Ticker,
                        OpenQuantity = position.Quantity
                    });
                }
            }

            return analytics
                .OrderByDescending(t => t.RealizedPnL)
                .ThenBy(t => t.Ticker)
                .ToList();
        }

        private void CaptureSnapshot(string source)
        {
            var summary = BuildSummary(_tradingService.Trades.Where(t => t.MarketMode == MarketMode.Virtual));
            _snapshots.Add(new PortfolioSnapshot
            {
                Timestamp = DateTime.Now,
                Source = source,
                Balance = summary.Balance,
                PositionsValue = summary.PositionsValue,
                TotalValue = summary.TotalValue,
                UnrealizedPnL = summary.UnrealizedPnL,
                RealizedPnL = summary.RealizedPnL,
                TotalPnL = summary.TotalPnL,
                TotalFees = summary.TotalFees
            });

            if (_snapshots.Count > 500)
                _snapshots.RemoveRange(0, _snapshots.Count - 500);

            RefreshSnapshotsCollection();
        }

        private void RefreshHeader()
        {
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(Tick));
            OnPropertyChanged(nameof(BotCount));
            OnPropertyChanged(nameof(SimulatedTime));
            OnPropertyChanged(nameof(MarketSentiment));
            OnPropertyChanged(nameof(SelectedMarketMode));
            OnPropertyChanged(nameof(GameSpeed));
        }

        private bool CanTrade() => SelectedAsset != null && TradeQuantity > 0;
        private bool CanTradeWithSelectedAsset() => SelectedAsset != null;

        private bool CanSellAll()
        {
            return SelectedAsset != null && _portfolio.Positions.Any(p => p.Ticker == SelectedAsset.Ticker && p.Quantity > 0);
        }

        private void UpdateTradeCommands()
        {
            if (BuyCommand is RelayCommand buyCommand) buyCommand.RaiseCanExecuteChanged();
            if (SellCommand is RelayCommand sellCommand) sellCommand.RaiseCanExecuteChanged();
            if (QuickBuy1Command is RelayCommand q1) q1.RaiseCanExecuteChanged();
            if (QuickBuy5Command is RelayCommand q5) q5.RaiseCanExecuteChanged();
            if (QuickBuy10Command is RelayCommand q10) q10.RaiseCanExecuteChanged();
            if (SellAllCommand is RelayCommand sellAll) sellAll.RaiseCanExecuteChanged();
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
                return $"\"{value.Replace("\"", "\"\"")}\"";

            return value;
        }
    }
}

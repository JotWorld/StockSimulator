using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.Services
{
    public class RealMarketStateService
    {
        private readonly string _filePath;
        private readonly string _backupFilePath;

        public RealMarketStateService()
        {
            string appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StockExchangeSimulator");

            Directory.CreateDirectory(appFolder);

            _filePath = Path.Combine(appFolder, "real_market_state.json");
            _backupFilePath = Path.Combine(appFolder, "real_market_state.backup.json");
        }

        public void Save(RealMarketState state)
        {
            try
            {
                Normalize(state);

                if (File.Exists(_filePath))
                    File.Copy(_filePath, _backupFilePath, true);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(state, options);
                File.WriteAllText(_filePath, json);
            }
            catch
            {
            }
        }

        public RealMarketState Load()
        {
            var defaultState = CreateDefaultState();

            try
            {
                if (!File.Exists(_filePath))
                    return defaultState;

                string json = File.ReadAllText(_filePath);
                var state = JsonSerializer.Deserialize<RealMarketState>(json);

                if (state == null)
                    return defaultState;

                Normalize(state);
                return state;
            }
            catch
            {
                try
                {
                    if (!File.Exists(_backupFilePath))
                        return defaultState;

                    string json = File.ReadAllText(_backupFilePath);
                    var state = JsonSerializer.Deserialize<RealMarketState>(json);

                    if (state == null)
                        return defaultState;

                    Normalize(state);
                    return state;
                }
                catch
                {
                    return defaultState;
                }
            }
        }

        private static void Normalize(RealMarketState state)
        {
            state.Version = Math.Max(state.Version, 3);

            state.Positions ??= new();
            state.Trades ??= new();
            state.TrackedTickers ??= new();
            state.Settings ??= new RealMarketSettings();
            state.LastRefreshStatus ??= string.Empty;
            state.Snapshots ??= new();

            state.Positions = state.Positions
                .Where(p => !string.IsNullOrWhiteSpace(p.Ticker) && p.Quantity > 0)
                .Select(p => new Position
                {
                    Ticker = p.Ticker.Trim().ToUpperInvariant(),
                    Quantity = p.Quantity,
                    AveragePrice = p.AveragePrice < 0 ? 0 : p.AveragePrice
                })
                .ToList();

            state.TrackedTickers = state.TrackedTickers
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToUpperInvariant())
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            int maxSnapshots = EnvService.GetInt("REAL_MARKET_MAX_SNAPSHOTS", 500);
            if (maxSnapshots <= 0)
                maxSnapshots = 500;

            state.Snapshots = state.Snapshots
                .OrderByDescending(s => s.Timestamp)
                .Take(maxSnapshots)
                .OrderBy(s => s.Timestamp)
                .ToList();

            if (state.Balance <= 0 && state.Positions.Count == 0 && state.Trades.Count == 0)
                state.Balance = 10000m;

            if (state.TrackedTickers.Count == 0)
            {
                state.TrackedTickers = new()
                {
                    "AAPL",
                    "MSFT",
                    "NVDA",
                    "TSLA"
                };
            }

            if (state.Settings.RefreshIntervalSeconds <= 0)
            {
                state.Settings.RefreshIntervalSeconds = EnvService.GetInt("DEFAULT_REFRESH_INTERVAL_SECONDS", 15);
            }
        }

        private static RealMarketState CreateDefaultState()
        {
            return new RealMarketState
            {
                Version = 3,
                Balance = 10000m,
                TrackedTickers = new()
                {
                    "AAPL",
                    "MSFT",
                    "NVDA",
                    "TSLA"
                },
                Settings = new RealMarketSettings
                {
                    AutoUpdateEnabled = EnvService.GetBool("DEFAULT_AUTO_UPDATE", true),
                    RefreshIntervalSeconds = EnvService.GetInt("DEFAULT_REFRESH_INTERVAL_SECONDS", 15)
                },
                LastRefreshStatus = "Состояние загружено"
            };
        }
    }
}
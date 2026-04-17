using System;
using System.IO;
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
                if (File.Exists(_filePath))
                {
                    File.Copy(_filePath, _backupFilePath, true);
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(state, options);
                File.WriteAllText(_filePath, json);
            }
            catch
            {
                // пока молча игнорируем, позже можно добавить логирование
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
            state.Positions ??= new();
            state.Trades ??= new();
            state.TrackedTickers ??= new();
            state.Settings ??= new RealMarketSettings();

            if (state.Balance <= 0 && state.Positions.Count == 0 && state.Trades.Count == 0)
                state.Balance = 10000m;

            if (state.TrackedTickers.Count == 0)
            {
                state.TrackedTickers = new()
                {
                    "AAPL",
                    "MSFT",
                    "TSLA",
                    "NVDA"
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
                Balance = 10000m,
                TrackedTickers = new()
                {
                    "AAPL",
                    "MSFT",
                    "TSLA",
                    "NVDA"
                },
                Settings = new RealMarketSettings
                {
                    AutoUpdateEnabled = EnvService.GetBool("DEFAULT_AUTO_UPDATE", true),
                    RefreshIntervalSeconds = EnvService.GetInt("DEFAULT_REFRESH_INTERVAL_SECONDS", 15)
                }
            };
        }
    }
}
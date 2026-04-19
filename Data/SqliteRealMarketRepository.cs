using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using StockExchangeSimulator.Enums;
using StockExchangeSimulator.Models;
using StockExchangeSimulator.Services;

namespace StockExchangeSimulator.Data
{
    public class SqliteRealMarketRepository : IRealMarketRepository
    {
        private readonly RealMarketDbService _dbService;

        public SqliteRealMarketRepository(RealMarketDbService dbService)
        {
            _dbService = dbService;
            TryMigrateFromJsonIfNeeded();
        }

        public RealMarketState LoadState()
        {
            using var connection = _dbService.CreateConnection();
            connection.Open();

            var state = new RealMarketState
            {
                Version = 3,
                Balance = GetDecimalAppState(connection, "Balance", 10000m),
                LastRefreshStatus = GetAppState(connection, "LastRefreshStatus", "Состояние загружено"),
                LastSuccessfulUpdateUtc = GetNullableDateTimeAppState(connection, "LastSuccessfulUpdateUtc"),
                Settings = LoadSettings(connection),
                TrackedTickers = LoadTrackedTickers(connection),
                Positions = LoadPositions(connection),
                Trades = LoadTrades(connection),
                Snapshots = LoadSnapshots(connection)
            };

            Normalize(state);
            return state;
        }

        public void SaveState(RealMarketState state)
        {
            Normalize(state);

            using var connection = _dbService.CreateConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            SaveAppState(connection, transaction, "Balance", ToInvariant(state.Balance));
            SaveAppState(connection, transaction, "LastRefreshStatus", state.LastRefreshStatus ?? string.Empty);
            SaveAppState(connection, transaction, "LastSuccessfulUpdateUtc",
                state.LastSuccessfulUpdateUtc?.ToString("O") ?? string.Empty);

            SaveSettings(connection, transaction, state.Settings);
            SaveTrackedTickers(connection, transaction, state.TrackedTickers);
            SavePositions(connection, transaction, state.Positions);
            SaveTrades(connection, transaction, state.Trades);
            SaveSnapshots(connection, transaction, state.Snapshots);

            transaction.Commit();
        }

        private void TryMigrateFromJsonIfNeeded()
        {
            using var connection = _dbService.CreateConnection();
            connection.Open();

            using var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM Trades;";
            long tradesCount = (long)(countCommand.ExecuteScalar() ?? 0L);

            using var tickerCommand = connection.CreateCommand();
            tickerCommand.CommandText = "SELECT COUNT(*) FROM TrackedTickers;";
            long tickerCount = (long)(tickerCommand.ExecuteScalar() ?? 0L);

            if (tradesCount > 0 || tickerCount > 0)
                return;

            var jsonStateService = new RealMarketStateService();
            var state = jsonStateService.Load();

            SaveState(state);
        }

        private static void Normalize(RealMarketState state)
        {
            state.Version = Math.Max(state.Version, 3);
            state.Positions ??= new();
            state.Trades ??= new();
            state.TrackedTickers ??= new();
            state.Settings ??= new RealMarketSettings();
            state.Snapshots ??= new();
            state.LastRefreshStatus ??= string.Empty;

            if (state.Balance <= 0 && state.Positions.Count == 0 && state.Trades.Count == 0)
                state.Balance = 10000m;

            if (state.TrackedTickers.Count == 0)
            {
                state.TrackedTickers = new List<string> { "AAPL", "MSFT", "NVDA", "TSLA" };
            }

            if (state.Settings.RefreshIntervalSeconds <= 0)
            {
                state.Settings.RefreshIntervalSeconds = EnvService.GetInt("DEFAULT_REFRESH_INTERVAL_SECONDS", 15);
            }

            state.TrackedTickers = state.TrackedTickers
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToUpperInvariant())
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            state.Positions = state.Positions
                .Where(p => !string.IsNullOrWhiteSpace(p.Ticker) && p.Quantity > 0)
                .Select(p => new Position
                {
                    Ticker = p.Ticker.Trim().ToUpperInvariant(),
                    Quantity = p.Quantity,
                    AveragePrice = p.AveragePrice
                })
                .ToList();

            int maxSnapshots = EnvService.GetInt("REAL_MARKET_MAX_SNAPSHOTS", 500);
            if (maxSnapshots <= 0)
                maxSnapshots = 500;

            state.Snapshots = state.Snapshots
                .OrderByDescending(s => s.Timestamp)
                .Take(maxSnapshots)
                .OrderBy(s => s.Timestamp)
                .ToList();
        }

        private static RealMarketSettings LoadSettings(SqliteConnection connection)
        {
            var settings = new RealMarketSettings
            {
                AutoUpdateEnabled = EnvService.GetBool("DEFAULT_AUTO_UPDATE", true),
                RefreshIntervalSeconds = EnvService.GetInt("DEFAULT_REFRESH_INTERVAL_SECONDS", 15)
            };

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Key, Value FROM Settings;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string key = reader.GetString(0);
                string value = reader.GetString(1);

                switch (key)
                {
                    case "AutoUpdateEnabled":
                        if (bool.TryParse(value, out bool autoUpdate))
                            settings.AutoUpdateEnabled = autoUpdate;
                        break;

                    case "RefreshIntervalSeconds":
                        if (int.TryParse(value, out int seconds))
                            settings.RefreshIntervalSeconds = seconds;
                        break;
                }
            }

            return settings;
        }

        private static List<string> LoadTrackedTickers(SqliteConnection connection)
        {
            var result = new List<string>();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Ticker FROM TrackedTickers ORDER BY Ticker;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(reader.GetString(0));
            }

            return result;
        }

        private static List<Position> LoadPositions(SqliteConnection connection)
        {
            var result = new List<Position>();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Ticker, Quantity, AveragePrice FROM Positions ORDER BY Ticker;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Position
                {
                    Ticker = reader.GetString(0),
                    Quantity = reader.GetInt32(1),
                    AveragePrice = ParseDecimal(reader.GetString(2))
                });
            }

            return result;
        }

        private static List<Trade> LoadTrades(SqliteConnection connection)
        {
            var result = new List<Trade>();

            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT Timestamp, Ticker, TradeType, Quantity, Price, Fee, RealizedPnL, MarketMode
FROM Trades
ORDER BY Timestamp;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Trade
                {
                    Timestamp = DateTime.Parse(reader.GetString(0), null, DateTimeStyles.RoundtripKind),
                    Ticker = reader.GetString(1),
                    TradeType = (TradeType)reader.GetInt32(2),
                    Quantity = reader.GetInt32(3),
                    Price = ParseDecimal(reader.GetString(4)),
                    Fee = ParseDecimal(reader.GetString(5)),
                    RealizedPnL = ParseDecimal(reader.GetString(6)),
                    MarketMode = (MarketMode)reader.GetInt32(7)
                });
            }

            return result;
        }

        private static List<PortfolioSnapshot> LoadSnapshots(SqliteConnection connection)
        {
            var result = new List<PortfolioSnapshot>();

            using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT Timestamp, Source, Balance, PositionsValue, TotalValue, UnrealizedPnL, RealizedPnL, TotalPnL, TotalFees
FROM Snapshots
ORDER BY Timestamp;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new PortfolioSnapshot
                {
                    Timestamp = DateTime.Parse(reader.GetString(0), null, DateTimeStyles.RoundtripKind),
                    Source = reader.GetString(1),
                    Balance = ParseDecimal(reader.GetString(2)),
                    PositionsValue = ParseDecimal(reader.GetString(3)),
                    TotalValue = ParseDecimal(reader.GetString(4)),
                    UnrealizedPnL = ParseDecimal(reader.GetString(5)),
                    RealizedPnL = ParseDecimal(reader.GetString(6)),
                    TotalPnL = ParseDecimal(reader.GetString(7)),
                    TotalFees = ParseDecimal(reader.GetString(8))
                });
            }

            return result;
        }

        private static void SaveSettings(SqliteConnection connection, SqliteTransaction transaction, RealMarketSettings settings)
        {
            ExecuteNonQuery(connection, transaction, "DELETE FROM Settings;");

            InsertKeyValue(connection, transaction, "Settings", "AutoUpdateEnabled", settings.AutoUpdateEnabled.ToString());
            InsertKeyValue(connection, transaction, "Settings", "RefreshIntervalSeconds", settings.RefreshIntervalSeconds.ToString());
        }

        private static void SaveTrackedTickers(SqliteConnection connection, SqliteTransaction transaction, IEnumerable<string> tickers)
        {
            ExecuteNonQuery(connection, transaction, "DELETE FROM TrackedTickers;");

            foreach (var ticker in tickers)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "INSERT INTO TrackedTickers (Ticker) VALUES (@Ticker);";
                command.Parameters.AddWithValue("@Ticker", ticker);
                command.ExecuteNonQuery();
            }
        }

        private static void SavePositions(SqliteConnection connection, SqliteTransaction transaction, IEnumerable<Position> positions)
        {
            ExecuteNonQuery(connection, transaction, "DELETE FROM Positions;");

            foreach (var position in positions)
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO Positions (Ticker, Quantity, AveragePrice)
VALUES (@Ticker, @Quantity, @AveragePrice);";
                command.Parameters.AddWithValue("@Ticker", position.Ticker);
                command.Parameters.AddWithValue("@Quantity", position.Quantity);
                command.Parameters.AddWithValue("@AveragePrice", ToInvariant(position.AveragePrice));
                command.ExecuteNonQuery();
            }
        }

        private static void SaveTrades(SqliteConnection connection, SqliteTransaction transaction, IEnumerable<Trade> trades)
        {
            ExecuteNonQuery(connection, transaction, "DELETE FROM Trades;");

            foreach (var trade in trades.OrderBy(t => t.Timestamp))
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO Trades
(Timestamp, Ticker, TradeType, Quantity, Price, Fee, RealizedPnL, MarketMode)
VALUES
(@Timestamp, @Ticker, @TradeType, @Quantity, @Price, @Fee, @RealizedPnL, @MarketMode);";

                command.Parameters.AddWithValue("@Timestamp", trade.Timestamp.ToString("O"));
                command.Parameters.AddWithValue("@Ticker", trade.Ticker);
                command.Parameters.AddWithValue("@TradeType", (int)trade.TradeType);
                command.Parameters.AddWithValue("@Quantity", trade.Quantity);
                command.Parameters.AddWithValue("@Price", ToInvariant(trade.Price));
                command.Parameters.AddWithValue("@Fee", ToInvariant(trade.Fee));
                command.Parameters.AddWithValue("@RealizedPnL", ToInvariant(trade.RealizedPnL));
                command.Parameters.AddWithValue("@MarketMode", (int)trade.MarketMode);
                command.ExecuteNonQuery();
            }
        }

        private static void SaveSnapshots(SqliteConnection connection, SqliteTransaction transaction, IEnumerable<PortfolioSnapshot> snapshots)
        {
            ExecuteNonQuery(connection, transaction, "DELETE FROM Snapshots;");

            foreach (var snapshot in snapshots.OrderBy(s => s.Timestamp))
            {
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = @"
INSERT INTO Snapshots
(Timestamp, Source, Balance, PositionsValue, TotalValue, UnrealizedPnL, RealizedPnL, TotalPnL, TotalFees)
VALUES
(@Timestamp, @Source, @Balance, @PositionsValue, @TotalValue, @UnrealizedPnL, @RealizedPnL, @TotalPnL, @TotalFees);";

                command.Parameters.AddWithValue("@Timestamp", snapshot.Timestamp.ToString("O"));
                command.Parameters.AddWithValue("@Source", snapshot.Source);
                command.Parameters.AddWithValue("@Balance", ToInvariant(snapshot.Balance));
                command.Parameters.AddWithValue("@PositionsValue", ToInvariant(snapshot.PositionsValue));
                command.Parameters.AddWithValue("@TotalValue", ToInvariant(snapshot.TotalValue));
                command.Parameters.AddWithValue("@UnrealizedPnL", ToInvariant(snapshot.UnrealizedPnL));
                command.Parameters.AddWithValue("@RealizedPnL", ToInvariant(snapshot.RealizedPnL));
                command.Parameters.AddWithValue("@TotalPnL", ToInvariant(snapshot.TotalPnL));
                command.Parameters.AddWithValue("@TotalFees", ToInvariant(snapshot.TotalFees));
                command.ExecuteNonQuery();
            }
        }

        private static void SaveAppState(SqliteConnection connection, SqliteTransaction transaction, string key, string value)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
INSERT INTO AppState (Key, Value)
VALUES (@Key, @Value)
ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;";
            command.Parameters.AddWithValue("@Key", key);
            command.Parameters.AddWithValue("@Value", value);
            command.ExecuteNonQuery();
        }

        private static string GetAppState(SqliteConnection connection, string key, string defaultValue)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM AppState WHERE Key = @Key;";
            command.Parameters.AddWithValue("@Key", key);

            object? result = command.ExecuteScalar();
            return result?.ToString() ?? defaultValue;
        }

        private static decimal GetDecimalAppState(SqliteConnection connection, string key, decimal defaultValue)
        {
            string value = GetAppState(connection, key, ToInvariant(defaultValue));
            return ParseDecimal(value);
        }

        private static DateTime? GetNullableDateTimeAppState(SqliteConnection connection, string key)
        {
            string value = GetAppState(connection, key, string.Empty);

            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out DateTime parsed))
                return parsed;

            return null;
        }

        private static void InsertKeyValue(SqliteConnection connection, SqliteTransaction transaction, string table, string key, string value)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"INSERT INTO {table} (Key, Value) VALUES (@Key, @Value);";
            command.Parameters.AddWithValue("@Key", key);
            command.Parameters.AddWithValue("@Value", value);
            command.ExecuteNonQuery();
        }

        private static void ExecuteNonQuery(SqliteConnection connection, SqliteTransaction transaction, string sql)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        private static decimal ParseDecimal(string value)
        {
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
                return parsed;

            return 0m;
        }

        private static string ToInvariant(decimal value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Data.Sqlite;
using StockExchangeSimulator.Enums;
using StockExchangeSimulator.Models;
using StockExchangeSimulator.Models.VirtualMarket;

namespace StockExchangeSimulator.Data
{
    public class SqliteVirtualMarketRepository : IVirtualMarketRepository
    {
        private readonly VirtualMarketDbService _dbService;

        public SqliteVirtualMarketRepository(VirtualMarketDbService dbService)
        {
            _dbService = dbService;
        }

        public VirtualMarketState LoadState()
        {
            using var connection = _dbService.CreateConnection();
            connection.Open();

            var state = new VirtualMarketState
            {
                Version = 1,
                Balance = GetDecimalAppState(connection, "Balance", 10000m),
                GameSpeed = GetDoubleAppState(connection, "GameSpeed", 1.0),
                MarketMode = GetMarketModeAppState(connection, "MarketMode", VirtualMarketMode.Normal),
                LastStatus = GetAppState(connection, "LastStatus", "Состояние виртуального рынка загружено."),
                LastSavedUtc = GetNullableDateTimeAppState(connection, "LastSavedUtc"),
                Positions = LoadPositions(connection),
                Trades = LoadTrades(connection),
                Snapshots = LoadSnapshots(connection)
            };

            Normalize(state);
            return state;
        }

        public void SaveState(VirtualMarketState state)
        {
            Normalize(state);
            state.LastSavedUtc = DateTime.UtcNow;

            using var connection = _dbService.CreateConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();

            SaveAppState(connection, transaction, "Balance", ToInvariant(state.Balance));
            SaveAppState(connection, transaction, "GameSpeed", state.GameSpeed.ToString(CultureInfo.InvariantCulture));
            SaveAppState(connection, transaction, "MarketMode", ((int)state.MarketMode).ToString(CultureInfo.InvariantCulture));
            SaveAppState(connection, transaction, "LastStatus", state.LastStatus ?? string.Empty);
            SaveAppState(connection, transaction, "LastSavedUtc", state.LastSavedUtc?.ToString("O") ?? string.Empty);

            SavePositions(connection, transaction, state.Positions);
            SaveTrades(connection, transaction, state.Trades);
            SaveSnapshots(connection, transaction, state.Snapshots);

            transaction.Commit();
        }

        private static void Normalize(VirtualMarketState state)
        {
            state.Version = Math.Max(state.Version, 1);
            state.Positions ??= new();
            state.Trades ??= new();
            state.Snapshots ??= new();
            state.LastStatus ??= string.Empty;

            if (state.Balance <= 0 && state.Positions.Count == 0 && state.Trades.Count == 0)
                state.Balance = 10000m;

            if (state.GameSpeed <= 0)
                state.GameSpeed = 1.0;

            state.Positions = state.Positions
                .Where(p => !string.IsNullOrWhiteSpace(p.Ticker) && p.Quantity > 0)
                .Select(p => new Position
                {
                    Ticker = p.Ticker.Trim().ToUpperInvariant(),
                    Quantity = p.Quantity,
                    AveragePrice = p.AveragePrice
                })
                .OrderBy(p => p.Ticker)
                .ToList();

            state.Trades = state.Trades
                .Where(t => !string.IsNullOrWhiteSpace(t.Ticker) && t.MarketMode == MarketMode.Virtual)
                .OrderBy(t => t.Timestamp)
                .ToList();

            state.Snapshots = state.Snapshots
                .OrderByDescending(s => s.Timestamp)
                .Take(500)
                .OrderBy(s => s.Timestamp)
                .ToList();
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
            return ParseDecimal(GetAppState(connection, key, ToInvariant(defaultValue)));
        }

        private static double GetDoubleAppState(SqliteConnection connection, string key, double defaultValue)
        {
            string value = GetAppState(connection, key, defaultValue.ToString(CultureInfo.InvariantCulture));
            return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed) ? parsed : defaultValue;
        }

        private static VirtualMarketMode GetMarketModeAppState(SqliteConnection connection, string key, VirtualMarketMode defaultValue)
        {
            string value = GetAppState(connection, key, ((int)defaultValue).ToString(CultureInfo.InvariantCulture));
            return int.TryParse(value, out int parsed) && Enum.IsDefined(typeof(VirtualMarketMode), parsed)
                ? (VirtualMarketMode)parsed
                : defaultValue;
        }

        private static DateTime? GetNullableDateTimeAppState(SqliteConnection connection, string key)
        {
            string value = GetAppState(connection, key, string.Empty);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out DateTime parsed) ? parsed : null;
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
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed) ? parsed : 0m;
        }

        private static string ToInvariant(decimal value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

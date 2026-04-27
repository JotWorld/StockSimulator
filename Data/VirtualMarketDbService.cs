using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace StockExchangeSimulator.Data
{
    public class VirtualMarketDbService
    {
        public string DbPath { get; }
        public string ConnectionString => $"Data Source={DbPath}";

        public VirtualMarketDbService()
        {
            string appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StockExchangeSimulator");

            Directory.CreateDirectory(appFolder);
            DbPath = Path.Combine(appFolder, "virtual_market.db");

            Initialize();
        }

        public SqliteConnection CreateConnection()
        {
            return new SqliteConnection(ConnectionString);
        }

        private void Initialize()
        {
            using var connection = CreateConnection();
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE IF NOT EXISTS AppState (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Positions (
    Ticker TEXT PRIMARY KEY,
    Quantity INTEGER NOT NULL,
    AveragePrice TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Trades (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp TEXT NOT NULL,
    Ticker TEXT NOT NULL,
    TradeType INTEGER NOT NULL,
    Quantity INTEGER NOT NULL,
    Price TEXT NOT NULL,
    Fee TEXT NOT NULL,
    RealizedPnL TEXT NOT NULL,
    MarketMode INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS Snapshots (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Timestamp TEXT NOT NULL,
    Source TEXT NOT NULL,
    Balance TEXT NOT NULL,
    PositionsValue TEXT NOT NULL,
    TotalValue TEXT NOT NULL,
    UnrealizedPnL TEXT NOT NULL,
    RealizedPnL TEXT NOT NULL,
    TotalPnL TEXT NOT NULL,
    TotalFees TEXT NOT NULL
);";
            command.ExecuteNonQuery();
        }
    }
}

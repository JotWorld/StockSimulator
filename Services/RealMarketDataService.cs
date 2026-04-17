using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using StockExchangeSimulator.Models;

namespace StockExchangeSimulator.Services
{
    public class RealMarketDataService
    {
        private static readonly HttpClient _httpClient = new();
        private readonly string _apiKey;

        public RealMarketDataService()
        {
            _apiKey = EnvService.Get("FINNHUB_API_KEY");
        }

        public async Task<List<Asset>> GetAssetsAsync(IEnumerable<string> tickers)
        {
            var result = new List<Asset>();

            if (string.IsNullOrWhiteSpace(_apiKey))
                return result;

            foreach (var ticker in tickers
                         .Where(t => !string.IsNullOrWhiteSpace(t))
                         .Select(t => t.Trim().ToUpper())
                         .Distinct())
            {
                var asset = await GetAssetAsync(ticker);
                if (asset != null)
                    result.Add(asset);
            }

            return result;
        }

        public async Task<bool> IsValidTickerAsync(string ticker)
        {
            var asset = await GetAssetAsync(ticker);
            return asset != null;
        }

        private async Task<Asset?> GetAssetAsync(string ticker)
        {
            try
            {
                string url = $"https://finnhub.io/api/v1/quote?symbol={ticker}&token={_apiKey}";
                string response = await _httpClient.GetStringAsync(url);

                var quote = JsonSerializer.Deserialize<FinnhubQuote>(response);

                if (quote == null || quote.c <= 0)
                    return null;

                return new Asset
                {
                    Ticker = ticker,
                    Name = ticker,
                    CurrentPrice = quote.c,
                    Change = quote.d,
                    ChangePercent = quote.dp,
                    IsVirtual = false
                };
            }
            catch
            {
                return null;
            }
        }

        private class FinnhubQuote
        {
            public decimal c { get; set; }
            public decimal d { get; set; }
            public decimal dp { get; set; }
        }
    }
}
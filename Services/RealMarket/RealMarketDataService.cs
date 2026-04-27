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
        private static readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private readonly string _apiKey;

        public RealMarketDataService()
        {
            _apiKey = EnvService.Get("FINNHUB_API_KEY");
        }

        public async Task<AssetFetchBatchResult> GetAssetsAsync(IEnumerable<string> tickers)
        {
            var normalizedTickers = tickers
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToUpperInvariant())
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            var batchResult = new AssetFetchBatchResult
            {
                RequestedCount = normalizedTickers.Count
            };

            if (normalizedTickers.Count == 0)
            {
                batchResult.Errors.Add(new AssetFetchError
                {
                    Ticker = "-",
                    Message = "Список тикеров пуст."
                });

                return batchResult;
            }

            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "your_api_key_here")
            {
                foreach (var ticker in normalizedTickers)
                {
                    batchResult.Errors.Add(new AssetFetchError
                    {
                        Ticker = ticker,
                        Message = "Укажи реальный FINNHUB_API_KEY в .env"
                    });
                }

                return batchResult;
            }

            var tasks = normalizedTickers.Select(GetAssetInternalAsync).ToList();
            var results = await Task.WhenAll(tasks);

            foreach (var result in results.OrderBy(r => r.Ticker))
            {
                if (result.Asset != null)
                {
                    batchResult.Assets.Add(result.Asset);
                }
                else
                {
                    batchResult.Errors.Add(new AssetFetchError
                    {
                        Ticker = result.Ticker,
                        Message = result.ErrorMessage
                    });
                }
            }

            return batchResult;
        }

        public async Task<TickerValidationResult> ValidateTickerAsync(string ticker)
        {
            string normalizedTicker = NormalizeTicker(ticker);

            if (string.IsNullOrWhiteSpace(normalizedTicker))
            {
                return new TickerValidationResult
                {
                    Ticker = string.Empty,
                    IsValid = false,
                    Message = "Тикер пустой."
                };
            }

            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "your_api_key_here")
            {
                return new TickerValidationResult
                {
                    Ticker = normalizedTicker,
                    IsValid = false,
                    Message = "Укажи реальный FINNHUB_API_KEY в .env"
                };
            }

            var result = await GetAssetInternalAsync(normalizedTicker);

            return new TickerValidationResult
            {
                Ticker = normalizedTicker,
                IsValid = result.Asset != null,
                Message = result.Asset != null ? "Тикер найден." : result.ErrorMessage,
                Asset = result.Asset
            };
        }

        private async Task<SingleAssetFetchResult> GetAssetInternalAsync(string ticker)
        {
            try
            {
                string url = $"https://finnhub.io/api/v1/quote?symbol={ticker}&token={_apiKey}";
                using var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return new SingleAssetFetchResult
                    {
                        Ticker = ticker,
                        ErrorMessage = $"HTTP {(int)response.StatusCode}"
                    };
                }

                string responseText = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(responseText))
                {
                    return new SingleAssetFetchResult
                    {
                        Ticker = ticker,
                        ErrorMessage = "Пустой ответ API."
                    };
                }

                FinnhubQuote? quote = JsonSerializer.Deserialize<FinnhubQuote>(responseText);

                if (quote == null)
                {
                    return new SingleAssetFetchResult
                    {
                        Ticker = ticker,
                        ErrorMessage = "Не удалось разобрать ответ API."
                    };
                }

                if (!string.IsNullOrWhiteSpace(quote.error))
                {
                    return new SingleAssetFetchResult
                    {
                        Ticker = ticker,
                        ErrorMessage = quote.error
                    };
                }

                if (quote.c <= 0)
                {
                    return new SingleAssetFetchResult
                    {
                        Ticker = ticker,
                        ErrorMessage = "Котировка не найдена или цена некорректна."
                    };
                }

                return new SingleAssetFetchResult
                {
                    Ticker = ticker,
                    Asset = new Asset
                    {
                        Ticker = ticker,
                        Name = ticker,
                        CurrentPrice = quote.c,
                        Change = quote.d,
                        ChangePercent = quote.dp,
                        IsVirtual = false,
                        LastUpdatedUtc = DateTime.UtcNow
                    }
                };
            }
            catch (TaskCanceledException)
            {
                return new SingleAssetFetchResult
                {
                    Ticker = ticker,
                    ErrorMessage = "Превышено время ожидания ответа API."
                };
            }
            catch (HttpRequestException ex)
            {
                return new SingleAssetFetchResult
                {
                    Ticker = ticker,
                    ErrorMessage = $"Ошибка сети: {ex.Message}"
                };
            }
            catch (JsonException)
            {
                return new SingleAssetFetchResult
                {
                    Ticker = ticker,
                    ErrorMessage = "Некорректный JSON от API."
                };
            }
            catch (Exception ex)
            {
                return new SingleAssetFetchResult
                {
                    Ticker = ticker,
                    ErrorMessage = $"Неизвестная ошибка: {ex.Message}"
                };
            }
        }

        private static string NormalizeTicker(string ticker)
        {
            return string.IsNullOrWhiteSpace(ticker)
                ? string.Empty
                : ticker.Trim().ToUpperInvariant();
        }

        private class SingleAssetFetchResult
        {
            public string Ticker { get; set; } = string.Empty;
            public Asset? Asset { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;
        }

        private class FinnhubQuote
        {
            public decimal c { get; set; }
            public decimal d { get; set; }
            public decimal dp { get; set; }
            public string? error { get; set; }
        }
    }
}
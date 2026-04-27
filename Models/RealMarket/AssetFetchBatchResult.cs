using System.Collections.Generic;
using System.Linq;

namespace StockExchangeSimulator.Models
{
    public class AssetFetchBatchResult
    {
        public List<Asset> Assets { get; set; } = new();
        public List<AssetFetchError> Errors { get; set; } = new();

        public int RequestedCount { get; set; }
        public int SuccessCount => Assets.Count;
        public int ErrorCount => Errors.Count;

        public bool HasSuccess => Assets.Count > 0;
        public bool HasErrors => Errors.Count > 0;

        public string BuildUserMessage()
        {
            if (RequestedCount == 0)
                return "Список тикеров пуст.";

            if (HasSuccess && !HasErrors)
                return $"Успешно обновлено {SuccessCount}/{RequestedCount} тикеров.";

            if (HasSuccess && HasErrors)
            {
                string failedTickers = string.Join(", ", Errors.Select(e => e.Ticker));
                return $"Обновление частичное: {SuccessCount}/{RequestedCount}. Ошибки: {failedTickers}.";
            }

            if (HasErrors)
            {
                var firstError = Errors.FirstOrDefault();
                if (firstError != null)
                    return $"Не удалось загрузить котировки. {firstError.Ticker}: {firstError.Message}";
            }

            return "Не удалось загрузить котировки.";
        }
    }
}
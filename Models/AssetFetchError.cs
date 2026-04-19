namespace StockExchangeSimulator.Models
{
    public class AssetFetchError
    {
        public string Ticker { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
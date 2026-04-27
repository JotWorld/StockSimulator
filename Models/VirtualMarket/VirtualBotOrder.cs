namespace StockExchangeSimulator.Models.VirtualMarket
{
    public enum VirtualOrderSide
    {
        Buy,
        Sell
    }

    public class VirtualBotOrder
    {
        public string BotName { get; set; } = string.Empty;
        public string Ticker { get; set; } = string.Empty;
        public VirtualOrderSide Side { get; set; }
        public int Quantity { get; set; }

        // Limit price is required by the matching engine.
        // If a strategy leaves it as 0, the engine will convert it to an aggressive limit order around the current price.
        public decimal LimitPrice { get; set; }

        public string Reason { get; set; } = "Bot";
    }
}

namespace StockExchangeSimulator.Services
{
    public interface IDialogService
    {
        void ShowInfo(string message, string title = "Информация");
        void ShowWarning(string message, string title = "Предупреждение");
        void ShowError(string message, string title = "Ошибка");
        bool Confirm(string message, string title = "Подтверждение");
        string? SaveFile(string filter, string defaultFileName);
    }
}
using Microsoft.Win32;
using System.Windows;

namespace StockExchangeSimulator.Services
{
    public class DialogService : IDialogService
    {
        public void ShowInfo(string message, string title = "Информация")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowWarning(string message, string title = "Предупреждение")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowError(string message, string title = "Ошибка")
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public bool Confirm(string message, string title = "Подтверждение")
        {
            return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public string? SaveFile(string filter, string defaultFileName)
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = defaultFileName,
                AddExtension = true,
                OverwritePrompt = true
            };

            return dialog.ShowDialog() == true
                ? dialog.FileName
                : null;
        }
    }
}
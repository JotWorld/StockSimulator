using System.IO;
using System.Windows;
using StockExchangeSimulator.Services;

namespace StockExchangeSimulator
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string envPath = Path.Combine(AppContext.BaseDirectory, ".env");
            EnvService.Load(envPath);
        }
    }
}
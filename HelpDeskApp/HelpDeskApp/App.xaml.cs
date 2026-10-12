using System.Configuration;
using System.Data;
using System.Windows;

namespace HelpDeskApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd przy uruchamianiu: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }
    }

}

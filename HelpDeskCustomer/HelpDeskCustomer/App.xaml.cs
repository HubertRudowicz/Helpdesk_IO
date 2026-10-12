using System.Windows;

namespace HelpDeskCustomer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var login = new LoginWindow();
            bool? result = login.ShowDialog();

            if (result != true)
            {
                Shutdown(); // logowanie nie powiodło się lub zamknięto okno
            }
        }
    }
}

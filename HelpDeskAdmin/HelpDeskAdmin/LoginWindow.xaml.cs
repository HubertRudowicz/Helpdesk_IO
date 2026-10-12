using System.Windows;
using System.Windows.Input;

namespace HelpDeskAdmin
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginTextBox.Text;
            string password = PasswordBox.Password;

            if (login == "nsi" && password == "nsi" || login== "uggre" && password == "Grebocice59150")
            {
                MainWindow main = new MainWindow();
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Niepoprawny login lub hasło.", "Błąd logowania", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LoginButton_Click(sender, e); // wywołanie logiki logowania
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoginTextBox.Focus();
        }
    }
}

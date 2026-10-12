using System;
using System.Diagnostics;
using System.Text;
using System.Windows;

namespace HelpDeskAdmin
{
    public partial class NotificationWindow : Window
    {
        private static NotificationWindow? currentNotification = null;
        private static readonly object lockObject = new object();

        public NotificationWindow(string message, int count)
        {
            InitializeComponent();

            txtSubtitle.Text = count == 1
                ? "Otrzymano 1 nowe zgłoszenie"
                : $"Otrzymano {count} nowych zgłoszeń";

            txtMessage.Text = message;

            Debug.WriteLine($"NotificationWindow: Utworzono okno powiadomienia (count: {count})");
        }

        public static void ShowNotification(string message, int count)
        {
            lock (lockObject)
            {
                // Jeśli już istnieje powiadomienie, zamknij je
                if (currentNotification != null)
                {
                    Debug.WriteLine("NotificationWindow: Zamykam istniejące powiadomienie");
                    try
                    {
                        currentNotification.Close();
                        currentNotification = null;
                    }
                    catch { }
                }

                // Stwórz nowe powiadomienie
                Debug.WriteLine("NotificationWindow: Tworzę nowe powiadomienie");
                currentNotification = new NotificationWindow(message, count);
                currentNotification.Closed += (s, e) =>
                {
                    lock (lockObject)
                    {
                        currentNotification = null;
                        Debug.WriteLine("NotificationWindow: Powiadomienie zamknięte");
                    }
                };

                currentNotification.ShowDialog();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("NotificationWindow: Kliknięto OK");
            this.DialogResult = true;
            this.Close();
        }
    }
}
using System;
using System.Windows;

namespace HelpDeskCustomer
{
    public partial class DateRangeDialog : Window
    {
        public DateTime? StartDate => dpStart.SelectedDate;
        public DateTime? EndDate => dpEnd.SelectedDate;

        public DateRangeDialog()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (StartDate == null || EndDate == null)
            {
                MessageBox.Show("Proszę wybrać obie daty.");
                return;
            }

            if (StartDate > EndDate)
            {
                MessageBox.Show("Data początkowa nie może być późniejsza niż końcowa.");
                return;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}

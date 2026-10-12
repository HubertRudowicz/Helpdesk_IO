using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace HelpDeskCustomer
{
    public partial class ESPWindow : Window
    {
        private string baseFolderPath;
        public ESPWindow(string baseFolderPath, DateTime startDate, DateTime endDate)
        {
            InitializeComponent();
            this.baseFolderPath = baseFolderPath;
            GenerateESP(startDate, endDate);
        }

        private void GenerateESP(DateTime startDate, DateTime endDate)
        {
            espResultPanel.Children.Clear();

            for (DateTime day = startDate; day <= endDate; day = day.AddDays(1))
            {
                var reportsForDay = GetReportsByDay(day);

                if (reportsForDay.Count == 0)
                    continue;

                TextBlock dayHeader = new TextBlock
                {
                    Text = day.ToString("yyyy-MM-dd"),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 10, 0, 5)
                };
                espResultPanel.Children.Add(dayHeader);

                foreach (var rep in reportsForDay)
                {
                    StackPanel reportPanel = new StackPanel
                    {
                        Margin = new Thickness(10, 0, 0, 10)
                    };

                    TextBlock header = new TextBlock
                    {
                        Text = $"[{rep.timestamp}] {rep.title} (Użytkownik: {rep.userName})",
                        FontWeight = FontWeights.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    };

                    reportPanel.Children.Add(header);

                    if (rep.IsCancelled)
                    {
                        TextBlock cancelledLabel = new TextBlock
                        {
                            Text = "Anulowane",
                            FontWeight = FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.Red,
                            Margin = new Thickness(0, 2, 0, 0)
                        };
                        reportPanel.Children.Add(cancelledLabel);
                    }

                    TextBlock description = new TextBlock
                    {
                        Text = rep.description,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 0)
                    };

                    reportPanel.Children.Add(description);
                    espResultPanel.Children.Add(reportPanel);
                }
            }
        }

        private List<ZgloszenieReport> GetReportsByDay(DateTime day)
        {
            List<ZgloszenieReport> reports = new List<ZgloszenieReport>();

            if (!Directory.Exists(baseFolderPath))
                return reports;

            var doneFolders = Directory.GetDirectories(baseFolderPath, "zgloszenie_*.done");
            var cancelledFolders = Directory.GetDirectories(baseFolderPath, "zgloszenie_*.cancelled");

            foreach (var folder in doneFolders.Concat(cancelledFolders))
            {
                string[] jsonFiles = Directory.GetFiles(folder, "*.json");
                if (jsonFiles.Length == 0)
                    continue;

                try
                {
                    string jsonText = File.ReadAllText(jsonFiles[0]);
                    var report = JsonSerializer.Deserialize<ZgloszenieReport>(jsonText);

                    if (report != null && DateTime.TryParse(report.timestamp, out DateTime reportDate))
                    {
                        if (reportDate.Date == day.Date)
                        {
                            // Dodaj informację, że jest anulowane, jeśli folder kończy się na .cancelled
                            if (folder.EndsWith(".cancelled", StringComparison.OrdinalIgnoreCase))
                                report.IsCancelled = true;

                            reports.Add(report);
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            return reports.OrderBy(r => r.timestamp).ToList();
        }
    }

    public class ZgloszenieReport
    {
        public string title { get; set; }
        public string description { get; set; }
        public string userName { get; set; }
        public string timestamp { get; set; }

        // Dodaj pole pomocnicze (nie serializowane)
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsCancelled { get; set; } = false;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace HelpDeskAdmin
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

                    // Użyj godziny zakończenia zamiast utworzenia
                    string displayTime = rep.completionTime ?? rep.timestamp;

                    TextBlock header = new TextBlock
                    {
                        Text = $"[{displayTime}] {rep.title} (Użytkownik: {rep.userName})",
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

                    if (report != null)
                    {
                        // Pobierz datę zakończenia z folderu
                        string folderName = Path.GetFileName(folder);
                        DateTime? completionDate = GetCompletionDateFromFolder(folder, folderName);

                        if (completionDate.HasValue && completionDate.Value.Date == day.Date)
                        {
                            // Ustaw godzinę zakończenia
                            report.completionTime = completionDate.Value.ToString("yyyy-MM-dd HH:mm:ss");

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

            // Sortuj po czasie zakończenia
            return reports.OrderBy(r => r.completionTime ?? r.timestamp).ToList();
        }

        private DateTime? GetCompletionDateFromFolder(string folderPath, string folderName)
        {
            try
            {
                // Spróbuj pobrać datę z metadanych folderu (data ostatniej modyfikacji)
                DirectoryInfo dirInfo = new DirectoryInfo(folderPath);

                // Sprawdź czy istnieje plik completion.txt z datą zakończenia
                string completionFile = Path.Combine(folderPath, "completion.txt");
                if (File.Exists(completionFile))
                {
                    string completionText = File.ReadAllText(completionFile);
                    if (DateTime.TryParse(completionText, out DateTime completionDate))
                    {
                        return completionDate;
                    }
                }

                // Jeśli nie ma pliku completion.txt, użyj daty ostatniej modyfikacji folderu
                return dirInfo.LastWriteTime;
            }
            catch
            {
                return null;
            }
        }
    }

    public class ZgloszenieReport
    {
        public string title { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string userName { get; set; } = string.Empty;
        public string timestamp { get; set; } = string.Empty;
        public string? completionTime { get; set; }

        // Dodaj pole pomocnicze (nie serializowane)
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsCancelled { get; set; } = false;
    }
}
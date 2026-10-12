using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using DrawingColor = System.Drawing.Color;
using IOPath = System.IO.Path;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace HelpDeskApp
{
    public partial class MainWindow : Window
    {
        private string StatusFilePath;
        private string ReportFolderPath;
        private List<string> attachments = new List<string>();
        private bool hasUnsavedChanges = false;
        private string RemoteSupportExePath;
        private DispatcherTimer zgłoszenieCheckerTimer;
        private HashSet<string> aktywneZgloszenia = new();



        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            LoadStatus();
            this.Closing += MainWindow_Closing;
            LoadReportFolderPath();
            WczytajZgloszenia();
            DataContext = this;

            foreach (var zg in HistoriaZgloszen)
            {
                if (!zg.Zakonczone && !zg.Anulowane)
                {
                    string bazowaSciezka = zg.PełnaŚcieżka;
                    if (Directory.Exists(bazowaSciezka) && !Directory.Exists(bazowaSciezka + ".done"))
                    {
                        aktywneZgloszenia.Add(bazowaSciezka);
                    }
                }
            }

            zgłoszenieCheckerTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            zgłoszenieCheckerTimer.Tick += SprawdzCzyZgloszeniaZostalyZakonczone;
            zgłoszenieCheckerTimer.Start();
        }


        private void LoadConfig()
        {
            try
            {
                string configPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException("Brak pliku konfiguracyjnego config.json");
                }

                string json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                StatusFilePath = config["StatusFilePath"];
                ReportFolderPath = config["ReportFolderPath"];
                RemoteSupportExePath = config["RemoteSupportExePath"];
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania konfiguracji: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }
        private void StartRemoteSupportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(RemoteSupportExePath))
                {
                    MessageBox.Show("Plik pomoc_zdalna.exe nie został znaleziony.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = RemoteSupportExePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się uruchomić programu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void AddAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog { Multiselect = true };
                if (openFileDialog.ShowDialog() == true)
                {
                    foreach (string file in openFileDialog.FileNames)
                    {
                        if (!attachments.Contains(file))
                        {
                            attachments.Add(file);
                            AttachmentsListBox.Items.Add(IOPath.GetFileName(file));
                            hasUnsavedChanges = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dodawania załącznika: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveAttachmentButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string attachmentFileName)
            {
                string fullPath = attachments.Find(path => IOPath.GetFileName(path) == attachmentFileName);
                if (fullPath != null)
                {
                    attachments.Remove(fullPath);
                    AttachmentsListBox.Items.Remove(attachmentFileName);
                    hasUnsavedChanges = true;
                }
            }
        }

        private void TakeScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.Hide();
                SnippingWindow snippingWindow = new SnippingWindow();
                snippingWindow.ScreenshotCaptured += (s, screenshotPath) =>
                {
                    if (!string.IsNullOrEmpty(screenshotPath))
                    {
                        if (!attachments.Contains(screenshotPath))
                        {
                            attachments.Add(screenshotPath);
                            AttachmentsListBox.Items.Add(IOPath.GetFileName(screenshotPath));
                            hasUnsavedChanges = true;
                        }
                    }
                    this.Show();
                };
                snippingWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się przechwycić zrzutu ekranu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Show();
            }
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string title = TitleTextBox.Text?.Trim();
                string desc = DescriptionTextBox.Text?.Trim();

                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(desc) && attachments.Count == 0)
                {
                    MessageBox.Show("Nie można wysłać pustego zgłoszenia.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string time = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string reportFolder = IOPath.Combine(ReportFolderPath, $"zgloszenie_{time}");
                Directory.CreateDirectory(reportFolder);

                List<string> attachmentPaths = new List<string>();
                foreach (string attachment in attachments)
                {
                    string destFile = IOPath.Combine(reportFolder, IOPath.GetFileName(attachment));
                    File.Copy(attachment, destFile, true);
                    attachmentPaths.Add(destFile);
                }

                var reportData = new
                {
                    title = string.IsNullOrWhiteSpace(title) ? "Brak tytułu" : title,
                    description = string.IsNullOrWhiteSpace(desc) ? "Brak opisu" : desc,
                    priority = (PriorityComboBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Nieokreślony",
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    userName = Environment.UserName,
                    attachments = attachmentPaths,
                    status = "new"
                };

                string jsonFilePath = IOPath.Combine(reportFolder, "zgloszenie.json");
                File.WriteAllText(jsonFilePath, JsonSerializer.Serialize(reportData, new JsonSerializerOptions { WriteIndented = true }));

                MessageBox.Show("Zgłoszenie zostało zapisane.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                TitleTextBox.Text = "";
                DescriptionTextBox.Text = "";
                PriorityComboBox.SelectedIndex = 0;
                AttachmentsListBox.Items.Clear();
                attachments.Clear();
                hasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            WczytajZgloszenia(); 
        }

        private void RefreshStatusButton_Click(object sender, RoutedEventArgs e)
        {
            LoadStatus();
        }

        private void LoadStatus()
        {
            try
            {
                if (File.Exists(StatusFilePath))
                {
                    string json = File.ReadAllText(StatusFilePath);
                    var statusList = ParseJson(json);
                    StatusTreeView.ItemsSource = statusList;
                }
                else
                {
                    MessageBox.Show("Plik statusu nie istnieje.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<ProgramStatus> ParseJson(string json)
        {
            var root = JsonDocument.Parse(json).RootElement;
            return ParseJsonElement(root);
        }

        private List<ProgramStatus> ParseJsonElement(JsonElement element)
        {
            var list = new List<ProgramStatus>();
            foreach (var property in element.EnumerateObject())
            {
                var status = new ProgramStatus { Name = property.Name };

                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    status.SubPrograms = ParseJsonElement(property.Value);
                    // NIE ustawiaj status.Status tutaj – zostaje null
                }
                else if (property.Value.ValueKind == JsonValueKind.True || property.Value.ValueKind == JsonValueKind.False)
                {
                    status.Status = property.Value.GetBoolean();
                }

                list.Add(status);
            }
            return list;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (hasUnsavedChanges ||
                !string.IsNullOrWhiteSpace(TitleTextBox.Text) ||
                !string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                var result = MessageBox.Show("Masz niezapisane dane. Zamknąć program?", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

        private class SnippingWindow : Window
        {
            private System.Windows.Point? startPoint;
            private WpfRectangle selectionRectangle;
            private Canvas canvas;

            public event EventHandler<string> ScreenshotCaptured;

            public SnippingWindow()
            {
                this.WindowStyle = WindowStyle.None;
                this.AllowsTransparency = true;
                this.Background = new SolidColorBrush(MediaColor.FromArgb(1, 0, 0, 0)); // prawie przezroczyste
                this.Opacity = 1;
                this.Topmost = true;
                this.Cursor = Cursors.Cross;
                this.Focusable = true;
                this.Focus();

                this.Left = SystemParameters.VirtualScreenLeft;
                this.Top = SystemParameters.VirtualScreenTop;
                this.Width = SystemParameters.VirtualScreenWidth;
                this.Height = SystemParameters.VirtualScreenHeight;

                canvas = new Canvas();
                this.Content = canvas;

                this.MouseDown += SnippingWindow_MouseDown;
                this.MouseMove += SnippingWindow_MouseMove;
                this.MouseUp += SnippingWindow_MouseUp;
                this.KeyDown += SnippingWindow_KeyDown;
            }


            private void SnippingWindow_MouseDown(object sender, MouseButtonEventArgs e)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    startPoint = e.GetPosition(this);
                    selectionRectangle = new WpfRectangle
                    {
                        Stroke = MediaBrushes.Red,
                        StrokeThickness = 2,
                        Fill = new SolidColorBrush(MediaColor.FromArgb(50, 255, 255, 255))
                    };
                    Canvas.SetLeft(selectionRectangle, startPoint.Value.X);
                    Canvas.SetTop(selectionRectangle, startPoint.Value.Y);
                    canvas.Children.Clear();
                    canvas.Children.Add(selectionRectangle);
                }
            }

            private void SnippingWindow_MouseMove(object sender, MouseEventArgs e)
            {
                if (startPoint.HasValue && e.LeftButton == MouseButtonState.Pressed)
                {
                    var pos = e.GetPosition(this);
                    double x = Math.Min(pos.X, startPoint.Value.X);
                    double y = Math.Min(pos.Y, startPoint.Value.Y);
                    double width = Math.Abs(pos.X - startPoint.Value.X);
                    double height = Math.Abs(pos.Y - startPoint.Value.Y);

                    Canvas.SetLeft(selectionRectangle, x);
                    Canvas.SetTop(selectionRectangle, y);
                    selectionRectangle.Width = width;
                    selectionRectangle.Height = height;
                }
            }

            private void SnippingWindow_MouseUp(object sender, MouseButtonEventArgs e)
            {
                if (startPoint.HasValue)
                {
                    var endPoint = e.GetPosition(this);
                    CaptureScreenshot(startPoint.Value, endPoint);
                    this.Close();
                }
            }

            private void SnippingWindow_KeyDown(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    ScreenshotCaptured?.Invoke(this, null);
                    this.Close();
                }
            }

            private void CaptureScreenshot(System.Windows.Point start, System.Windows.Point end)
            {
                try
                {
                    int x = (int)(Math.Min(start.X, end.X) + SystemParameters.VirtualScreenLeft);
                    int y = (int)(Math.Min(start.Y, end.Y) + SystemParameters.VirtualScreenTop);
                    int width = (int)Math.Abs(end.X - start.X);
                    int height = (int)Math.Abs(end.Y - start.Y);

                    if (width < 1 || height < 1)
                    {
                        ScreenshotCaptured?.Invoke(this, null);
                        return; // bez zrzutu, zamknij
                    }

                    this.Hide();

                    using (Bitmap bmp = new Bitmap(width, height))
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(width, height));
                        }

                        string tempDir = IOPath.Combine(IOPath.GetTempPath(), "HelpDeskScreenshots");
                        Directory.CreateDirectory(tempDir);
                        string filename = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                        string filepath = IOPath.Combine(tempDir, filename);
                        bmp.Save(filepath, ImageFormat.Png);

                        this.Show();

                        ScreenshotCaptured?.Invoke(this, filepath);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd zapisu zrzutu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    ScreenshotCaptured?.Invoke(this, null);
                }
            }

        }
        public ZgloszenieInfo WybraneZgloszenie { get; set; }

        private void OdswiezZgloszenia_Click(object sender, RoutedEventArgs e)
        {
            WczytajZgloszenia();
        }

        private void AnulujZgloszenie_Click(object sender, RoutedEventArgs e)
        {
            if (WybraneZgloszenie == null || WybraneZgloszenie.Zakonczone || WybraneZgloszenie.Anulowane)
            {
                MessageBox.Show("Nie można anulować tego zgłoszenia.");
                return;
            }

            string nowaNazwa = WybraneZgloszenie.PełnaŚcieżka + ".cancelled";

            try
            {
                System.IO.Directory.Move(WybraneZgloszenie.PełnaŚcieżka, nowaNazwa);
                WczytajZgloszenia();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd przy anulowaniu zgłoszenia:\n{ex.Message}");
            }
        }


        public ObservableCollection<ZgloszenieInfo> HistoriaZgloszen { get; set; } = new ObservableCollection<ZgloszenieInfo>();

        private void WczytajZgloszenia()
        {
            HistoriaZgloszen.Clear();
            if (!Directory.Exists(reportFolderPath)) return;

            var folders = Directory.GetDirectories(reportFolderPath)
                .Where(f => System.IO.Path.GetFileName(f).StartsWith("zgloszenie_"));

            string localUserName = Environment.UserName;
            foreach (var folder in folders)
            {
                string jsonPath = System.IO.Path.Combine(folder, "zgloszenie.json");
                if (!File.Exists(jsonPath)) continue;

                try
                {
                    string json = File.ReadAllText(jsonPath);
                    using var doc = JsonDocument.Parse(json);
                    string userName = doc.RootElement.GetProperty("userName").GetString();

                    if (string.Equals(userName, localUserName, StringComparison.OrdinalIgnoreCase))
                    {
                        HistoriaZgloszen.Add(new ZgloszenieInfo
                        {
                            Nazwa = System.IO.Path.GetFileName(folder),
                            PełnaŚcieżka = folder
                        });
                    }
                }
                catch
                {
                    continue;
                }
            }

            // Aktualizacja aktywnych zgłoszeń
            aktywneZgloszenia.Clear();
            foreach (var zg in HistoriaZgloszen)
            {
                if (!zg.Zakonczone && !zg.Anulowane)
                {
                    string bazowaSciezka = zg.PełnaŚcieżka;
                    if (Directory.Exists(bazowaSciezka) && !Directory.Exists(bazowaSciezka + ".done"))
                    {
                        aktywneZgloszenia.Add(bazowaSciezka);
                    }
                }
            }
        }

        private void HistoriaListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ZgloszeniaList.SelectedItem is ZgloszenieInfo wybrane)
            {
                WybraneZgloszenie = wybrane;
                string jsonPath = System.IO.Path.Combine(wybrane.PełnaŚcieżka, "zgloszenie.json");
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        string json = File.ReadAllText(jsonPath);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        TitleTextBox.Text = root.GetProperty("title").GetString();
                        DescriptionTextBox.Text = root.GetProperty("description").GetString();

                        PriorityComboBox.SelectedItem = PriorityComboBox.Items
                            .Cast<ComboBoxItem>()
                            .FirstOrDefault(i => i.Content.ToString() == root.GetProperty("priority").GetString());

                        AttachmentsListBox.Items.Clear();
                        attachments.Clear();

                        if (root.TryGetProperty("attachments", out JsonElement attList))
                        {
                            foreach (var item in attList.EnumerateArray())
                            {
                                string path = item.GetString();
                                if (File.Exists(path))
                                {
                                    attachments.Add(path);
                                    AttachmentsListBox.Items.Add(System.IO.Path.GetFileName(path));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd wczytywania zgłoszenia: {ex.Message}");
                    }
                }
            }
        }
        private void ZgloszeniaList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ZgloszeniaList.SelectedItem is ZgloszenieInfo zgl)
            {
                string jsonPath = System.IO.Path.Combine(zgl.PełnaŚcieżka, "zgloszenie.json");
                if (!File.Exists(jsonPath)) return;

                try
                {
                    string json = File.ReadAllText(jsonPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    TitleTextBox.Text = root.GetProperty("title").GetString() ?? "";
                    DescriptionTextBox.Text = root.GetProperty("description").GetString() ?? "";

                    string priority = root.GetProperty("priority").GetString() ?? "Nieokreślony";
                    PriorityComboBox.SelectedIndex = -1;
                    foreach (ComboBoxItem item in PriorityComboBox.Items)
                    {
                        if (item.Content?.ToString() == priority)
                        {
                            PriorityComboBox.SelectedItem = item;
                            break;
                        }
                    }

                    AttachmentsListBox.Items.Clear();
                    attachments.Clear();

                    if (root.TryGetProperty("attachments", out JsonElement attList))
                    {
                        foreach (var item in attList.EnumerateArray())
                        {
                            string path = item.GetString();
                            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                            {
                                attachments.Add(path);
                                AttachmentsListBox.Items.Add(System.IO.Path.GetFileName(path));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd wczytywania zgłoszenia: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private string reportFolderPath;

        private void LoadReportFolderPath()
        {
            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string exeDir = System.IO.Path.GetDirectoryName(exePath);
            string configPath = System.IO.Path.Combine(exeDir, "config.json");

            if (File.Exists(configPath))
            {
                var configJson = File.ReadAllText(configPath);
                var configData = JsonSerializer.Deserialize<Dictionary<string, string>>(configJson);
                if (configData.TryGetValue("ReportFolderPath", out string path))
                {
                    reportFolderPath = path;
                }
            }
        }
        private void SprawdzCzyZgloszeniaZostalyZakonczone(object sender, EventArgs e)
        {
            try
            {
                List<string> zakonczone = new();
                foreach (string sciezka in aktywneZgloszenia.ToList())
                {
                    string zakonczonaSciezka = sciezka + ".done";
                    if (!Directory.Exists(sciezka) && Directory.Exists(zakonczonaSciezka))
                    {
                        zakonczone.Add(sciezka);
                        aktywneZgloszenia.Remove(sciezka);
                    }
                }
                if (zakonczone.Count > 0)
                {
                    MessageBox.Show($"Zakończono {zakonczone.Count} zgłoszenie(a).", "Zakończone zgłoszenia", MessageBoxButton.OK, MessageBoxImage.Information);
                    WczytajZgloszenia();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd w timerze: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }




        private class ProgramStatus
        {
            public string Name { get; set; }
            public bool? Status { get; set; }
            public List<ProgramStatus> SubPrograms { get; set; }
        }
        public class ZgloszenieInfo
        {
            public string Nazwa { get; set; }
            public string PełnaŚcieżka { get; set; }
            public bool Zakonczone => Nazwa.EndsWith(".done");
            public bool Anulowane => Nazwa.EndsWith(".cancelled");
            public string Status => Anulowane ? "Anulowane" : (Zakonczone ? "Zakończone" : "W toku");

            public string WyswietlanaNazwa
            {
                get
                {
                    string baseName = Nazwa;
                    if (baseName.EndsWith(".done")) baseName = baseName[..^5];
                    if (baseName.EndsWith(".cancelled")) baseName = baseName[..^10];

                    string timePart = baseName.Replace("zgloszenie_", "");
                    string[] parts = timePart.Split('_');
                    if (parts.Length >= 2)
                    {
                        string[] dateParts = parts[0].Split('-'); // [2025, 08, 04]
                        if (dateParts.Length == 3)
                        {
                            string data = $"{dateParts[2]}.{dateParts[1]}"; // dd.MM
                            if (File.Exists(IOPath.Combine(PełnaŚcieżka, "zgloszenie.json")))
                            {
                                try
                                {
                                    string json = File.ReadAllText(IOPath.Combine(PełnaŚcieżka, "zgloszenie.json"));
                                    using var doc = JsonDocument.Parse(json);
                                    string title = doc.RootElement.GetProperty("title").GetString();
                                    return $"{title} {data}";
                                }
                                catch
                                {
                                    return $"(błąd odczytu) {data}";
                                }
                            }
                            return $"(brak tytułu) {data}";
                        }
                    }
                    return Nazwa;
                }
            }
        }
    }
}

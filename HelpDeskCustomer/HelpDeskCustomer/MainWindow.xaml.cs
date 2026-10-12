using HelpDeskCustomer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HelpDeskCustomer
{
    public partial class MainWindow : Window
    {
        private string baseFolderPath;
        private DispatcherTimer refreshTimer;
        private HashSet<string> knownFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, DateTime> notifiedFolders = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private bool isLoading = false;

        public MainWindow()
        {
            InitializeComponent();
            WindowState = WindowState.Maximized;

            this.Closed += (s, e) =>
            {
                refreshTimer?.Stop();
                Application.Current.Shutdown();
            };

            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (!File.Exists(configPath))
                {
                    MessageBox.Show("Nie znaleziono pliku config.json.");
                    Application.Current.Shutdown();
                    return;
                }

                string jsonConfig = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(jsonConfig);
                if (doc.RootElement.TryGetProperty("baseFolderPath", out var basePathProp))
                {
                    baseFolderPath = basePathProp.GetString();
                }
                else
                {
                    MessageBox.Show("Brakuje właściwości 'baseFolderPath' w config.json.");
                    Application.Current.Shutdown();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd wczytywania konfiguracji: " + ex.Message);
                Application.Current.Shutdown();
                return;
            }

            LoadReports(initialLoad: true);

            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromSeconds(30);
            refreshTimer.Tick += (s, e) => LoadReports(initialLoad: false);
            refreshTimer.Start();
        }

        private async void LoadReports(bool initialLoad = false)
        {
            isLoading = true;

            try
            {
                if (!Directory.Exists(baseFolderPath))
                {
                    MessageBox.Show("Nie znaleziono folderu zgłoszeń.");
                    return;
                }

                var folders = Directory.GetDirectories(baseFolderPath, "zgloszenie_*")
                    .Where(f => !f.EndsWith(".done", StringComparison.OrdinalIgnoreCase) && Directory.GetFiles(f, "*.json").Any())
                    .ToArray();

                var newFolderNames = folders.Select(f => Path.GetFileName(f)).ToList();
                var newOnes = newFolderNames.Where(name => !knownFolders.Contains(name)).ToList();

                Debug.WriteLine($"LoadReports wywołane o {DateTime.Now}, initialLoad: {initialLoad}");
                Debug.WriteLine($"Aktualne foldery: {string.Join(", ", newFolderNames)}");
                Debug.WriteLine($"Znane foldery: {string.Join(", ", knownFolders)}");
                Debug.WriteLine($"Nowe foldery: {string.Join(", ", newOnes)}");
                    // Zaktualizuj listę tylko dla istniejących folderów, zachowując nowo dodane
                    string previouslySelectedTag = null;
                    string previouslySelectedAttachment = null;

                    if (listBoxFolders.SelectedItem is ListBoxItem selectedItem)
                        previouslySelectedTag = selectedItem.Tag?.ToString();

                    if (listBoxAttachments.SelectedItem != null)
                        previouslySelectedAttachment = listBoxAttachments.SelectedItem.ToString();

                    var currentItems = listBoxFolders.Items.Cast<ListBoxItem>().ToDictionary(item => item.Tag?.ToString(), item => item);
                    var itemsToRemove = new List<ListBoxItem>();
                    foreach (ListBoxItem item in listBoxFolders.Items)
                    {
                        if (!newFolderNames.Contains(item.Tag?.ToString()))
                            itemsToRemove.Add(item);
                    }
                    foreach (var item in itemsToRemove)
                    {
                        listBoxFolders.Items.Remove(item);
                    }

                    foreach (var folderPath in folders)
                    {
                        string folderName = Path.GetFileName(folderPath);

                        string jsonFile = Directory.GetFiles(folderPath, "*.json").FirstOrDefault();
                        if (jsonFile == null) continue;

                        Report report;
                        try
                        {
                            string jsonText = File.ReadAllText(jsonFile);
                            report = JsonSerializer.Deserialize<Report>(jsonText);
                        }
                        catch
                        {
                            continue;
                        }

                        string displayName = $"{report.userName} {DateTime.Parse(report.timestamp).ToString("HH:mm")} {report.title}";

                        if (currentItems.TryGetValue(folderName, out var existingItem))
                        {
                            existingItem.Content = displayName; // Zaktualizuj istniejący element
                        }
                        else
                        {
                            var newItem = new ListBoxItem
                            {
                                Content = displayName,
                                Tag = folderName
                            };
                            listBoxFolders.Items.Add(newItem);
                        }

                        if (folderName == previouslySelectedTag)
                        {
                            listBoxFolders.SelectedItem = currentItems.ContainsKey(folderName) ? currentItems[folderName] : listBoxFolders.Items.Cast<ListBoxItem>().FirstOrDefault(i => i.Tag?.ToString() == folderName);
                        }
                    }

                    if (previouslySelectedTag != null && listBoxFolders.SelectedItem != null && listBoxFolders.SelectedItem is ListBoxItem selItem)
                    {
                        string selectedFolderPath = Path.Combine(baseFolderPath, previouslySelectedTag);
                        listBoxAttachments.Items.Clear();
                        foreach (var file in Directory.GetFiles(selectedFolderPath))
                        {
                            if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                listBoxAttachments.Items.Add(Path.GetFileName(file));
                        }

                        if (previouslySelectedAttachment != null && listBoxAttachments.Items.Contains(previouslySelectedAttachment))
                        {
                            listBoxAttachments.SelectedItem = previouslySelectedAttachment;
                        }
                        else
                        {
                            imageViewer.Source = null;
                        }
                    }
                    else
                    {
                        listBoxAttachments.Items.Clear();
                        imageViewer.Source = null;
                    }

                knownFolders = new HashSet<string>(newFolderNames, StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                isLoading = false;
            }
        }
        private void listBoxFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearDetails();
            if (listBoxFolders.SelectedItem == null) return;

            string folderName;
            if (listBoxFolders.SelectedItem is ListBoxItem lbi)
                folderName = lbi.Tag.ToString();
            else
                folderName = listBoxFolders.SelectedItem.ToString();

            string folderPath = Path.Combine(baseFolderPath, folderName);

            string[] jsonFiles = Directory.GetFiles(folderPath, "*.json");
            if (jsonFiles.Length == 0)
            {
                MessageBox.Show("Brak pliku JSON w folderze.");
                return;
            }

            string jsonFile = jsonFiles[0];
            Report report;
            try
            {
                string jsonText = File.ReadAllText(jsonFile);
                report = JsonSerializer.Deserialize<Report>(jsonText);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd odczytu JSON: " + ex.Message);
                return;
            }

            txtTitle.Text = report.title ?? "";
            txtDescription.Text = report.description ?? "";
            txtPriority.Text = report.priority ?? "";
            txtTimestamp.Text = report.timestamp ?? "";
            txtUserName.Text = report.userName ?? "";
            txtStatus.Text = report.status ?? "";

            listBoxAttachments.Items.Clear();
            foreach (var file in Directory.GetFiles(folderPath))
            {
                if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    listBoxAttachments.Items.Add(Path.GetFileName(file));
                }
            }

            imageViewer.Source = null;
        }

        private void ClearDetails()
        {
            txtTitle.Text = "";
            txtDescription.Text = "";
            txtPriority.Text = "";
            txtTimestamp.Text = "";
            txtUserName.Text = "";
            txtStatus.Text = "";
            listBoxAttachments.Items.Clear();
            imageViewer.Source = null;
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadReports();
            ClearDetails();
        }

        private void btnMarkDone_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxFolders.SelectedItem == null) return;
            string folderName = listBoxFolders.SelectedItem is ListBoxItem lbi ? lbi.Tag.ToString() : listBoxFolders.SelectedItem.ToString();
            string folderPath = Path.Combine(baseFolderPath, folderName);

            try
            {
                string doneFolderPath = folderPath + ".done";
                if (Directory.Exists(doneFolderPath))
                {
                    MessageBox.Show("Folder już oznaczony jako zakończony.");
                    return;
                }
                Directory.Move(folderPath, doneFolderPath);

                LoadReports();
                ClearDetails();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas oznaczania jako zakończone: " + ex.Message);
            }
        }

        private void listBoxAttachments_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listBoxAttachments.SelectedItem == null)
            {
                imageViewer.Source = null;
                return;
            }

            string folderName = listBoxFolders.SelectedItem is ListBoxItem lbi ? lbi.Tag.ToString() : listBoxFolders.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(folderName))
            {
                imageViewer.Source = null;
                return;
            }

            string folderPath = Path.Combine(baseFolderPath, folderName);
            string selectedFileName = listBoxAttachments.SelectedItem.ToString();
            string selectedFile = Path.Combine(folderPath, selectedFileName);

            if (File.Exists(selectedFile))
            {
                try
                {
                    string ext = Path.GetExtension(selectedFile).ToLower();
                    string[] allowedExts = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
                    if (allowedExts.Contains(ext))
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(selectedFile);
                        bitmap.EndInit();
                        imageViewer.Source = bitmap;
                    }
                    else
                    {
                        imageViewer.Source = null;
                    }
                }
                catch
                {
                    imageViewer.Source = null;
                    MessageBox.Show("Nie można otworzyć obrazu.");
                }
            }
            else
            {
                imageViewer.Source = null;
            }
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxFolders.SelectedItem == null) return;

            string folderName = null;
            if (listBoxFolders.SelectedItem is ListBoxItem lbi)
                folderName = lbi.Tag.ToString();
            else
                folderName = listBoxFolders.SelectedItem.ToString();

            string folderPath = Path.Combine(baseFolderPath, folderName);

            if (Directory.Exists(folderPath))
            {
                Process.Start("explorer.exe", folderPath);
            }
            else
            {
                MessageBox.Show("Folder nie istnieje.");
            }
        }

        private void btnGenerateESP_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DateRangeDialog();
            if (dialog.ShowDialog() == true)
            {
                DateTime startDate = dialog.StartDate ?? DateTime.MinValue;
                DateTime endDate = dialog.EndDate ?? DateTime.MaxValue;

                if (startDate > endDate)
                {
                    MessageBox.Show("Data początkowa nie może być większa od końcowej.");
                    return;
                }

                var espWindow = new ESPWindow(baseFolderPath, startDate, endDate);
                espWindow.Show();
            }
        }
    }

    public class Report
    {
        public string title { get; set; }
        public string description { get; set; }
        public string priority { get; set; }
        public string timestamp { get; set; }
        public string userName { get; set; }
        public string status { get; set; }
    }
}
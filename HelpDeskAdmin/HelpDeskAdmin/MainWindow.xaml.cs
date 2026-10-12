using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Linq;
using System.Media;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace HelpDeskAdmin
{
    public partial class MainWindow : Window
    {
        private string baseFolderPath = string.Empty;
        private DispatcherTimer refreshTimer = new DispatcherTimer();
        private HashSet<string> knownFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> notifiedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool isLoading = false;
        private bool isShowingNotification = false;

        public MainWindow()
        {
            InitializeComponent();

            // Ustawienia okna dla poprawnego wyświetlania
            this.WindowState = WindowState.Maximized;
            this.MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
            this.MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;

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
                    baseFolderPath = basePathProp.GetString() ?? string.Empty;
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

            // Pierwsze załadowanie bez powiadomień
            LoadReports(initialLoad: true);

            // Timer odświeżania co 30 sekund
            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromSeconds(30);
            refreshTimer.Tick += (s, e) => LoadReports(initialLoad: false);
            refreshTimer.Start();

            Debug.WriteLine("MainWindow: Inicjalizacja zakończona");
        }

        private async void LoadReports(bool initialLoad = false)
        {
            // Jeśli pokazuje się powiadomienie, poczekaj
            if (isLoading || isShowingNotification)
            {
                Debug.WriteLine("LoadReports: Pominięto - trwa ładowanie lub pokazywanie powiadomienia");
                return;
            }

            isLoading = true;
            Debug.WriteLine($"LoadReports: Rozpoczęto (initialLoad: {initialLoad})");

            try
            {
                if (!Directory.Exists(baseFolderPath))
                {
                    MessageBox.Show("Nie znaleziono folderu zgłoszeń - uruchom ponownie program.");
                    return;
                }

                var allFolders = Directory.GetDirectories(baseFolderPath, "zgloszenie_*")
                    .Where(f => Directory.GetFiles(f, "*.json").Any())
                    .ToArray();

                var activeFolders = allFolders
                    .Where(f =>
                        !f.EndsWith(".done", StringComparison.OrdinalIgnoreCase) &&
                        !f.EndsWith(".cancelled", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                var currentFolderNames = activeFolders
                    .Select(f => Path.GetFileName(f))
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Znajdź nowe foldery
                var newFolders = currentFolderNames
                    .Where(name => !knownFolders.Contains(name))
                    .ToList();

                // Znajdź usunięte foldery
                var removedFolders = knownFolders
                    .Where(name => !currentFolderNames.Contains(name))
                    .ToList();

                Debug.WriteLine($"LoadReports: Nowe: {newFolders.Count}, Usunięte: {removedFolders.Count}");

                // Usuń nieistniejące foldery
                RemoveFoldersFromList(removedFolders);

                // KLUCZOWE: Zaktualizuj knownFolders PRZED powiadomieniami
                knownFolders = new HashSet<string>(currentFolderNames, StringComparer.OrdinalIgnoreCase);

                // Pokaż powiadomienie o nowych zgłoszeniach (tylko jeśli nie initialLoad)
                if (!initialLoad && newFolders.Any())
                {
                    await ShowNewReportsNotification(newFolders);
                }

                // Odśwież listę
                RefreshReportsList(activeFolders, currentFolderNames);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd w LoadReports: {ex.Message}");
                MessageBox.Show($"Błąd podczas odświeżania listy: {ex.Message}");
            }
            finally
            {
                isLoading = false;
                Debug.WriteLine("LoadReports: Zakończono");
            }
        }

        private void RemoveFoldersFromList(List<string> foldersToRemove)
        {
            var itemsToRemove = new List<ListBoxItem>();
            foreach (ListBoxItem item in listBoxFolders.Items)
            {
                string? itemTag = item.Tag?.ToString();
                if (itemTag != null && foldersToRemove.Contains(itemTag, StringComparer.OrdinalIgnoreCase))
                {
                    itemsToRemove.Add(item);
                    knownFolders.Remove(itemTag);
                }
            }

            foreach (var item in itemsToRemove)
            {
                listBoxFolders.Items.Remove(item);
            }
        }

        private async Task ShowNewReportsNotification(List<string> newFolders)
        {
            // Sprawdź które foldery jeszcze nie były powiadomione
            var foldersToNotify = newFolders
                .Where(name => !notifiedFolders.Contains(name))
                .ToList();

            if (!foldersToNotify.Any())
            {
                Debug.WriteLine("ShowNewReportsNotification: Wszystkie foldery już powiadomione");
                return;
            }

            Debug.WriteLine($"ShowNewReportsNotification: Powiadomienie o {foldersToNotify.Count} folderach");

            // Oznacz jako powiadomione NATYCHMIAST
            foreach (var folder in foldersToNotify)
            {
                notifiedFolders.Add(folder);
            }

            // Zablokuj timer i ustaw flagę
            isShowingNotification = true;
            refreshTimer.Stop();

            try
            {
                // Buduj wiadomość
                var message = new StringBuilder();

                foreach (var folderName in foldersToNotify)
                {
                    string folderPath = Path.Combine(baseFolderPath, folderName);
                    string? jsonFile = Directory.GetFiles(folderPath, "*.json").FirstOrDefault();

                    if (jsonFile != null)
                    {
                        try
                        {
                            string jsonText = File.ReadAllText(jsonFile);
                            var report = JsonSerializer.Deserialize<Report>(jsonText);
                            if (report != null)
                            {
                                message.AppendLine($"📋 {report.userName}: {report.title}");
                            }
                            else
                            {
                                message.AppendLine($"📋 {folderName}");
                            }
                        }
                        catch
                        {
                            message.AppendLine($"📋 {folderName}");
                        }
                    }
                }

                // Odtwórz dźwięk
                PlayNotificationSound();

                // Pokaż WŁASNE okno powiadomienia (nie MessageBox!)
                Debug.WriteLine("ShowNewReportsNotification: Wywołuję NotificationWindow.ShowNotification");
                NotificationWindow.ShowNotification(message.ToString().Trim(), foldersToNotify.Count);

                Debug.WriteLine("ShowNewReportsNotification: NotificationWindow zamknięte");
            }
            finally
            {
                // Odczekaj chwilę przed wznowieniem timera
                await Task.Delay(1000);

                isShowingNotification = false;
                refreshTimer.Start();

                Debug.WriteLine("ShowNewReportsNotification: Timer wznowiony");
            }
        }

        private void RefreshReportsList(string[] activeFolders, HashSet<string> currentFolderNames)
        {
            string? previouslySelectedTag = null;
            string? previouslySelectedAttachment = null;

            if (listBoxFolders.SelectedItem is ListBoxItem selectedItem)
                previouslySelectedTag = selectedItem.Tag?.ToString();

            if (listBoxAttachments.SelectedItem is ListBoxItem attachmentItem)
                previouslySelectedAttachment = attachmentItem.Content?.ToString();

            // Pobierz aktualną mapę elementów
            var currentItems = listBoxFolders.Items.Cast<ListBoxItem>()
                .Where(item => item.Tag?.ToString() != null)
                .ToDictionary(item => item.Tag!.ToString()!, item => item, StringComparer.OrdinalIgnoreCase);

            // Licznik nieodczytanych zgłoszeń
            int unreadCount = 0;

            // Dodaj lub zaktualizuj elementy
            foreach (var folderPath in activeFolders)
            {
                string folderName = Path.GetFileName(folderPath);
                if (string.IsNullOrEmpty(folderName)) continue;

                string? jsonFile = Directory.GetFiles(folderPath, "*.json").FirstOrDefault();

                if (jsonFile == null) continue;

                Report? report;
                try
                {
                    string jsonText = File.ReadAllText(jsonFile);
                    report = JsonSerializer.Deserialize<Report>(jsonText);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Błąd odczytu JSON dla {folderName}: {ex.Message}");
                    continue;
                }

                if (report == null) continue;

                // Sformatuj wyświetlaną nazwę
                DateTime reportTime = DateTime.Parse(report.timestamp ?? DateTime.Now.ToString());
                string timeFormat = reportTime.Date == DateTime.Today ? "HH:mm" : "dd.MM HH:mm";
                string displayName = $"{report.userName ?? "Nieznany"} | {report.title ?? "Bez tytułu"} | {reportTime.ToString(timeFormat)}";

                bool isUnread = string.IsNullOrEmpty(report.status) || report.status.Equals("new", StringComparison.OrdinalIgnoreCase);
                if (isUnread) unreadCount++;

                // Ustaw kolor tła w zależności od priorytetu
                Brush backgroundColor = GetPriorityColor(report.priority ?? "", isUnread);

                if (currentItems.TryGetValue(folderName, out var existingItem))
                {
                    existingItem.Content = displayName;
                    existingItem.Background = backgroundColor;
                    existingItem.Foreground = Brushes.White;
                    existingItem.FontWeight = isUnread ? FontWeights.SemiBold : FontWeights.Normal;
                }
                else
                {
                    var newItem = new ListBoxItem
                    {
                        Content = displayName,
                        Tag = folderName,
                        Background = backgroundColor,
                        Foreground = Brushes.White,
                        FontWeight = isUnread ? FontWeights.SemiBold : FontWeights.Normal
                    };
                    listBoxFolders.Items.Add(newItem);
                    Debug.WriteLine($"Dodano nowy element do listy: {folderName}");
                }
            }

            // Aktualizuj tytuł z liczbą nieodczytanych
            Dispatcher.Invoke(() =>
            {
                var headerTitle = this.FindName("HeaderTitle") as TextBlock;
                if (headerTitle != null)
                {
                    headerTitle.Text = $"Panel Informatyka - Nieodczytane: {unreadCount}";
                }
            });

            // Przywróć zaznaczenie
            if (previouslySelectedTag != null)
            {
                var itemToSelect = listBoxFolders.Items.Cast<ListBoxItem>()
                    .FirstOrDefault(i => string.Equals(i.Tag?.ToString(), previouslySelectedTag, StringComparison.OrdinalIgnoreCase));

                if (itemToSelect != null)
                {
                    listBoxFolders.SelectedItem = itemToSelect;

                    // Przywróć załączniki
                    if (Directory.Exists(Path.Combine(baseFolderPath, previouslySelectedTag)))
                    {
                        LoadAttachments(previouslySelectedTag);

                        if (previouslySelectedAttachment != null)
                        {
                            // Znajdź odpowiedni ListBoxItem po zawartości
                            foreach (var item in listBoxAttachments.Items)
                            {
                                if (item is ListBoxItem lbItem && lbItem.Content?.ToString() == previouslySelectedAttachment)
                                {
                                    listBoxAttachments.SelectedItem = lbItem;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private Brush GetPriorityColor(string priority, bool isUnread)
        {
            // Jeśli zgłoszenie jest nieodczytane, użyj jaśniejszych kolorów
            if (isUnread)
            {
                return priority?.ToLower() switch
                {
                    "wysoki" => new SolidColorBrush(Color.FromRgb(220, 38, 38)),  // Jasny czerwony
                    "średni" => new SolidColorBrush(Color.FromRgb(250, 204, 21)),  // Jasny żółty (mniej pomarańczowy)
                    "niski" => new SolidColorBrush(Color.FromRgb(59, 130, 246)),  // Jasny niebieski
                    _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))        // Szary
                };
            }
            else
            {
                // Ciemniejsze kolory dla odczytanych
                return priority?.ToLower() switch
                {
                    "wysoki" => new SolidColorBrush(Color.FromRgb(153, 27, 27)),  // Ciemny czerwony
                    "średni" => new SolidColorBrush(Color.FromRgb(202, 138, 4)),   // Ciemny żółty (mniej pomarańczowy)
                    "niski" => new SolidColorBrush(Color.FromRgb(30, 64, 175)),   // Ciemny niebieski
                    _ => new SolidColorBrush(Color.FromRgb(51, 65, 85))           // Ciemny szary
                };
            }
        }

        private void LoadAttachments(string folderName)
        {
            listBoxAttachments.Items.Clear();
            string folderPath = Path.Combine(baseFolderPath, folderName);

            if (!Directory.Exists(folderPath)) return;

            foreach (var file in Directory.GetFiles(folderPath))
            {
                string fileName = Path.GetFileName(file);
                // Pomiń pliki JSON i notes.txt
                if (!file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                    !fileName.Equals("notes.txt", StringComparison.OrdinalIgnoreCase))
                {
                    // Tworzymy ListBoxItem z białym tekstem
                    var item = new ListBoxItem
                    {
                        Content = fileName,
                        Foreground = Brushes.White
                    };
                    listBoxAttachments.Items.Add(item);
                }
            }
        }

        private void PlayNotificationSound()
        {
            try
            {
                SystemSounds.Exclamation.Play();
            }
            catch { }
        }

        private void UstawStatus_Click(object sender, RoutedEventArgs e)
        {
            ProgramStatusWindow statusWindow = new ProgramStatusWindow();
            statusWindow.Owner = this;
            statusWindow.ShowDialog();
        }

        private void listBoxFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ClearDetails();
            if (listBoxFolders.SelectedItem == null) return;

            string? folderName = null;
            if (listBoxFolders.SelectedItem is ListBoxItem lbi)
                folderName = lbi.Tag?.ToString();
            else
                folderName = listBoxFolders.SelectedItem.ToString();

            if (string.IsNullOrEmpty(folderName)) return;

            string folderPath = Path.Combine(baseFolderPath, folderName);

            if (!Directory.Exists(folderPath))
            {
                MessageBox.Show("Folder zgłoszenia już nie istnieje (może został zakończony).");
                ClearDetails();
                return;
            }

            // Oznacz jako odczytane w JSON i zaktualizuj wygląd elementu
            MarkReportAsRead(folderPath);
            if (listBoxFolders.SelectedItem is ListBoxItem selectedItem)
            {
                selectedItem.Foreground = Brushes.White;
                selectedItem.FontWeight = FontWeights.Normal;
            }

            LoadReportDetails(folderPath);
            LoadAttachments(folderName);
            imageViewer.Source = null;
        }

        private void MarkReportAsRead(string folderPath)
        {
            try
            {
                string[] jsonFiles = Directory.GetFiles(folderPath, "*.json");
                if (jsonFiles.Length == 0) return;

                string jsonFile = jsonFiles[0];
                string jsonText = File.ReadAllText(jsonFile);
                var report = JsonSerializer.Deserialize<Report>(jsonText);

                if (report == null) return;

                // Zmień status na "Odczytane" jeśli jest "new"
                if (string.IsNullOrEmpty(report.status) || report.status.Equals("new", StringComparison.OrdinalIgnoreCase))
                {
                    report.status = "Odczytane";

                    // Zapisz zaktualizowany JSON
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string updatedJson = JsonSerializer.Serialize(report, options);
                    File.WriteAllText(jsonFile, updatedJson);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd podczas oznaczania jako odczytane: {ex.Message}");
            }
        }

        private void LoadReportDetails(string folderPath)
        {
            string[] jsonFiles = Directory.GetFiles(folderPath, "*.json");
            if (jsonFiles.Length == 0)
            {
                MessageBox.Show("Brak pliku JSON w folderze.");
                return;
            }

            string jsonFile = jsonFiles[0];
            Report? report;
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

            if (report == null)
            {
                MessageBox.Show("Nie można odczytać danych zgłoszenia.");
                return;
            }

            txtTitle.Text = report.title ?? "";
            txtDescription.Text = report.description ?? "";
            txtPriority.Text = report.priority ?? "";
            txtTimestamp.Text = report.timestamp ?? "";
            txtUserName.Text = report.userName ?? "";
            txtStatus.Text = report.status ?? "";

            // Wczytaj notatki
            LoadNotes(folderPath);
        }

        private void LoadNotes(string folderPath)
        {
            string notesFile = Path.Combine(baseFolderPath, folderPath, "notes.txt");
            if (File.Exists(notesFile))
            {
                try
                {
                    txtNotes.Text = File.ReadAllText(notesFile);
                }
                catch
                {
                    txtNotes.Text = "";
                }
            }
            else
            {
                txtNotes.Text = "";
            }
        }

        private void btnSaveNotes_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxFolders.SelectedItem == null) return;

            string? folderName = listBoxFolders.SelectedItem is ListBoxItem lbi ? lbi.Tag?.ToString() : listBoxFolders.SelectedItem.ToString();
            if (string.IsNullOrEmpty(folderName)) return;

            string folderPath = Path.Combine(baseFolderPath, folderName);
            string notesFile = Path.Combine(folderPath, "notes.txt");

            try
            {
                File.WriteAllText(notesFile, txtNotes.Text);
                MessageBox.Show("Notatka została zapisana.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania notatki: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearDetails()
        {
            txtTitle.Text = "";
            txtDescription.Text = "";
            txtPriority.Text = "";
            txtTimestamp.Text = "";
            txtUserName.Text = "";
            txtStatus.Text = "";
            txtNotes.Text = "";
            listBoxAttachments.Items.Clear();
            imageViewer.Source = null;
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            string? selectedFolder = null;

            if (listBoxFolders.SelectedItem is ListBoxItem lbi)
                selectedFolder = lbi.Tag?.ToString();

            btnRefresh.IsEnabled = false;
            try
            {
                await Task.Run(() =>
                {
                    Dispatcher.Invoke(() => LoadReports());
                });

                if (!string.IsNullOrEmpty(selectedFolder))
                {
                    var item = listBoxFolders.Items
                        .Cast<ListBoxItem>()
                        .FirstOrDefault(i => string.Equals(i.Tag?.ToString(), selectedFolder, StringComparison.OrdinalIgnoreCase));

                    if (item != null)
                    {
                        listBoxFolders.SelectedItem = item;
                    }
                }
            }
            finally
            {
                btnRefresh.IsEnabled = true;
            }
        }

        private void btnMarkDone_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxFolders.SelectedItem == null) return;

            string? folderName = listBoxFolders.SelectedItem is ListBoxItem lbi ? lbi.Tag?.ToString() : listBoxFolders.SelectedItem.ToString();
            if (string.IsNullOrEmpty(folderName)) return;

            string folderPath = Path.Combine(baseFolderPath, folderName);

            // Potwierdzenie zakończenia zgłoszenia
            var result = MessageBox.Show(
                $"Czy na pewno chcesz zakończyć zgłoszenie?\n\nFolder: {folderName}",
                "Potwierdzenie zakończenia zgłoszenia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                string doneFolderPath = folderPath + ".done";
                if (Directory.Exists(doneFolderPath))
                {
                    MessageBox.Show("Folder już oznaczony jako zakończony.");
                    return;
                }

                // Zapisz datę zakończenia przed przeniesieniem
                string completionFile = Path.Combine(folderPath, "completion.txt");
                File.WriteAllText(completionFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                Directory.Move(folderPath, doneFolderPath);

                var itemToRemove = listBoxFolders.Items
                    .Cast<ListBoxItem>()
                    .FirstOrDefault(i => string.Equals(i.Tag?.ToString(), folderName, StringComparison.OrdinalIgnoreCase));

                if (itemToRemove != null)
                {
                    listBoxFolders.Items.Remove(itemToRemove);
                    knownFolders.Remove(folderName);
                }

                ClearDetails();
                MessageBox.Show("Zgłoszenie zostało oznaczone jako zakończone.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
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

            string? folderName = listBoxFolders.SelectedItem is ListBoxItem lbi ? lbi.Tag?.ToString() : listBoxFolders.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(folderName))
            {
                imageViewer.Source = null;
                return;
            }

            string folderPath = Path.Combine(baseFolderPath, folderName);

            // Pobierz nazwę pliku z ListBoxItem
            string? selectedFileName = null;
            if (listBoxAttachments.SelectedItem is ListBoxItem attachmentItem)
            {
                selectedFileName = attachmentItem.Content?.ToString();
            }

            if (string.IsNullOrEmpty(selectedFileName))
            {
                imageViewer.Source = null;
                return;
            }

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

        private void listBoxAttachments_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (listBoxAttachments.SelectedItem == null) return;

            string? folderName = listBoxFolders.SelectedItem is ListBoxItem lbi ? lbi.Tag?.ToString() : listBoxFolders.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(folderName)) return;

            string folderPath = Path.Combine(baseFolderPath, folderName);

            // Pobierz nazwę pliku z ListBoxItem
            string? selectedFileName = null;
            if (listBoxAttachments.SelectedItem is ListBoxItem attachmentItem)
            {
                selectedFileName = attachmentItem.Content?.ToString();
            }

            if (string.IsNullOrEmpty(selectedFileName)) return;

            string selectedFile = Path.Combine(folderPath, selectedFileName);

            if (File.Exists(selectedFile))
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = selectedFile,
                        UseShellExecute = true
                    };
                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Nie można otworzyć pliku: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Plik nie istnieje.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (listBoxFolders.SelectedItem == null) return;

            string? folderName = null;
            if (listBoxFolders.SelectedItem is ListBoxItem lbi)
                folderName = lbi.Tag?.ToString();
            else
                folderName = listBoxFolders.SelectedItem.ToString();

            if (string.IsNullOrEmpty(folderName)) return;

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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                // Przejdź do trybu okna
                this.WindowState = WindowState.Normal;
                this.ResizeMode = ResizeMode.CanResize;

                // Dodaj obramowanie i tytuł okna za pomocą WindowChrome
                var chrome = new System.Windows.Shell.WindowChrome();
                chrome.CaptionHeight = 0;
                chrome.ResizeBorderThickness = new Thickness(5);
                chrome.GlassFrameThickness = new Thickness(1);
                chrome.UseAeroCaptionButtons = false;
                System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);

                // Zmień rozmiar i pozycję
                this.Width = 1200;
                this.Height = 800;
                this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
                this.Top = (SystemParameters.PrimaryScreenHeight - this.Height) / 2;
            }
            else
            {
                // Przejdź do trybu pełnego ekranu
                System.Windows.Shell.WindowChrome.SetWindowChrome(this, null);
                this.ResizeMode = ResizeMode.CanResize;
                this.WindowState = WindowState.Maximized;
                // Poprawne ustawienia dla widoczności paska zadań
                this.MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
                this.MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Pozwól na przeciąganie okna gdy jest w trybie Normal (okienkowym)
            if (this.WindowState == WindowState.Normal && e.ClickCount == 1)
            {
                try
                {
                    this.DragMove();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Błąd przeciągania: {ex.Message}");
                }
            }
            // Podwójne kliknięcie przełącza między trybami
            else if (e.ClickCount == 2)
            {
                MaximizeButton_Click(sender, new RoutedEventArgs());
            }
        }
    }

    public class Report
    {
        public string title { get; set; } = string.Empty;
        public string description { get; set; } = string.Empty;
        public string priority { get; set; } = string.Empty;
        public string timestamp { get; set; } = string.Empty;
        public string userName { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
    }
}
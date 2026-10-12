using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HelpDeskAdmin
{
    public class Config
    {
        public string? StatusFilePath { get; set; }
        public string? ReportFolderPath { get; set; }
        public string? RemoteSupportExePath { get; set; }

        public static Config? Load(string path)
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Config>(json);
        }
    }

    public partial class ProgramStatusWindow : Window
    {
        private readonly string jsonPath;
        private Dictionary<string, object>? programStatus;

        public ProgramStatusWindow()
        {
            InitializeComponent();
            var exeFolder = AppDomain.CurrentDomain.BaseDirectory;
            var configPath = Path.Combine(exeFolder, "config.json");
            var config = Config.Load(configPath);
            jsonPath = config?.StatusFilePath ?? "";
            LoadStatus();
        }

        private void LoadStatus()
        {
            string json = File.ReadAllText(jsonPath);
            programStatus = JsonSerializer.Deserialize<Dictionary<string, object>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            ProgramStack.Children.Clear();

            if (programStatus == null) return;

            foreach (var kvp in programStatus)
            {
                if (kvp.Value is JsonElement element)
                {
                    AddGroupWithControls(kvp.Key, element);
                }
            }

            // Przycisk dodawania nowej grupy na końcu
            AddNewGroupButton();
        }

        private void AddGroupWithControls(string groupName, JsonElement element)
        {
            // Panel dla grupy
            var groupPanel = new StackPanel
            {
                Margin = new Thickness(0, 15, 0, 5),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 75)),
                Tag = groupName
            };

            // Header grupy z przyciskami
            var headerPanel = new DockPanel
            {
                Margin = new Thickness(5)
            };

            // Przyciski akcji (po prawej)
            var actionButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(actionButtons, Dock.Right);

            // Przycisk dodawania elementu do grupy (jeśli grupa jest obiektem)
            if (element.ValueKind == JsonValueKind.Object)
            {
                var btnAddItem = new Button
                {
                    Content = "➕ Element",
                    Width = 80,
                    Height = 25,
                    Margin = new Thickness(2),
                    Background = new SolidColorBrush(Color.FromRgb(102, 126, 234)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                btnAddItem.Click += (s, e) => AddItemToGroup_Click(groupPanel, groupName);
                actionButtons.Children.Add(btnAddItem);
            }

            // Przycisk usuwania grupy
            var btnDeleteGroup = new Button
            {
                Content = "🗑️ Usuń",
                Width = 70,
                Height = 25,
                Margin = new Thickness(2),
                Background = new SolidColorBrush(Color.FromRgb(220, 38, 38)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnDeleteGroup.Click += (s, e) => DeleteGroup_Click(groupPanel);
            actionButtons.Children.Add(btnDeleteGroup);

            headerPanel.Children.Add(actionButtons);

            // Nazwa grupy (edytowalna)
            var txtGroupName = new TextBox
            {
                Text = groupName,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 90)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(102, 126, 234)),
                Padding = new Thickness(5),
                Tag = "GroupName"
            };
            headerPanel.Children.Add(txtGroupName);

            groupPanel.Children.Add(headerPanel);

            // Elementy grupy
            var itemsPanel = new StackPanel
            {
                Margin = new Thickness(20, 5, 5, 5),
                Tag = "ItemsPanel"
            };

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var sub in element.EnumerateObject())
                {
                    AddItemToPanel(itemsPanel, sub.Name, sub.Value.GetBoolean());
                }
            }
            else if (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
            {
                AddItemToPanel(itemsPanel, groupName, element.GetBoolean());
            }

            groupPanel.Children.Add(itemsPanel);
            ProgramStack.Children.Add(groupPanel);
        }

        private void AddItemToPanel(StackPanel itemsPanel, string itemName, bool isChecked)
        {
            var itemPanel = new DockPanel
            {
                Margin = new Thickness(0, 2, 0, 2)
            };

            // Przycisk usuwania elementu
            var btnDelete = new Button
            {
                Content = "❌",
                Width = 25,
                Height = 25,
                Margin = new Thickness(0, 0, 5, 0),
                Background = new SolidColorBrush(Color.FromRgb(185, 28, 28)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnDelete.Click += (s, e) => itemsPanel.Children.Remove(itemPanel);
            DockPanel.SetDock(btnDelete, Dock.Right);
            itemPanel.Children.Add(btnDelete);

            // CheckBox
            var checkbox = new CheckBox
            {
                Content = itemName,
                IsChecked = isChecked,
                Foreground = Brushes.White,
                Tag = itemName
            };
            itemPanel.Children.Add(checkbox);

            itemsPanel.Children.Add(itemPanel);
        }

        private void AddItemToGroup_Click(StackPanel groupPanel, string groupName)
        {
            // Dialog do dodawania nowego elementu
            var dialog = new Window
            {
                Title = $"Dodaj element do {groupName}",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var stackPanel = new StackPanel { Margin = new Thickness(10) };

            stackPanel.Children.Add(new TextBlock
            {
                Text = "Nazwa elementu:",
                Margin = new Thickness(0, 0, 0, 5)
            });

            var txtName = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(txtName);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnOK = new Button
            {
                Content = "Dodaj",
                Width = 75,
                Margin = new Thickness(0, 0, 5, 0)
            };
            btnOK.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtName.Text))
                {
                    // Znajdź panel z elementami
                    StackPanel? itemsPanel = null;
                    foreach (var child in groupPanel.Children)
                    {
                        if (child is StackPanel sp && sp.Tag?.ToString() == "ItemsPanel")
                        {
                            itemsPanel = sp;
                            break;
                        }
                    }

                    if (itemsPanel != null)
                    {
                        AddItemToPanel(itemsPanel, txtName.Text, true);
                    }

                    dialog.DialogResult = true;
                }
            };
            btnPanel.Children.Add(btnOK);

            var btnCancel = new Button
            {
                Content = "Anuluj",
                Width = 75
            };
            btnCancel.Click += (s, e) => dialog.DialogResult = false;
            btnPanel.Children.Add(btnCancel);

            stackPanel.Children.Add(btnPanel);
            dialog.Content = stackPanel;

            dialog.ShowDialog();
        }

        private void DeleteGroup_Click(StackPanel groupPanel)
        {
            var result = MessageBox.Show(
                "Czy na pewno chcesz usunąć tę grupę?",
                "Potwierdzenie usunięcia",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ProgramStack.Children.Remove(groupPanel);
            }
        }

        private void AddNewGroupButton()
        {
            var btnAddGroup = new Button
            {
                Content = "➕ Dodaj nową grupę",
                Height = 40,
                Margin = new Thickness(0, 20, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(34, 197, 94)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnAddGroup.Click += AddNewGroup_Click;
            ProgramStack.Children.Add(btnAddGroup);
        }

        private void AddNewGroup_Click(object sender, RoutedEventArgs e)
        {
            // Dialog do dodawania nowej grupy
            var dialog = new Window
            {
                Title = "Dodaj nową grupę",
                Width = 350,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var stackPanel = new StackPanel { Margin = new Thickness(10) };

            stackPanel.Children.Add(new TextBlock
            {
                Text = "Nazwa grupy:",
                Margin = new Thickness(0, 0, 0, 5)
            });

            var txtName = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(txtName);

            stackPanel.Children.Add(new TextBlock
            {
                Text = "Typ grupy:",
                Margin = new Thickness(0, 0, 0, 5)
            });

            var cmbType = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 10)
            };
            cmbType.Items.Add(new ComboBoxItem { Content = "Prosta (checkbox)", Tag = "simple" });
            cmbType.Items.Add(new ComboBoxItem { Content = "Złożona (z elementami)", Tag = "complex" });
            cmbType.SelectedIndex = 0;
            stackPanel.Children.Add(cmbType);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnOK = new Button
            {
                Content = "Dodaj",
                Width = 75,
                Margin = new Thickness(0, 0, 5, 0)
            };
            btnOK.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txtName.Text))
                {
                    var selectedItem = cmbType.SelectedItem as ComboBoxItem;
                    bool isComplex = selectedItem?.Tag?.ToString() == "complex";

                    // Utwórz JsonElement dla nowej grupy
                    JsonElement newElement;
                    if (isComplex)
                    {
                        // Pusta grupa złożona
                        newElement = JsonDocument.Parse("{}").RootElement;
                    }
                    else
                    {
                        // Prosta grupa z wartością true
                        newElement = JsonDocument.Parse("true").RootElement;
                    }

                    // Usuń przycisk "Dodaj nową grupę" tymczasowo
                    UIElement? addButton = null;
                    foreach (UIElement child in ProgramStack.Children)
                    {
                        if (child is Button btn && btn.Content?.ToString()?.Contains("Dodaj nową grupę") == true)
                        {
                            addButton = child;
                            break;
                        }
                    }
                    if (addButton != null)
                    {
                        ProgramStack.Children.Remove(addButton);
                    }

                    // Dodaj nową grupę
                    AddGroupWithControls(txtName.Text, newElement);

                    // Przywróć przycisk "Dodaj nową grupę"
                    AddNewGroupButton();

                    dialog.DialogResult = true;
                }
            };
            btnPanel.Children.Add(btnOK);

            var btnCancel = new Button
            {
                Content = "Anuluj",
                Width = 75
            };
            btnCancel.Click += (s, e) => dialog.DialogResult = false;
            btnPanel.Children.Add(btnCancel);

            stackPanel.Children.Add(btnPanel);
            dialog.Content = stackPanel;

            dialog.ShowDialog();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var newStatus = new Dictionary<string, object>();

            foreach (UIElement element in ProgramStack.Children)
            {
                if (element is StackPanel groupPanel && groupPanel.Tag != null)
                {
                    // Znajdź nazwę grupy
                    string? groupName = null;
                    StackPanel? itemsPanel = null;

                    foreach (var child in groupPanel.Children)
                    {
                        if (child is DockPanel dockPanel)
                        {
                            foreach (var dockChild in dockPanel.Children)
                            {
                                if (dockChild is TextBox txt && txt.Tag?.ToString() == "GroupName")
                                {
                                    groupName = txt.Text;
                                    break;
                                }
                            }
                        }
                        else if (child is StackPanel sp && sp.Tag?.ToString() == "ItemsPanel")
                        {
                            itemsPanel = sp;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(groupName) || itemsPanel == null) continue;

                    // Sprawdź czy grupa jest prosta czy złożona
                    if (itemsPanel.Children.Count == 1)
                    {
                        // Sprawdź czy jedyny element to checkbox z tą samą nazwą co grupa
                        var firstItem = itemsPanel.Children[0] as DockPanel;
                        if (firstItem != null)
                        {
                            foreach (var child in firstItem.Children)
                            {
                                if (child is CheckBox cb)
                                {
                                    if (cb.Content?.ToString() == groupName)
                                    {
                                        // Prosta grupa
                                        newStatus[groupName] = cb.IsChecked == true;
                                    }
                                    else
                                    {
                                        // Złożona grupa z jednym elementem
                                        var dict = new Dictionary<string, bool>();
                                        dict[cb.Content?.ToString() ?? ""] = cb.IsChecked == true;
                                        newStatus[groupName] = dict;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Złożona grupa
                        var dict = new Dictionary<string, bool>();
                        foreach (var item in itemsPanel.Children)
                        {
                            if (item is DockPanel dockPanel)
                            {
                                foreach (var dockChild in dockPanel.Children)
                                {
                                    if (dockChild is CheckBox cb)
                                    {
                                        dict[cb.Content?.ToString() ?? ""] = cb.IsChecked == true;
                                        break;
                                    }
                                }
                            }
                        }
                        newStatus[groupName] = dict;
                    }
                }
            }

            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonOut = JsonSerializer.Serialize(newStatus, options);
                File.WriteAllText(jsonPath, jsonOut);

                MessageBox.Show("Zapisano zmiany.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
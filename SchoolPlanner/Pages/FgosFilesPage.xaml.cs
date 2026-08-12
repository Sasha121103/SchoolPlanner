using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using SchoolPlanner.Database;

namespace SchoolPlanner.Pages
{
    public partial class FgosFilesPage 
    {
        private DbHelper dbHelper;
        private User currentUser;
        private List<DbHelper.FgosFile> allFiles;
        private List<Subject> allSubjects;

        public FgosFilesPage()
        {
            try
            {
                InitializeComponent();
                dbHelper = new DbHelper();
                currentUser = App.CurrentUser;

                // Проверяем права доступа (только для УВР)
                if (currentUser.Role != UserRole.Admin)
                {
                    MessageBox.Show("Доступ запрещен. Только для УВР.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    NavigationService.GoBack();
                    return;
                }

                Loaded += Page_Loaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSubjects();
            LoadFiles();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void LoadSubjects()
        {
            try
            {
                allSubjects = dbHelper.GetAllSubjects();
                var subjectNames = allSubjects.Select(s => s.Name).ToList();
                subjectNames.Insert(0, "Все предметы");
                cmbSubjectFilter.ItemsSource = subjectNames;
                cmbSubjectFilter.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки предметов: {ex.Message}");
            }
        }

        private void LoadFiles()
        {
            try
            {
                int? subjectId = null;
                string gradeLevel = null;

                // Фильтр по предмету
                if (cmbSubjectFilter.SelectedItem != null &&
                    cmbSubjectFilter.SelectedItem.ToString() != "Все предметы")
                {
                    string subjectName = cmbSubjectFilter.SelectedItem.ToString();
                    var subject = allSubjects.FirstOrDefault(s => s.Name == subjectName);
                    if (subject != null)
                        subjectId = subject.Id;
                }

                // Фильтр по классу
                if (cmbGradeFilter.SelectedItem != null &&
                    cmbGradeFilter.SelectedItem.ToString() != "Все классы")
                {
                    gradeLevel = cmbGradeFilter.SelectedItem.ToString();
                }

                allFiles = dbHelper.GetAllFgosFiles(subjectId, gradeLevel);

                if (allFiles != null && allFiles.Any())
                {
                    lvFgosFiles.ItemsSource = allFiles;
                    lvFgosFiles.Visibility = Visibility.Visible;
                    txtNoFiles.Visibility = Visibility.Collapsed;
                }
                else
                {
                    lvFgosFiles.ItemsSource = null;
                    lvFgosFiles.Visibility = Visibility.Collapsed;
                    txtNoFiles.Visibility = Visibility.Visible;
                }

                // Обновляем статистику
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки файлов: {ex.Message}");
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                if (allFiles != null)
                {
                    txtFileCount.Text = $"Всего файлов: {allFiles.Count}";

                    long totalSize = allFiles.Sum(f => (long)f.FileSize);
                    string sizeStr;
                    if (totalSize < 1024)
                        sizeStr = $"{totalSize} Б";
                    else if (totalSize < 1024 * 1024)
                        sizeStr = $"{totalSize / 1024.0:F1} КБ";
                    else
                        sizeStr = $"{totalSize / (1024.0 * 1024.0):F1} МБ";

                    txtTotalSize.Text = $"Общий размер: {sizeStr}";
                }
                else
                {
                    txtFileCount.Text = "Всего файлов: 0";
                    txtTotalSize.Text = "Общий размер: 0 МБ";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления статистики: {ex.Message}");
            }
        }

        private void cmbSubjectFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadFiles();
        }

        private void cmbGradeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadFiles();
        }

        private void btnAddFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "PDF файлы (*.pdf)|*.pdf|Word файлы (*.doc;*.docx)|*.doc;*.docx|Все файлы (*.*)|*.*",
                Title = "Выберите файл ФГОС"
            };

            if (openDialog.ShowDialog() == true)
            {
                // Открываем диалог с информацией о файле
                ShowFileInfoDialog(openDialog.FileName);
            }
        }

        private void ShowFileInfoDialog(string filePath)
        {
            var dialog = new Window
            {
                Title = "Информация о файле ФГОС",
                Width = 450,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                ResizeMode = ResizeMode.NoResize
            };

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel panel = new StackPanel { Margin = new Thickness(20) };

            FileInfo fileInfo = new FileInfo(filePath);

            // Заголовок
            panel.Children.Add(new TextBlock
            {
                Text = "📄 Добавление файла ФГОС",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Margin = new Thickness(0, 0, 0, 20)
            });

            // Имя файла
            panel.Children.Add(new TextBlock
            {
                Text = "Имя файла:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5)
            });
            var txtFileName = new TextBox
            {
                Text = fileInfo.Name,
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                IsReadOnly = true,
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
            };
            panel.Children.Add(txtFileName);

            // Размер файла
            panel.Children.Add(new TextBlock
            {
                Text = "Размер:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5)
            });
            var txtFileSize = new TextBox
            {
                Text = FormatFileSize((int)fileInfo.Length),
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                IsReadOnly = true,
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
            };
            panel.Children.Add(txtFileSize);

            // Предмет
            panel.Children.Add(new TextBlock
            {
                Text = "Предмет:*",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5)
            });
            var cmbSubject = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                ItemsSource = allSubjects.Select(s => s.Name).ToList()
            };
            if (cmbSubject.Items.Count > 0)
                cmbSubject.SelectedIndex = 0;
            panel.Children.Add(cmbSubject);

            // Класс
            panel.Children.Add(new TextBlock
            {
                Text = "Класс:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5)
            });
            var cmbGrade = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10)
            };
            cmbGrade.Items.Add("");
            for (int i = 5; i <= 11; i++)
                cmbGrade.Items.Add(i.ToString());
            cmbGrade.SelectedIndex = 0;
            panel.Children.Add(cmbGrade);

            // Вариант ФГОС
            panel.Children.Add(new TextBlock
            {
                Text = "Вариант ФГОС:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5)
            });
            var cmbVariant = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10)
            };
            cmbVariant.Items.Add("");
            cmbVariant.Items.Add("2.2.2");
            cmbVariant.Items.Add("1");
            cmbVariant.SelectedIndex = 0;
            panel.Children.Add(cmbVariant);

            // Описание
            panel.Children.Add(new TextBlock
            {
                Text = "Описание:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5)
            });
            var txtDescription = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                Height = 60
            };
            panel.Children.Add(txtDescription);

            // Подсказка
            panel.Children.Add(new TextBlock
            {
                Text = "* - обязательные поля",
                Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 15)
            });

            // Кнопки
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Button btnSave = new Button
            {
                Content = "Сохранить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Margin = new Thickness(0, 0, 10, 0),
                Width = 100
            };

            // ИСПРАВЛЕНО: уникальные имена параметров
            btnSave.Click += (saveSender, saveArgs) =>
            {
                try
                {
                    string subjectName = cmbSubject.SelectedItem?.ToString();
                    if (string.IsNullOrEmpty(subjectName))
                    {
                        MessageBox.Show("Выберите предмет", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var subject = allSubjects.First(s => s.Name == subjectName);

                    // Копируем файл в папку приложения
                    string appFolder = AppDomain.CurrentDomain.BaseDirectory;
                    string fgosFolder = Path.Combine(appFolder, "FGOS_Files");
                    if (!Directory.Exists(fgosFolder))
                        Directory.CreateDirectory(fgosFolder);

                    string destFileName = $"{DateTime.Now:yyyyMMddHHmmss}_{fileInfo.Name}";
                    string destPath = Path.Combine(fgosFolder, destFileName);

                    File.Copy(filePath, destPath);

                    var fgosFile = new DbHelper.FgosFile
                    {
                        SubjectId = subject.Id,
                        SubjectName = subjectName,
                        FileName = fileInfo.Name,
                        FilePath = destPath,
                        FileSize = (int)fileInfo.Length,
                        FileType = fileInfo.Extension,
                        GradeLevel = cmbGrade.SelectedItem?.ToString() ?? "",
                        Variant = cmbVariant.SelectedItem?.ToString() ?? "",
                        Description = txtDescription.Text,
                        UploadedBy = currentUser.Id
                    };

                    int id = dbHelper.AddFgosFile(fgosFile);
                    if (id > 0)
                    {
                        MessageBox.Show("Файл успешно добавлен", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        dialog.Close();
                        LoadFiles();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения файла: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            buttonPanel.Children.Add(btnSave);

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Width = 100
            };

            // ИСПРАВЛЕНО: уникальные имена параметров
            btnCancel.Click += (cancelSender, cancelArgs) => dialog.Close();
            buttonPanel.Children.Add(btnCancel);

            panel.Children.Add(buttonPanel);
            scrollViewer.Content = panel;
            dialog.Content = scrollViewer;
            dialog.ShowDialog();
        }

        private string FormatFileSize(int bytes)
        {
            if (bytes < 1024) return $"{bytes} Б";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} КБ";
            return $"{bytes / (1024.0 * 1024.0):F1} МБ";
        }

        private void btnDownloadFile_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var file = button?.Tag as DbHelper.FgosFile;
            if (file == null) return;

            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = $"{file.FileType} файлы (*{file.FileType})|*{file.FileType}|Все файлы (*.*)|*.*",
                    FileName = file.FileName
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.Copy(file.FilePath, saveDialog.FileName, true);

                    // Увеличиваем счетчик скачиваний
                    dbHelper.IncrementFgosDownloadCount(file.Id);

                    MessageBox.Show("Файл успешно скачан", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // Обновляем список для отображения нового счетчика
                    LoadFiles();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка скачивания файла: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDeleteFile_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var file = button?.Tag as DbHelper.FgosFile;
            if (file == null) return;

            var result = MessageBox.Show($"Вы действительно хотите удалить файл {file.FileName}?",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    bool deleted = dbHelper.DeleteFgosFile(file.Id);
                    if (deleted)
                    {
                        MessageBox.Show("Файл удален", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadFiles();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления файла: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
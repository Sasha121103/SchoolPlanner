using Microsoft.Win32;
using SchoolPlanner.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static SchoolPlanner.Database.DbHelper;
using SchoolPlanner.Database;

namespace SchoolPlanner.Pages
{
    public partial class StudentsPage 
    {
        private DbHelper dbHelper;
        private User currentUser;
        private List<Student> allStudents;
        private Student selectedStudent;
        private List<Grade> studentGrades;
        private List<Attendance> studentAttendance;

        public StudentsPage()
        {
            try
            {
                InitializeComponent();
                dbHelper = new DbHelper();
                currentUser = App.CurrentUser;

                allStudents = new List<Student>();
                studentGrades = new List<Grade>();
                studentAttendance = new List<Attendance>();

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
            try
            {
                if (!IsLoaded) return;

                SetupRoleBasedUI();
                LoadClasses();
                LoadStudents();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка Page_Loaded: {ex.Message}");
                MessageBox.Show($"Ошибка загрузки страницы: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupRoleBasedUI()
        {
            try
            {
                if (currentUser == null) return;

                if (currentUser.Role == UserRole.Admin)
                {
                    txtRoleIndicator.Text = "👤 УВР";
                    btnAddStudent.Visibility = Visibility.Visible;
                    btnEditStudent.Visibility = Visibility.Visible;
                    btnDeleteStudent.Visibility = Visibility.Visible;
                    btnImportStudents.Visibility = Visibility.Visible;
                    btnExportStudents.Visibility = Visibility.Visible;
                }
                else
                {
                    txtRoleIndicator.Text = "👤 Учитель";
                    btnAddStudent.Visibility = Visibility.Collapsed;
                    btnEditStudent.Visibility = Visibility.Collapsed;
                    btnDeleteStudent.Visibility = Visibility.Collapsed;
                    btnImportStudents.Visibility = Visibility.Collapsed;
                    btnExportStudents.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка SetupRoleBasedUI: {ex.Message}");
            }
        }

        private void LoadClasses()
        {
            try
            {
                if (cmbClassFilter == null) return;

                var classes = dbHelper.GetAllClasses() ?? new List<SchoolClass>();
                cmbClassFilter.Items.Clear();
                cmbClassFilter.Items.Add("📌 Все классы");
                foreach (var c in classes.OrderBy(c => c.Name))
                {
                    cmbClassFilter.Items.Add(c.Name);
                }
                cmbClassFilter.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadClasses: {ex.Message}");
            }
        }

        private void LoadStudents()
        {
            try
            {
                if (lvStudents == null) return;

                allStudents = dbHelper.GetAllStudents() ?? new List<Student>();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadStudents: {ex.Message}");
                allStudents = new List<Student>();
                if (lvStudents != null)
                    lvStudents.ItemsSource = allStudents;
                if (txtStats != null)
                    txtStats.Text = "Всего учеников: 0";
            }
        }

        private void ApplyFilter()
        {
            try
            {
                if (lvStudents == null) return;

                if (allStudents == null)
                    allStudents = new List<Student>();

                string filter = cmbClassFilter?.SelectedItem?.ToString() ?? "📌 Все классы";
                if (filter.StartsWith("📌 "))
                    filter = filter.Substring(3);

                List<Student> filtered;

                if (filter == "Все классы")
                {
                    filtered = allStudents;
                }
                else
                {
                    filtered = allStudents.Where(s => s.ClassName == filter).ToList();
                }

                lvStudents.ItemsSource = filtered;
                if (txtStats != null)
                    txtStats.Text = $"Всего учеников: {filtered.Count}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка ApplyFilter: {ex.Message}");
            }
        }

        private void cmbClassFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void lvStudents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (lvStudents == null) return;

                if (lvStudents.SelectedItem is Student student)
                {
                    selectedStudent = student;
                    ShowStudentDetails(student);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка SelectionChanged: {ex.Message}");
            }
        }

        private void ShowStudentDetails(Student student)
        {
            try
            {
                if (student == null) return;

                txtStudentName.Text = student.FullName ?? "Неизвестно";
                txtClass.Text = student.ClassName ?? "—";
                txtBirthDate.Text = student.BirthDate?.ToString("dd.MM.yyyy") ?? "—";

                // Загружаем краткую статистику
                LoadStatistics(student.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка ShowStudentDetails: {ex.Message}");
            }
        }

        private void LoadStatistics(int studentId)
        {
            try
            {
                // Оценки
                studentGrades = dbHelper.GetGradesForStudent(studentId) ?? new List<Grade>();

                if (studentGrades.Any())
                {
                    double avg = studentGrades.Average(g => g.Value);
                    txtAvgGrade.Text = avg.ToString("F2");
                    txtTotalGrades.Text = studentGrades.Count.ToString();
                }
                else
                {
                    txtAvgGrade.Text = "—";
                    txtTotalGrades.Text = "0";
                }

                // Посещаемость
                studentAttendance = dbHelper.GetAttendanceForStudent(studentId) ?? new List<Attendance>();

                if (studentAttendance.Any())
                {
                    int present = studentAttendance.Count(a => a.Status == AttendanceStatus.Present);
                    int total = studentAttendance.Count;
                    int percent = total > 0 ? (present * 100 / total) : 0;
                    txtAttendancePercent.Text = $"{percent}%";
                }
                else
                {
                    txtAttendancePercent.Text = "0%";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadStatistics: {ex.Message}");
                txtAvgGrade.Text = "—";
                txtTotalGrades.Text = "0";
                txtAttendancePercent.Text = "0%";
            }
        }

        // ============================================================
        // ОТКРЫТИЕ ЖУРНАЛА
        // ============================================================

        private void btnOpenJournal_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStudent == null)
            {
                MessageBox.Show("Выберите ученика для открытия журнала", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Открываем журнал для класса
                var journalWindow = new JournalWindow(selectedStudent.ClassName, dbHelper);
                journalWindow.Owner = Window.GetWindow(this);
                journalWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия журнала: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // УПРАВЛЕНИЕ УЧЕНИКАМИ
        // ============================================================

        private void btnAddStudent_Click(object sender, RoutedEventArgs e)
        {
            OpenStudentDialog(null);
        }

        private void btnEditStudent_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStudent == null)
            {
                MessageBox.Show("Выберите ученика", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            OpenStudentDialog(selectedStudent);
        }

        private int GetClassId(string className)
        {
            try
            {
                var classes = dbHelper.GetAllClasses() ?? new List<SchoolClass>();
                var cls = classes.FirstOrDefault(c => c.Name == className);
                return cls?.Id ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        private void OpenStudentDialog(Student student)
        {
            try
            {
                var dialog = new Window
                {
                    Title = student == null ? "➕ Добавление ученика" : "✏ Редактирование ученика",
                    Width = 400,
                    Height = 550,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
                };

                var panel = new StackPanel { Margin = new Thickness(20) };

                panel.Children.Add(new TextBlock
                {
                    Text = student == null ? "Добавление ученика" : "Редактирование ученика",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    Margin = new Thickness(0, 0, 0, 20)
                });

                // ФИО
                panel.Children.Add(new TextBlock { Text = "ФИО:*", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var txtName = new TextBox
                {
                    Text = student?.FullName ?? "",
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10)
                };
                panel.Children.Add(txtName);

                // Класс
                panel.Children.Add(new TextBlock { Text = "Класс:*", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var cmbClass = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10)
                };
                var classes = dbHelper.GetAllClasses() ?? new List<SchoolClass>();
                foreach (var c in classes)
                    cmbClass.Items.Add(c.Name);
                if (student != null) cmbClass.Text = student.ClassName;
                else if (cmbClass.Items.Count > 0) cmbClass.SelectedIndex = 0;
                panel.Children.Add(cmbClass);

                // Дата рождения
                panel.Children.Add(new TextBlock { Text = "Дата рождения:", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var dpBirth = new DatePicker
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    SelectedDate = student?.BirthDate
                };
                panel.Children.Add(dpBirth);

                // Пол
                panel.Children.Add(new TextBlock { Text = "Пол:", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var cmbGender = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10)
                };
                cmbGender.Items.Add("М");
                cmbGender.Items.Add("Ж");
                if (student != null && !string.IsNullOrEmpty(student.Gender))
                    cmbGender.Text = student.Gender;
                else
                    cmbGender.SelectedIndex = 0;
                panel.Children.Add(cmbGender);

                // Родитель
                panel.Children.Add(new TextBlock { Text = "Родитель:", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var txtParent = new TextBox
                {
                    Text = student?.ParentName ?? "",
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10)
                };
                panel.Children.Add(txtParent);

                // Телефон родителя
                panel.Children.Add(new TextBlock { Text = "Телефон родителя:", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var txtPhone = new TextBox
                {
                    Text = student?.ParentPhone ?? "",
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10)
                };
                panel.Children.Add(txtPhone);

                // Адрес
                panel.Children.Add(new TextBlock { Text = "Адрес:", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var txtAddress = new TextBox
                {
                    Text = student?.Address ?? "",
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10)
                };
                panel.Children.Add(txtAddress);

                // Кнопки
                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

                Button btnSave = new Button
                {
                    Content = "Сохранить",
                    Style = (Style)FindResource("MainButtonStyle"),
                    Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    Margin = new Thickness(0, 0, 10, 0),
                    Width = 100
                };
                btnSave.Click += (s, args) =>
                {
                    try
                    {
                        if (string.IsNullOrEmpty(txtName.Text))
                        {
                            MessageBox.Show("Введите ФИО", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        if (string.IsNullOrEmpty(cmbClass.Text))
                        {
                            MessageBox.Show("Выберите класс", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        int classId = GetClassId(cmbClass.Text);

                        var newStudent = new Student
                        {
                            FullName = txtName.Text,
                            ClassId = classId,
                            ClassName = cmbClass.Text,
                            BirthDate = dpBirth.SelectedDate,
                            Gender = cmbGender.Text,
                            ParentName = txtParent.Text,
                            ParentPhone = txtPhone.Text,
                            Address = txtAddress.Text
                        };

                        bool success;
                        if (student != null)
                        {
                            newStudent.Id = student.Id;
                            success = dbHelper.UpdateStudent(newStudent);
                        }
                        else
                        {
                            int result = dbHelper.AddStudent(newStudent);
                            success = result > 0;
                        }

                        if (success)
                        {
                            dialog.Close();
                            LoadStudents();
                            MessageBox.Show("Ученик сохранен", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("Ошибка сохранения", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                btnCancel.Click += (s, args) => dialog.Close();
                buttonPanel.Children.Add(btnCancel);

                panel.Children.Add(buttonPanel);
                dialog.Content = panel;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия диалога: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDeleteStudent_Click(object sender, RoutedEventArgs e)
        {
            if (selectedStudent == null)
            {
                MessageBox.Show("Выберите ученика", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Удалить ученика {selectedStudent.FullName}?\n\nВсе оценки и записи о посещаемости будут удалены.",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    if (dbHelper.HardDeleteStudent(selectedStudent.Id))
                    {
                        selectedStudent = null;
                        LoadStudents();
                        ClearStudentDetails();
                        MessageBox.Show("Ученик удален", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Ошибка удаления", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ClearStudentDetails()
        {
            txtStudentName.Text = "Выберите ученика";
            txtClass.Text = "—";
            txtBirthDate.Text = "—";
            txtAvgGrade.Text = "—";
            txtTotalGrades.Text = "0";
            txtAttendancePercent.Text = "0%";
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (NavigationService != null && NavigationService.CanGoBack)
                    NavigationService.GoBack();
                else
                {
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        mainWindow.MainFrame.Navigate(new MainMenuPage());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка навигации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // ИМПОРТ УЧЕНИКОВ
        // ============================================================

        private void btnImportStudents_Click(object sender, RoutedEventArgs e)
        {
            ImportStudentsFromCSV();
        }

        private void ImportStudentsFromCSV()
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Title = "Выберите CSV файл с учениками",
                    Filter = "CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*",
                    DefaultExt = ".csv"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var lines = File.ReadAllLines(openFileDialog.FileName, Encoding.UTF8);
                    var students = new List<Student>();
                    int imported = 0;
                    int errors = 0;

                    int startIndex = 0;
                    if (lines.Length > 0 && (lines[0].Contains("ФИО") || lines[0].Contains("Класс")))
                        startIndex = 1;

                    for (int i = startIndex; i < lines.Length; i++)
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(lines[i])) continue;

                            var parts = lines[i].Split(new[] { ';', ',' }, StringSplitOptions.None);
                            if (parts.Length < 2) continue;

                            var student = new Student
                            {
                                FullName = parts[0].Trim(),
                                ClassName = parts.Length > 1 ? parts[1].Trim() : "",
                                BirthDate = parts.Length > 2 && !string.IsNullOrEmpty(parts[2]) ? DateTime.Parse(parts[2].Trim()) : (DateTime?)null,
                                Gender = parts.Length > 3 ? parts[3].Trim() : "",
                                ParentName = parts.Length > 4 ? parts[4].Trim() : "",
                                ParentPhone = parts.Length > 5 ? parts[5].Trim() : "",
                                Address = parts.Length > 6 ? parts[6].Trim() : ""
                            };

                            if (!string.IsNullOrEmpty(student.FullName) && !string.IsNullOrEmpty(student.ClassName))
                            {
                                students.Add(student);
                                imported++;
                            }
                            else
                            {
                                errors++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors++;
                            System.Diagnostics.Debug.WriteLine($"Ошибка импорта строки {i + 1}: {ex.Message}");
                        }
                    }

                    if (students.Any())
                    {
                        // Получаем ID классов
                        var classes = dbHelper.GetAllClasses() ?? new List<SchoolClass>();
                        foreach (var student in students)
                        {
                            var cls = classes.FirstOrDefault(c => c.Name == student.ClassName);
                            student.ClassId = cls?.Id ?? 0;
                        }

                        int result = dbHelper.ImportStudents(students);
                        MessageBox.Show($"Импортировано: {result} учеников\n" +
                                      $"Ошибок: {errors}\n" +
                                      $"Всего обработано: {lines.Length - startIndex}",
                                      "Импорт завершен",
                                      MessageBoxButton.OK,
                                      result > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
                        LoadStudents();
                    }
                    else
                    {
                        MessageBox.Show("Не найдено данных для импорта.\n\n" +
                                      "Формат CSV:\n" +
                                      "ФИО;Класс;Дата рождения;Пол;Родитель;Телефон;Адрес",
                                      "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка импорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Автоматический импорт с примером данных
        /// </summary>
        public void ImportStudentsAutomatically()
        {
            try
            {
                var students = new List<Student>
                {
                    new Student { FullName = "Иванов Иван Иванович", ClassName = "5а", BirthDate = new DateTime(2012, 1, 15), Gender = "М", ParentName = "Иванов И.И.", ParentPhone = "+7-999-123-45-67" },
                    new Student { FullName = "Петрова Анна Сергеевна", ClassName = "5а", BirthDate = new DateTime(2012, 3, 20), Gender = "Ж", ParentName = "Петрова С.И.", ParentPhone = "+7-999-234-56-78" },
                    new Student { FullName = "Сидоров Алексей Петрович", ClassName = "5б", BirthDate = new DateTime(2012, 5, 10), Gender = "М", ParentName = "Сидоров П.А.", ParentPhone = "+7-999-345-67-89" },
                    new Student { FullName = "Козлова Екатерина Дмитриевна", ClassName = "5б", BirthDate = new DateTime(2012, 7, 25), Gender = "Ж", ParentName = "Козлова Д.В.", ParentPhone = "+7-999-456-78-90" },
                    new Student { FullName = "Смирнов Олег Николаевич", ClassName = "5в", BirthDate = new DateTime(2012, 9, 12), Gender = "М", ParentName = "Смирнов Н.О.", ParentPhone = "+7-999-567-89-01" }
                };

                // Получаем ID классов
                var classes = dbHelper.GetAllClasses() ?? new List<SchoolClass>();
                foreach (var student in students)
                {
                    var cls = classes.FirstOrDefault(c => c.Name == student.ClassName);
                    student.ClassId = cls?.Id ?? 0;
                }

                int imported = dbHelper.ImportStudents(students);
                MessageBox.Show($"Импортировано {imported} учеников", "Импорт завершен",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                LoadStudents();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка импорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // ЭКСПОРТ УЧЕНИКОВ
        // ============================================================

        private void btnExportStudents_Click(object sender, RoutedEventArgs e)
        {
            ExportStudentsToCSV();
        }

        private void ExportStudentsToCSV()
        {
            try
            {
                if (allStudents == null || !allStudents.Any())
                {
                    MessageBox.Show("Нет данных для экспорта", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Title = "Сохранить список учеников",
                    Filter = "CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*",
                    DefaultExt = ".csv",
                    FileName = $"Ученики_{DateTime.Now:yyyy-MM-dd}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();

                    // Заголовки
                    sb.AppendLine("ФИО;Класс;Дата рождения;Пол;Родитель;Телефон родителя;Адрес");

                    // Данные
                    foreach (var student in allStudents)
                    {
                        sb.AppendLine($"{student.FullName};" +
                                     $"{student.ClassName};" +
                                     $"{student.BirthDate?.ToString("yyyy-MM-dd") ?? ""};" +
                                     $"{student.Gender ?? ""};" +
                                     $"{student.ParentName ?? ""};" +
                                     $"{student.ParentPhone ?? ""};" +
                                     $"{student.Address ?? ""}");
                    }

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Экспортировано {allStudents.Count} учеников", "Экспорт завершен",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Экспорт учеников с фильтром по классу
        /// </summary>
        private void ExportStudentsByClass(string className)
        {
            try
            {
                if (string.IsNullOrEmpty(className) || className == "Все классы")
                {
                    ExportStudentsToCSV();
                    return;
                }

                var students = allStudents.Where(s => s.ClassName == className).ToList();
                if (!students.Any())
                {
                    MessageBox.Show($"Нет учеников в классе {className}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Title = $"Сохранить список учеников класса {className}",
                    Filter = "CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*",
                    DefaultExt = ".csv",
                    FileName = $"Ученики_{className}_{DateTime.Now:yyyy-MM-dd}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("ФИО;Класс;Дата рождения;Пол;Родитель;Телефон родителя;Адрес");

                    foreach (var student in students)
                    {
                        sb.AppendLine($"{student.FullName};" +
                                     $"{student.ClassName};" +
                                     $"{student.BirthDate?.ToString("yyyy-MM-dd") ?? ""};" +
                                     $"{student.Gender ?? ""};" +
                                     $"{student.ParentName ?? ""};" +
                                     $"{student.ParentPhone ?? ""};" +
                                     $"{student.Address ?? ""}");
                    }

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Экспортировано {students.Count} учеников из класса {className}",
                        "Экспорт завершен",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
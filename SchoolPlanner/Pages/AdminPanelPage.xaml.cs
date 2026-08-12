using MySql.Data.MySqlClient;
using SchoolPlanner.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using static SchoolPlanner.Database.DbHelper;

namespace SchoolPlanner.Page
{
    public partial class AdminPanelPage
    {
        private DbHelper dbHelper;
        private User currentUser;
        private bool isUpdating = false;
        private DateTime lastUpdateTime = DateTime.Now;

        // Модели для отображения
        public class TeacherViewModel
        {
            public int Id { get; set; }
            public string FullName { get; set; }
            public string Subject { get; set; }
            public List<string> Subjects { get; set; } = new List<string>();
            public List<ClassSubjectItem> SubjectsByClass { get; set; } = new List<ClassSubjectItem>(); // ✅ ДОБАВЛЕНО
            public string Username { get; set; }
            public int MaxHours { get; set; }
            public string Room { get; set; }
        }
        public class RoomViewModel
        {
            public int Id { get; set; }
            public string Number { get; set; }
            public string Subject { get; set; }
            public string TeacherName { get; set; }
        }

        public class ClassViewModel
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Grade { get; set; }
            public int StudentCount { get; set; }
            public string PlanName { get; set; }
            public int? PlanId { get; set; }
        }

        // Класс для отображения в DataGrid учебных планов
        public class StudyPlanRow : System.ComponentModel.INotifyPropertyChanged
        {
            private string _subjectName;
            private Dictionary<int, int?> _hoursByGrade = new Dictionary<int, int?>();

            public string SubjectName
            {
                get => _subjectName;
                set { _subjectName = value; OnPropertyChanged(); }
            }

            public Dictionary<int, int?> HoursByGrade
            {
                get => _hoursByGrade;
                set { _hoursByGrade = value; OnPropertyChanged(); }
            }

            public int TotalHours
            {
                get
                {
                    return HoursByGrade.Values.Where(v => v.HasValue).Sum(v => v.Value);
                }
            }

            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
            {
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
            }
        }

        // Конвертер для отображения прочерка
        public class NullToDashConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (value == null || (value is int? && !((int?)value).HasValue))
                {
                    return "—";
                }
                return value.ToString();
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                var str = value as string;
                if (string.IsNullOrEmpty(str) || str == "—" || str == "-" || str == " ")
                {
                    return null;
                }

                str = str.Trim();
                if (string.IsNullOrEmpty(str))
                {
                    return null;
                }

                if (int.TryParse(str, out int result))
                {
                    return result;
                }

                return null;
            }
        }

        public AdminPanelPage()
        {
            try
            {
                InitializeComponent();
                dbHelper = new DbHelper();
                currentUser = App.CurrentUser;

                if (currentUser.Role != UserRole.Admin)
                {
                    MessageBox.Show("Доступ запрещен. Только для УВР.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    NavigationService.GoBack();
                    return;
                }

                Loaded += Page_Loaded;
                dgStudyPlans.CellEditEnding += DgStudyPlans_CellEditEnding;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTeachers();
            LoadRooms();
            LoadClasses();
            LoadDifficultyScale();
            LoadClassPlans();
            LoadTeacherSchedule(); // ✅ Добавлено
            UpdateLastUpdateTime();
        }
        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.GoBack();
        }

        private void UpdateLastUpdateTime()
        {
            lastUpdateTime = DateTime.Now;
            if (txtLastUpdate != null)
            {
                txtLastUpdate.Text = $"Последнее обновление: {lastUpdateTime:HH:mm:ss}";
            }
        }

        // ============================================================
        // ЗАГРУЗКА ДАННЫХ
        // ============================================================


        private void LoadTeachers()
        {
            try
            {
                var teachers = dbHelper.GetAllTeachersWithDetails();
                var teacherViewModels = new List<TeacherViewModel>();

                foreach (var teacher in teachers)
                {
                    var viewModel = new TeacherViewModel
                    {
                        Id = teacher.Id,
                        FullName = teacher.FullName,
                        Subject = teacher.Subject,
                        Subjects = (teacher.Subject?.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries) ?? new string[0]).ToList(),
                        Username = teacher.Username,
                        MaxHours = teacher.MaxHours,
                        Room = teacher.Room,
                        SubjectsByClass = new List<ClassSubjectItem>() // Инициализация
                    };

                    // Получаем предметы учителя по классам
                    try
                    {
                        var subjectsByClass = dbHelper.GetTeacherSubjectsByClass(teacher.Id);
                        foreach (var item in subjectsByClass.OrderBy(x => x.Key))
                        {
                            viewModel.SubjectsByClass.Add(new ClassSubjectItem
                            {
                                ClassName = item.Key,
                                Subjects = string.Join(", ", item.Value)
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка получения предметов для {teacher.FullName}: {ex.Message}");
                    }

                    teacherViewModels.Add(viewModel);
                }

                lvTeachers.ItemsSource = teacherViewModels;
                txtTeacherStats.Text = $"Всего учителей: {teacherViewModels.Count}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки учителей: {ex.Message}");
                MessageBox.Show($"Ошибка загрузки учителей: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void btnRefreshTeachers_Click(object sender, RoutedEventArgs e)
        {
            LoadTeachers();
            MessageBox.Show("Данные обновлены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        public class ClassSubjectItem
        {
            public string ClassName { get; set; }
            public string Subjects { get; set; }
        }
        private void LoadRooms()
        {
            try
            {
                var rooms = dbHelper.GetAllRooms();
                var roomViewModels = rooms.Select(room => new RoomViewModel
                {
                    Id = room.Id,
                    Number = room.Number,
                    Subject = room.Subject,
                    TeacherName = room.TeacherName
                }).ToList();

                lvRooms.ItemsSource = roomViewModels;
                txtRoomStats.Text = $"Всего кабинетов: {roomViewModels.Count}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки кабинетов: {ex.Message}");
            }
        }

        private void LoadDifficultyScale()
        {
            try
            {
                var difficulties = dbHelper.GetAllSubjectDifficulties();
                dgDifficulty.ItemsSource = difficulties;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки шкалы трудности: {ex.Message}");
            }
        }

        // ============================================================
        // УЧЕБНЫЕ ПЛАНЫ (ТАБЛИЦА)
        // ============================================================

        private void LoadClassPlans()
        {
            if (isUpdating) return;
            isUpdating = true;

            try
            {
                var allClasses = dbHelper.GetAllClassesWithInfo();

                // Проверяем, есть ли классы
                if (allClasses == null || !allClasses.Any())
                {
                    dgStudyPlans.ItemsSource = null;
                    txtPlanStats.Text = "Нет классов в системе";
                    UpdateLastUpdateTime();
                    return;
                }

                var classPlans = new Dictionary<string, DbHelper.StudyPlan>();
                foreach (var classItem in allClasses)
                {
                    var plan = dbHelper.GetPlanForClass(classItem.Name);
                    if (plan != null)
                    {
                        classPlans[classItem.Name] = plan;
                    }
                }

                // Если нет планов - показываем сообщение
                if (!classPlans.Any())
                {
                    dgStudyPlans.Columns.Clear();
                    dgStudyPlans.ItemsSource = null;

                    // Показываем информационное сообщение
                    var noDataText = new TextBlock
                    {
                        Text = "📋 Нет учебных планов\n\nСоздайте учебный план с помощью кнопки 'Добавить учебный план'",
                        FontSize = 16,
                        Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 50, 0, 0)
                    };

                    dgStudyPlans.ItemsSource = new List<TextBlock> { noDataText };
                    txtPlanStats.Text = "Всего предметов: 0 | Всего классов: 0 | Всего часов: 0";
                    UpdateLastUpdateTime();
                    return;
                }

                // Собираем все предметы из планов
                var allSubjectNames = new HashSet<string>();
                foreach (var plan in classPlans.Values)
                {
                    foreach (var subject in plan.Subjects)
                    {
                        allSubjectNames.Add(subject.SubjectName);
                    }
                }

                if (!allSubjectNames.Any())
                {
                    dgStudyPlans.ItemsSource = null;
                    txtPlanStats.Text = "Нет предметов в учебных планах";
                    UpdateLastUpdateTime();
                    return;
                }

                // Получаем порядок сортировки из БД
                var allSubjectsFromDb = dbHelper.GetAllSubjectsWithHours();
                var subjectOrderDict = new Dictionary<string, int>();

                if (allSubjectsFromDb != null)
                {
                    foreach (var subject in allSubjectsFromDb)
                    {
                        subjectOrderDict[subject.Name] = subject.SortOrder ?? 999;
                    }
                }

                // Сортируем предметы
                var sortedSubjects = allSubjectNames
                    .OrderBy(subject => subjectOrderDict.ContainsKey(subject) ? subjectOrderDict[subject] : 999)
                    .ThenBy(subject => subject)
                    .ToList();

                // Сортируем классы по параллели и названию
                var sortedClasses = allClasses
                    .Where(c => classPlans.ContainsKey(c.Name))
                    .OrderBy(c => c.Grade)
                    .ThenBy(c => c.Name)
                    .ToList();

                if (!sortedClasses.Any())
                {
                    dgStudyPlans.ItemsSource = null;
                    txtPlanStats.Text = "Нет классов с учебными планами";
                    UpdateLastUpdateTime();
                    return;
                }

                // Строим строки таблицы
                var rows = new List<StudyPlanRow>();
                foreach (var subjectName in sortedSubjects)
                {
                    var row = new StudyPlanRow
                    {
                        SubjectName = subjectName
                    };

                    foreach (var classItem in sortedClasses)
                    {
                        int? hours = null;
                        if (classPlans.TryGetValue(classItem.Name, out var plan))
                        {
                            var planSubject = plan.Subjects.FirstOrDefault(s => s.SubjectName == subjectName);
                            if (planSubject != null)
                            {
                                hours = planSubject.HoursPerWeek;
                            }
                        }
                        row.HoursByGrade[classItem.Id] = hours;
                    }

                    rows.Add(row);
                }

                // Очищаем и перестраиваем DataGrid
                dgStudyPlans.Columns.Clear();
                dgStudyPlans.ItemsSource = null;

                // Колонка "Предмет"
                var subjectColumn = new DataGridTextColumn
                {
                    Header = "📚 Предмет",
                    Binding = new Binding("SubjectName"),
                    Width = new DataGridLength(220),
                    IsReadOnly = true
                };
                subjectColumn.ElementStyle = new Style(typeof(TextBlock));
                subjectColumn.ElementStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left));
                subjectColumn.ElementStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(10, 0, 0, 0)));
                subjectColumn.ElementStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
                dgStudyPlans.Columns.Add(subjectColumn);

                // Колонки для классов
                foreach (var classItem in sortedClasses)
                {
                    var column = new DataGridTextColumn
                    {
                        Header = classItem.Name,
                        Width = new DataGridLength(55),
                    };

                    var binding = new Binding($"HoursByGrade[{classItem.Id}]")
                    {
                        Converter = new NullToDashConverter(),
                        TargetNullValue = "—",
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    };
                    column.Binding = binding;

                    column.ElementStyle = new Style(typeof(TextBlock));
                    column.ElementStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));

                    column.EditingElementStyle = new Style(typeof(TextBox));
                    column.EditingElementStyle.Setters.Add(new Setter(TextBox.TextAlignmentProperty, TextAlignment.Center));
                    column.EditingElementStyle.Setters.Add(new Setter(TextBox.WidthProperty, 50.0));

                    dgStudyPlans.Columns.Add(column);
                }

                // Колонка "Итого"
                var totalColumn = new DataGridTextColumn
                {
                    Header = "Итого",
                    Width = new DataGridLength(55),
                    Binding = new Binding("TotalHours"),
                    IsReadOnly = true
                };
                totalColumn.ElementStyle = new Style(typeof(TextBlock));
                totalColumn.ElementStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
                totalColumn.ElementStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
                totalColumn.ElementStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(33, 150, 243))));
                dgStudyPlans.Columns.Add(totalColumn);

                // Устанавливаем источник данных
                dgStudyPlans.ItemsSource = rows;
                UpdatePlanStats(rows, sortedClasses);
                UpdateLastUpdateTime();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки учебных планов: {ex.Message}");
                MessageBox.Show($"Ошибка загрузки учебных планов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isUpdating = false;
            }
        }

        private void DgStudyPlans_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            try
            {
                if (e.EditAction != DataGridEditAction.Commit) return;

                var row = e.Row.Item as StudyPlanRow;
                if (row == null) return;

                var column = e.Column as DataGridTextColumn;
                if (column == null) return;

                var classHeader = column.Header.ToString();
                if (classHeader == "📚 Предмет" || classHeader == "Итого") return;

                var allClasses = dbHelper.GetAllClassesWithInfo();
                var classItem = allClasses.FirstOrDefault(c => c.Name == classHeader);
                if (classItem == null) return;

                var textBox = e.EditingElement as TextBox;
                if (textBox == null) return;

                string textValue = textBox.Text?.Trim() ?? "";

                if (string.IsNullOrEmpty(textValue) || textValue == "—" || textValue == "-")
                {
                    row.HoursByGrade[classItem.Id] = null;
                    RemoveSubjectFromClassPlan(classItem.Name, row.SubjectName);
                    UpdatePlanStats(dgStudyPlans.ItemsSource as List<StudyPlanRow>, allClasses);
                    UpdateLastUpdateTime();
                    RefreshDataGrid();
                    return;
                }

                if (int.TryParse(textValue, out int hours))
                {
                    if (hours >= 0 && hours <= 8)
                    {
                        row.HoursByGrade[classItem.Id] = hours;
                        UpdateClassPlanSubject(classItem.Name, row.SubjectName, hours);
                        UpdatePlanStats(dgStudyPlans.ItemsSource as List<StudyPlanRow>, allClasses);
                        UpdateLastUpdateTime();
                        RefreshDataGrid();
                    }
                    else
                    {
                        MessageBox.Show("Введите количество часов от 0 до 8", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        var oldValue = row.HoursByGrade[classItem.Id];
                        textBox.Text = oldValue?.ToString() ?? "—";
                        e.Cancel = true;
                    }
                }
                else
                {
                    MessageBox.Show("Введите число от 0 до 8 или '-' для удаления", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    var oldValue = row.HoursByGrade[classItem.Id];
                    textBox.Text = oldValue?.ToString() ?? "—";
                    e.Cancel = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка редактирования: {ex.Message}");
                MessageBox.Show($"Ошибка редактирования: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshDataGrid()
        {
            try
            {
                var items = dgStudyPlans.ItemsSource as List<StudyPlanRow>;
                if (items != null)
                {
                    var selectedItem = dgStudyPlans.SelectedItem;
                    dgStudyPlans.ItemsSource = null;
                    dgStudyPlans.ItemsSource = items;
                    if (selectedItem != null)
                    {
                        dgStudyPlans.SelectedItem = selectedItem;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления DataGrid: {ex.Message}");
            }
        }

        private void RemoveSubjectFromClassPlan(string className, string subjectName)
        {
            try
            {
                var plan = dbHelper.GetPlanForClass(className);
                if (plan == null) return;

                var subjectToRemove = plan.Subjects.FirstOrDefault(s => s.SubjectName == subjectName);
                if (subjectToRemove != null)
                {
                    plan.Subjects.Remove(subjectToRemove);
                    int order = 1;
                    foreach (var s in plan.Subjects.OrderBy(x => x.SortOrder))
                    {
                        s.SortOrder = order++;
                    }

                    dbHelper.UpdateStudyPlan(plan);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления предмета из плана: {ex.Message}");
            }
        }

        private void UpdateClassPlanSubject(string className, string subjectName, int hours)
        {
            try
            {
                var plan = dbHelper.GetPlanForClass(className);
                if (plan == null)
                {
                    int grade = GetGradeFromClassName(className);
                    plan = new DbHelper.StudyPlan
                    {
                        Name = $"Учебный план для {className}",
                        Variant = "standard",
                        Description = $"Учебный план для класса {className}",
                        CreatedBy = currentUser.Id,
                        Subjects = new List<DbHelper.PlanSubject>()
                    };

                    int planId = dbHelper.SaveStudyPlan(plan);
                    if (planId > 0)
                    {
                        dbHelper.AssignClassToPlan(className, planId, grade);
                        plan = dbHelper.GetPlanForClass(className);
                        if (plan == null) return;
                    }
                    else
                    {
                        return;
                    }
                }

                var planSubject = plan.Subjects.FirstOrDefault(s => s.SubjectName == subjectName);
                if (planSubject != null)
                {
                    planSubject.HoursPerWeek = hours;
                    dbHelper.UpdateStudyPlan(plan);
                }
                else
                {
                    var grade = GetGradeFromClassName(className);
                    plan.Subjects.Add(new DbHelper.PlanSubject
                    {
                        SubjectName = subjectName,
                        Grade = grade,
                        HoursPerWeek = hours,
                        Difficulty = 1,
                        IsRequired = true,
                        SortOrder = plan.Subjects.Count + 1
                    });
                    dbHelper.UpdateStudyPlan(plan);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления: {ex.Message}");
                MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePlanStats(List<StudyPlanRow> rows, List<DbHelper.ClassInfo> classes)
        {
            try
            {
                if (rows == null)
                {
                    txtPlanStats.Text = "Всего предметов: 0 | Всего классов: 0 | Всего часов: 0";
                    return;
                }

                int totalSubjects = rows.Count;
                int totalClasses = classes?.Count ?? 0;
                int totalHours = 0;

                foreach (var row in rows)
                {
                    totalHours += row.TotalHours;
                }

                txtPlanStats.Text = $"Всего предметов: {totalSubjects} | Всего классов: {totalClasses} | Всего часов: {totalHours}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка UpdatePlanStats: {ex.Message}");
            }
        }

        // ============================================================
        // УПРАВЛЕНИЕ УЧЕБНЫМИ ПЛАНАМИ
        // ============================================================

        private void btnAddPlan_Click(object sender, RoutedEventArgs e)
        {
            var classes = dbHelper.GetAllClassesWithInfo();
            var classesWithoutPlan = classes.Where(classItem => classItem.PlanId == null || classItem.PlanId == 0).ToList();

            if (!classesWithoutPlan.Any())
            {
                MessageBox.Show("У всех классов уже есть учебные планы", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Window
            {
                Title = "➕ Добавление учебного плана",
                Width = 450,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = "Выберите класс для добавления учебного плана",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Margin = new Thickness(0, 0, 0, 15)
            });

            var cmbClass = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10)
            };
            foreach (var classItem in classesWithoutPlan.OrderBy(classObj => classObj.Name))
            {
                cmbClass.Items.Add(classItem.Name);
            }
            if (cmbClass.Items.Count > 0) cmbClass.SelectedIndex = 0;
            panel.Children.Add(cmbClass);

            panel.Children.Add(new TextBlock
            {
                Text = "Выберите вариант плана:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5)
            });

            var cmbVariant = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10)
            };
            cmbVariant.Items.Add("Стандартный (ФГОС)");
            cmbVariant.Items.Add("ФАОП ООО вариант 1");
            cmbVariant.Items.Add("ФАОП ООО вариант 2.2.2");
            cmbVariant.SelectedIndex = 0;
            panel.Children.Add(cmbVariant);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button btnAdd = new Button
            {
                Content = "Добавить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Margin = new Thickness(0, 0, 10, 0),
                Width = 100,
                Height = 32
            };
            btnAdd.Click += (buttonSender, args) =>
            {
                if (cmbClass.SelectedItem == null)
                {
                    MessageBox.Show("Выберите класс", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string className = cmbClass.SelectedItem.ToString();
                string variant = cmbVariant.SelectedItem.ToString();

                CreatePlanForClass(className, variant);
                dialog.Close();
                LoadClassPlans();
            };
            buttonPanel.Children.Add(btnAdd);

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Width = 100,
                Height = 32
            };
            btnCancel.Click += (buttonSender, args) => dialog.Close();
            buttonPanel.Children.Add(btnCancel);

            panel.Children.Add(buttonPanel);
            dialog.Content = panel;
            dialog.ShowDialog();
        }

        private void CreatePlanForClass(string className, string variant)
        {
            try
            {
                int grade = GetGradeFromClassName(className);
                var existingPlan = dbHelper.GetPlanForClass(className);
                if (existingPlan != null)
                {
                    MessageBox.Show($"У класса {className} уже есть учебный план", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var plan = new DbHelper.StudyPlan
                {
                    Name = $"Учебный план для {className} ({variant})",
                    Variant = variant.Replace("Стандартный (ФГОС)", "standard").Replace("ФАОП ООО вариант 1", "1").Replace("ФАОП ООО вариант 2.2.2", "2.2.2"),
                    Description = $"Учебный план для класса {className}",
                    CreatedBy = currentUser.Id,
                    Subjects = new List<DbHelper.PlanSubject>()
                };

                int planId = dbHelper.SaveStudyPlan(plan);
                if (planId > 0)
                {
                    dbHelper.AssignClassToPlan(className, planId, grade);
                    MessageBox.Show($"Учебный план для класса {className} успешно создан", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания плана: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnAddSubject_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "➕ Добавление нового предмета",
                Width = 480,
                Height = 480,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                ResizeMode = ResizeMode.NoResize
            };

            var mainPanel = new StackPanel { Margin = new Thickness(20) };

            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var headerText = new TextBlock
            {
                Text = "📚 Добавление нового предмета",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192))
            };
            headerBorder.Child = headerText;
            mainPanel.Children.Add(headerBorder);

            mainPanel.Children.Add(new TextBlock
            {
                Text = "Название предмета:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 4),
                FontWeight = FontWeights.SemiBold
            });

            var txtSubjectName = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            mainPanel.Children.Add(txtSubjectName);

            mainPanel.Children.Add(new TextBlock
            {
                Text = "Порядок сортировки:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 4),
                FontWeight = FontWeights.SemiBold
            });

            var txtSortOrder = new TextBox
            {
                Text = "0",
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            mainPanel.Children.Add(txtSortOrder);

            mainPanel.Children.Add(new TextBlock
            {
                Text = "Для каких классов (отметьте галочками):",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 4),
                FontWeight = FontWeights.SemiBold
            });

            var classesPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
            var classCheckboxes = new Dictionary<int, CheckBox>();
            var gradeColors = new Dictionary<int, SolidColorBrush>
            {
                { 5, new SolidColorBrush(Color.FromRgb(76, 175, 80)) },
                { 6, new SolidColorBrush(Color.FromRgb(33, 150, 243)) },
                { 7, new SolidColorBrush(Color.FromRgb(255, 152, 0)) },
                { 8, new SolidColorBrush(Color.FromRgb(156, 39, 176)) },
                { 9, new SolidColorBrush(Color.FromRgb(233, 30, 99)) },
                { 10, new SolidColorBrush(Color.FromRgb(0, 150, 136)) },
                { 11, new SolidColorBrush(Color.FromRgb(96, 125, 139)) }
            };

            for (int grade = 5; grade <= 11; grade++)
            {
                var checkBox = new CheckBox
                {
                    Content = $"{grade} класс",
                    Margin = new Thickness(0, 0, 12, 4),
                    Tag = grade,
                    IsChecked = true,
                    Foreground = gradeColors.ContainsKey(grade) ? gradeColors[grade] : new SolidColorBrush(Color.FromRgb(85, 85, 85))
                };
                classCheckboxes[grade] = checkBox;
                classesPanel.Children.Add(checkBox);
            }
            mainPanel.Children.Add(classesPanel);

            var selectAllPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 12)
            };

            Button btnSelectAll = new Button
            {
                Content = "Выбрать все",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 8, 0),
                Height = 28,
                FontSize = 11
            };
            btnSelectAll.Click += (s, args) =>
            {
                foreach (var cb in classCheckboxes.Values)
                {
                    cb.IsChecked = true;
                }
            };
            selectAllPanel.Children.Add(btnSelectAll);

            Button btnDeselectAll = new Button
            {
                Content = "Снять все",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                Padding = new Thickness(10, 4, 10, 4),
                Height = 28,
                FontSize = 11
            };
            btnDeselectAll.Click += (s, args) =>
            {
                foreach (var cb in classCheckboxes.Values)
                {
                    cb.IsChecked = false;
                }
            };
            selectAllPanel.Children.Add(btnDeselectAll);
            mainPanel.Children.Add(selectAllPanel);

            var infoBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var infoText = new TextBlock
            {
                Text = "💡 Новый предмет будет добавлен в шкалу трудности и во все выбранные классы с 0 часами.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 81, 0)),
                TextWrapping = TextWrapping.Wrap
            };
            infoBorder.Child = infoText;
            mainPanel.Children.Add(infoBorder);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 5, 0, 0)
            };

            Button btnAdd = new Button
            {
                Content = "Добавить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Margin = new Thickness(0, 0, 8, 0),
                Width = 100,
                Height = 32
            };
            btnAdd.Click += (buttonSender, args) =>
            {
                string subjectName = txtSubjectName.Text.Trim();
                if (string.IsNullOrEmpty(subjectName))
                {
                    MessageBox.Show("Введите название предмета", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(txtSortOrder.Text, out int sortOrder) || sortOrder < 0)
                {
                    MessageBox.Show("Введите корректный порядок сортировки (число >= 0)", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var existingSubjects = dbHelper.GetAllSubjectsWithHours();
                if (existingSubjects.Any(s => s.Name == subjectName))
                {
                    MessageBox.Show($"Предмет '{subjectName}' уже существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int subjectId = dbHelper.AddSubject(subjectName, sortOrder);
                if (subjectId <= 0)
                {
                    MessageBox.Show("Ошибка добавления предмета", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var selectedGrades = classCheckboxes.Where(c => c.Value.IsChecked == true).Select(c => c.Key).ToList();
                if (!selectedGrades.Any())
                {
                    MessageBox.Show("Выберите хотя бы один класс", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var allClasses = dbHelper.GetAllClassesWithInfo();
                var classesForSubject = allClasses.Where(c => selectedGrades.Contains(c.Grade)).ToList();

                int addedCount = 0;
                int existingCount = 0;

                foreach (var classItem in classesForSubject)
                {
                    var plan = dbHelper.GetPlanForClass(classItem.Name);
                    if (plan != null)
                    {
                        if (!plan.Subjects.Any(s => s.SubjectName == subjectName))
                        {
                            plan.Subjects.Add(new DbHelper.PlanSubject
                            {
                                SubjectName = subjectName,
                                Grade = classItem.Grade,
                                HoursPerWeek = 0,
                                Difficulty = 1,
                                IsRequired = true,
                                SortOrder = plan.Subjects.Count + 1
                            });
                            dbHelper.UpdateStudyPlan(plan);
                            addedCount++;
                        }
                        else
                        {
                            existingCount++;
                        }
                    }
                    else
                    {
                        int grade = GetGradeFromClassName(classItem.Name);
                        var newPlan = new DbHelper.StudyPlan
                        {
                            Name = $"Учебный план для {classItem.Name}",
                            Variant = "standard",
                            Description = $"Учебный план для класса {classItem.Name}",
                            CreatedBy = currentUser.Id,
                            Subjects = new List<DbHelper.PlanSubject>()
                        };

                        int planId = dbHelper.SaveStudyPlan(newPlan);
                        if (planId > 0)
                        {
                            dbHelper.AssignClassToPlan(classItem.Name, planId, grade);
                            var plan2 = dbHelper.GetPlanForClass(classItem.Name);
                            if (plan2 != null)
                            {
                                plan2.Subjects.Add(new DbHelper.PlanSubject
                                {
                                    SubjectName = subjectName,
                                    Grade = classItem.Grade,
                                    HoursPerWeek = 0,
                                    Difficulty = 1,
                                    IsRequired = true,
                                    SortOrder = 1
                                });
                                dbHelper.UpdateStudyPlan(plan2);
                                addedCount++;
                            }
                        }
                    }
                }

                string message = $"Предмет '{subjectName}' успешно добавлен!\n\n" +
                    $"• Добавлен в шкалу трудности\n" +
                    $"• Добавлен в {addedCount} классов\n" +
                    $"• В {existingCount} классах уже был";

                if (addedCount == 0)
                {
                    message = $"Предмет '{subjectName}' уже существует во всех выбранных классах!";
                }

                MessageBox.Show(message, "Результат",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                dialog.Close();
                LoadClassPlans();
                LoadDifficultyScale();
            };
            buttonPanel.Children.Add(btnAdd);

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                Width = 100,
                Height = 32
            };
            btnCancel.Click += (buttonSender, args) => dialog.Close();
            buttonPanel.Children.Add(btnCancel);

            mainPanel.Children.Add(buttonPanel);
            dialog.Content = mainPanel;
            dialog.ShowDialog();
        }

        private void btnDeletePlan_Click(object sender, RoutedEventArgs e)
        {
            var classes = dbHelper.GetAllClassesWithInfo();
            var classesWithPlan = classes.Where(classItem => classItem.PlanId != null && classItem.PlanId > 0).ToList();

            if (!classesWithPlan.Any())
            {
                MessageBox.Show("Нет классов с учебными планами для удаления", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Window
            {
                Title = "🗑 Удаление учебного плана",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = "Выберите класс для удаления учебного плана",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Margin = new Thickness(0, 0, 0, 15)
            });

            var cmbClass = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10)
            };
            foreach (var classItem in classesWithPlan.OrderBy(classObj => classObj.Name))
            {
                cmbClass.Items.Add(classItem.Name);
            }
            if (cmbClass.Items.Count > 0) cmbClass.SelectedIndex = 0;
            panel.Children.Add(cmbClass);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button btnDelete = new Button
            {
                Content = "🗑 Удалить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                Margin = new Thickness(0, 0, 10, 0),
                Width = 100,
                Height = 32
            };
            btnDelete.Click += (buttonSender, args) =>
            {
                if (cmbClass.SelectedItem == null)
                {
                    MessageBox.Show("Выберите класс", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string className = cmbClass.SelectedItem.ToString();

                if (MessageBox.Show($"Удалить учебный план для класса {className}?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    var plan = dbHelper.GetPlanForClass(className);
                    if (plan != null && plan.Id > 0)
                    {
                        bool deleted = dbHelper.DeleteStudyPlan(plan.Id);
                        if (deleted)
                        {
                            MessageBox.Show($"План для класса {className} удален", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    dialog.Close();
                    LoadClassPlans();
                }
            };
            buttonPanel.Children.Add(btnDelete);

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Width = 100,
                Height = 32
            };
            btnCancel.Click += (buttonSender, args) => dialog.Close();
            buttonPanel.Children.Add(btnCancel);

            panel.Children.Add(buttonPanel);
            dialog.Content = panel;
            dialog.ShowDialog();
        }

        private void btnSavePlans_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Все изменения автоматически сохранены при редактировании", "Информация",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnRefreshPlans_Click(object sender, RoutedEventArgs e)
        {
            LoadClassPlans();
        }

        private void btnExportPlan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = dgStudyPlans.ItemsSource as List<StudyPlanRow>;
                if (data == null || !data.Any())
                {
                    MessageBox.Show("Нет данных для экспорта", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    DefaultExt = ".csv",
                    FileName = $"Учебный_план_{DateTime.Now:yyyy-MM-dd}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var lines = new List<string>();
                    var headers = new List<string> { "Предмет" };
                    var classes = dbHelper.GetAllClassesWithInfo().OrderBy(c => c.Grade).ThenBy(c => c.Name).ToList();
                    foreach (var classItem in classes)
                    {
                        headers.Add(classItem.Name);
                    }
                    headers.Add("Итого");
                    lines.Add(string.Join(";", headers));

                    foreach (var row in data)
                    {
                        var values = new List<string> { row.SubjectName };
                        foreach (var classItem in classes)
                        {
                            if (row.HoursByGrade.TryGetValue(classItem.Id, out var hours))
                            {
                                values.Add(hours?.ToString() ?? "0");
                            }
                            else
                            {
                                values.Add("0");
                            }
                        }
                        values.Add(row.TotalHours.ToString());
                        lines.Add(string.Join(";", values));
                    }

                    System.IO.File.WriteAllLines(saveDialog.FileName, lines, System.Text.Encoding.UTF8);
                    MessageBox.Show($"Экспорт выполнен успешно!\nФайл: {saveDialog.FileName}", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int GetGradeFromClassName(string className)
        {
            if (string.IsNullOrEmpty(className)) return 5;
            if (className.StartsWith("5")) return 5;
            if (className.StartsWith("6")) return 6;
            if (className.StartsWith("7")) return 7;
            if (className.StartsWith("8")) return 8;
            if (className.StartsWith("9")) return 9;
            if (className.StartsWith("10")) return 10;
            if (className.StartsWith("11")) return 11;
            return 5;
        }

        // ============================================================
        // УЧИТЕЛЯ
        // ============================================================

        private string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return null;

            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(password);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }
        private void btnAddTeacher_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "➕ Добавление учителя",
                Width = 520,
                Height = 620,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                ResizeMode = ResizeMode.NoResize
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(5)
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var headerText = new TextBlock
            {
                Text = "👨‍🏫 Добавление нового учителя",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192))
            };
            headerBorder.Child = headerText;
            panel.Children.Add(headerBorder);

            // ФИО
            panel.Children.Add(new TextBlock
            {
                Text = "ФИО учителя:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtFullName = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(txtFullName);

            // ✅ Предметы (ListBox с множественным выбором)
            panel.Children.Add(new TextBlock
            {
                Text = "📚 Предметы (можно выбрать несколько с Ctrl+клик):",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });

            var lbSubjects = new ListBox
            {
                Height = 120,
                SelectionMode = SelectionMode.Multiple,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 12),
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255))
            };

            try
            {
                var subjects = dbHelper.GetAllSubjectsWithHours();
                foreach (var subject in subjects.OrderBy(s => s.Name))
                {
                    lbSubjects.Items.Add(subject.Name);
                }
            }
            catch { }

            panel.Children.Add(lbSubjects);

            // Логин
            panel.Children.Add(new TextBlock
            {
                Text = "Логин (для входа в систему):",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtUsername = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(txtUsername);

            // Пароль
            panel.Children.Add(new TextBlock
            {
                Text = "Пароль:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtPassword = new PasswordBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(txtPassword);

            // Подтверждение пароля
            panel.Children.Add(new TextBlock
            {
                Text = "Подтверждение пароля:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtConfirmPassword = new PasswordBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(txtConfirmPassword);

            // Максимум часов
            panel.Children.Add(new TextBlock
            {
                Text = "Максимальное количество часов в неделю:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtMaxHours = new TextBox
            {
                Text = "20",
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(txtMaxHours);

            // Кабинет
            panel.Children.Add(new TextBlock
            {
                Text = "Кабинет (номер):",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtRoom = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(txtRoom);

            // Информация
            var infoBorder2 = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var infoText2 = new TextBlock
            {
                Text = "💡 Учитель может вести несколько предметов. Выберите все нужные предметы с нажатой клавишей Ctrl.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 81, 0)),
                TextWrapping = TextWrapping.Wrap
            };
            infoBorder2.Child = infoText2;
            panel.Children.Add(infoBorder2);

            // Кнопки
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button btnAdd = new Button
            {
                Content = "Добавить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Margin = new Thickness(0, 0, 10, 0),
                Width = 120,
                Height = 35
            };

            btnAdd.Click += (buttonSender, args) =>
            {
                // Валидация
                if (string.IsNullOrWhiteSpace(txtFullName.Text))
                {
                    MessageBox.Show("Введите ФИО учителя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (lbSubjects.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Выберите хотя бы один предмет", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtUsername.Text))
                {
                    MessageBox.Show("Введите логин", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (txtUsername.Text.Length < 3)
                {
                    MessageBox.Show("Логин должен содержать минимум 3 символа", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtPassword.Password))
                {
                    MessageBox.Show("Введите пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (txtPassword.Password.Length < 6)
                {
                    MessageBox.Show("Пароль должен содержать минимум 6 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (txtPassword.Password != txtConfirmPassword.Password)
                {
                    MessageBox.Show("Пароли не совпадают", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(txtMaxHours.Text, out int maxHours) || maxHours <= 0 || maxHours > 40)
                {
                    MessageBox.Show("Введите корректное количество часов (1-40)", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    var existingTeachers = dbHelper.GetAllTeachersWithDetails();
                    if (existingTeachers.Any(t => t.Username == txtUsername.Text))
                    {
                        MessageBox.Show("Учитель с таким логином уже существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    string fullName = txtFullName.Text.Trim();
                    string username = txtUsername.Text.Trim();
                    string password = txtPassword.Password;
                    string room = txtRoom.Text.Trim();

                    // ✅ Получаем список предметов
                    var selectedSubjects = lbSubjects.SelectedItems.Cast<string>().ToList();
                    string subjectsString = string.Join(", ", selectedSubjects);
                    string firstSubject = selectedSubjects.FirstOrDefault() ?? "";

                    string hashedPassword = HashPassword(password);

                    // ✅ Добавляем пользователя
                    int userId = dbHelper.AddUser(fullName, username, hashedPassword, "Teacher", firstSubject, maxHours);

                    if (userId > 0)
                    {
                        // ✅ Добавляем учителя в таблицу teachers
                        bool teacherAdded = dbHelper.AddTeacher(userId, firstSubject, maxHours, room);

                        if (teacherAdded)
                        {
                            // ✅ Обновляем список предметов
                            bool subjectsUpdated = dbHelper.UpdateTeacherSubjects(userId, selectedSubjects);

                            if (subjectsUpdated)
                            {
                                MessageBox.Show($"Учитель {fullName} успешно добавлен!\nПредметы: {subjectsString}",
                                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show($"Учитель добавлен, но предметы не сохранены.\nПредметы: {subjectsString}",
                                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                            dialog.Close();
                            LoadTeachers();
                        }
                        else
                        {
                            MessageBox.Show("Ошибка добавления учителя", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Ошибка создания пользователя", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            buttonPanel.Children.Add(btnAdd);

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                Width = 100,
                Height = 35
            };
            btnCancel.Click += (buttonSender, args) => dialog.Close();
            buttonPanel.Children.Add(btnCancel);

            panel.Children.Add(buttonPanel);
            scrollViewer.Content = panel;
            dialog.Content = scrollViewer;
            dialog.ShowDialog();
        }

        private void btnDeleteTeacher_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var teacher = button?.Tag as TeacherViewModel;

            if (teacher != null)
            {
                var result = MessageBox.Show($"Вы действительно хотите удалить учителя {teacher.FullName}?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        bool deleted = dbHelper.DeleteTeacher(teacher.Id);
                        if (deleted)
                        {
                            MessageBox.Show("Учитель удален", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadTeachers();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // ============================================================
        // КАБИНЕТЫ
        // ============================================================

        private void btnAddRoom_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "➕ Добавление кабинета",
                Width = 450,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var headerText = new TextBlock
            {
                Text = "🚪 Добавление нового кабинета",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192))
            };
            headerBorder.Child = headerText;
            panel.Children.Add(headerBorder);

            panel.Children.Add(new TextBlock
            {
                Text = "Номер кабинета:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtRoomNumber = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(txtRoomNumber);

            panel.Children.Add(new TextBlock
            {
                Text = "Предмет (для специализированного кабинета):",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var cmbSubject = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                IsEditable = true
            };
            try
            {
                var subjects = dbHelper.GetAllSubjectsWithHours();
                cmbSubject.Items.Add("Универсальный");
                foreach (var subject in subjects.OrderBy(s => s.Name))
                {
                    cmbSubject.Items.Add(subject.Name);
                }
                cmbSubject.SelectedIndex = 0;
            }
            catch { }
            panel.Children.Add(cmbSubject);

            panel.Children.Add(new TextBlock
            {
                Text = "Ответственный учитель:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var cmbTeacher = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                FontSize = 13
            };
            try
            {
                var teachers = dbHelper.GetAllTeachersWithDetails();
                cmbTeacher.Items.Add("Не назначен");
                foreach (var teacher in teachers.OrderBy(t => t.FullName))
                {
                    cmbTeacher.Items.Add(teacher.FullName);
                }
                cmbTeacher.SelectedIndex = 0;
            }
            catch { }
            panel.Children.Add(cmbTeacher);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button btnAdd = new Button
            {
                Content = "Добавить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Margin = new Thickness(0, 0, 10, 0),
                Width = 120,
                Height = 35
            };
            btnAdd.Click += (buttonSender, args) =>
            {
                if (string.IsNullOrWhiteSpace(txtRoomNumber.Text))
                {
                    MessageBox.Show("Введите номер кабинета", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    string roomNumber = txtRoomNumber.Text.Trim();
                    string subject = cmbSubject.SelectedItem?.ToString() ?? "Универсальный";
                    string teacherName = cmbTeacher.SelectedItem?.ToString() ?? "";
                    if (teacherName == "Не назначен") teacherName = "";

                    var existingRooms = dbHelper.GetAllRooms();
                    if (existingRooms.Any(r => r.Number == roomNumber))
                    {
                        MessageBox.Show("Кабинет с таким номером уже существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    int roomId = dbHelper.AddRoom(roomNumber, subject, teacherName);
                    if (roomId > 0)
                    {
                        MessageBox.Show($"Кабинет {roomNumber} успешно добавлен!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        dialog.Close();
                        LoadRooms();
                    }
                    else
                    {
                        MessageBox.Show("Ошибка добавления кабинета", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            buttonPanel.Children.Add(btnAdd);

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                Width = 100,
                Height = 35
            };
            btnCancel.Click += (buttonSender, args) => dialog.Close();
            buttonPanel.Children.Add(btnCancel);

            panel.Children.Add(buttonPanel);
            dialog.Content = panel;
            dialog.ShowDialog();
        }

        private void btnEditRoom_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var room = button?.Tag as RoomViewModel;
            if (room == null) return;

            var dialog = new Window
            {
                Title = "✏️ Редактирование кабинета",
                Width = 450,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var headerText = new TextBlock
            {
                Text = $"✏️ Редактирование кабинета: {room.Number}",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192))
            };
            headerBorder.Child = headerText;
            panel.Children.Add(headerBorder);

            panel.Children.Add(new TextBlock
            {
                Text = "Номер кабинета:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtRoomNumber = new TextBox
            {
                Text = room.Number,
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(txtRoomNumber);

            panel.Children.Add(new TextBlock
            {
                Text = "Предмет:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var cmbSubject = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                IsEditable = true
            };
            try
            {
                var subjects = dbHelper.GetAllSubjectsWithHours();
                cmbSubject.Items.Add("Универсальный");
                foreach (var subject in subjects.OrderBy(s => s.Name))
                {
                    cmbSubject.Items.Add(subject.Name);
                }
                cmbSubject.SelectedItem = string.IsNullOrEmpty(room.Subject) ? "Универсальный" : room.Subject;
            }
            catch { }
            panel.Children.Add(cmbSubject);

            panel.Children.Add(new TextBlock
            {
                Text = "Ответственный учитель:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var cmbTeacher = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                FontSize = 13
            };
            try
            {
                var teachers = dbHelper.GetAllTeachersWithDetails();
                cmbTeacher.Items.Add("Не назначен");
                foreach (var teacher in teachers.OrderBy(t => t.FullName))
                {
                    cmbTeacher.Items.Add(teacher.FullName);
                }
                cmbTeacher.SelectedItem = string.IsNullOrEmpty(room.TeacherName) ? "Не назначен" : room.TeacherName;
            }
            catch { }
            panel.Children.Add(cmbTeacher);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button btnSave = new Button
            {
                Content = "💾 Сохранить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Margin = new Thickness(0, 0, 10, 0),
                Width = 120,
                Height = 35
            };
            btnSave.Click += (buttonSender, args) =>
            {
                if (string.IsNullOrWhiteSpace(txtRoomNumber.Text))
                {
                    MessageBox.Show("Введите номер кабинета", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    string roomNumber = txtRoomNumber.Text.Trim();
                    string subject = cmbSubject.SelectedItem?.ToString() ?? "Универсальный";
                    string teacherName = cmbTeacher.SelectedItem?.ToString() ?? "";
                    if (teacherName == "Не назначен") teacherName = "";

                    bool success = dbHelper.UpdateRoom(room.Id, roomNumber, subject, teacherName);

                    if (success)
                    {
                        MessageBox.Show("Данные кабинета обновлены!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        dialog.Close();
                        LoadRooms();
                    }
                    else
                    {
                        MessageBox.Show("Ошибка обновления данных", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            buttonPanel.Children.Add(btnSave);

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                Width = 100,
                Height = 35
            };
            btnCancel.Click += (buttonSender, args) => dialog.Close();
            buttonPanel.Children.Add(btnCancel);

            panel.Children.Add(buttonPanel);
            dialog.Content = panel;
            dialog.ShowDialog();
        }

        private void btnDeleteRoom_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var room = button?.Tag as RoomViewModel;

            if (room != null)
            {
                var result = MessageBox.Show($"Вы действительно хотите удалить кабинет {room.Number}?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        bool deleted = dbHelper.DeleteRoom(room.Id);
                        if (deleted)
                        {
                            MessageBox.Show("Кабинет удален", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadRooms();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // ============================================================
        // КЛАССЫ
        // ============================================================

        private void LoadClasses()
        {
            try
            {
                var classes = dbHelper.GetAllClassesWithInfo();
                var classViewModels = classes.Select(classItem => new ClassViewModel
                {
                    Id = classItem.Id,
                    Name = classItem.Name,
                    Grade = classItem.Grade,
                    StudentCount = classItem.StudentCount,
                    PlanName = classItem.PlanName,
                    PlanId = classItem.PlanId
                }).ToList();

                lvClasses.ItemsSource = classViewModels;

                int totalClasses = classViewModels.Count;
                int totalStudents = classViewModels.Sum(classItem => classItem.StudentCount);
                double avgStudents = totalClasses > 0 ? (double)totalStudents / totalClasses : 0;

                txtClassStats.Text = $"Всего классов: {totalClasses}";
                txtTotalStudents.Text = $"Всего учеников: {totalStudents}";
                txtAvgStudents.Text = $"Среднее: {avgStudents:F1}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки классов: {ex.Message}");
            }
        }

        private void btnAddClass_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "➕ Добавление класса",
                Width = 400,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var headerText = new TextBlock
            {
                Text = "🏫 Добавление нового класса",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192))
            };
            headerBorder.Child = headerText;
            panel.Children.Add(headerBorder);

            panel.Children.Add(new TextBlock
            {
                Text = "Название класса:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtClassName = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(txtClassName);

            panel.Children.Add(new TextBlock
            {
                Text = "Параллель:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var cmbGrade = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                FontSize = 13
            };
            for (int i = 5; i <= 11; i++)
            {
                cmbGrade.Items.Add(i);
            }
            cmbGrade.SelectedIndex = 0;
            panel.Children.Add(cmbGrade);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button btnAdd = new Button
            {
                Content = "Добавить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Margin = new Thickness(0, 0, 10, 0),
                Width = 120,
                Height = 35
            };
            btnAdd.Click += (buttonSender, args) =>
            {
                string className = txtClassName.Text.Trim();
                if (string.IsNullOrEmpty(className))
                {
                    MessageBox.Show("Введите название класса", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (cmbGrade.SelectedItem == null)
                {
                    MessageBox.Show("Выберите параллель", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var existingClasses = dbHelper.GetAllClassesWithInfo();
                if (existingClasses.Any(c => c.Name == className))
                {
                    MessageBox.Show($"Класс '{className}' уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int grade = (int)cmbGrade.SelectedItem;
                int classId = dbHelper.AddClass(className, grade);

                if (classId > 0)
                {
                    MessageBox.Show($"Класс '{className}' успешно добавлен!\n\nНе забудьте добавить учебный план через кнопку 'Добавить учебный план'",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    dialog.Close();
                    LoadClasses();
                    LoadClassPlans();
                }
                else
                {
                    MessageBox.Show("Ошибка добавления класса", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            buttonPanel.Children.Add(btnAdd);

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                Width = 100,
                Height = 35
            };
            btnCancel.Click += (buttonSender, args) => dialog.Close();
            buttonPanel.Children.Add(btnCancel);

            panel.Children.Add(buttonPanel);
            dialog.Content = panel;
            dialog.ShowDialog();
        }

        private void btnAddMultipleClasses_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "➕ Добавление нескольких классов",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var headerText = new TextBlock
            {
                Text = "🏫 Добавление нескольких классов",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192))
            };
            headerBorder.Child = headerText;
            panel.Children.Add(headerBorder);

            panel.Children.Add(new TextBlock
            {
                Text = "Выберите параллель:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });

            var cmbGrade = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13
            };
            for (int i = 5; i <= 11; i++)
            {
                cmbGrade.Items.Add($"{i} класс");
            }
            cmbGrade.SelectedIndex = 0;
            panel.Children.Add(cmbGrade);

            panel.Children.Add(new TextBlock
            {
                Text = "Количество классов:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });

            var txtCount = new TextBox
            {
                Text = "1",
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(txtCount);

            panel.Children.Add(new TextBlock
            {
                Text = "Буквы классов (через запятую, например: А,Б,В):",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });

            var txtLetters = new TextBox
            {
                Text = "А,Б",
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(txtLetters);

            var infoBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var infoText = new TextBlock
            {
                Text = "💡 Будет создано указанное количество классов с номерами от 1 до N",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 81, 0)),
                TextWrapping = TextWrapping.Wrap
            };
            infoBorder.Child = infoText;
            panel.Children.Add(infoBorder);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button btnAdd = new Button
            {
                Content = "Добавить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Margin = new Thickness(0, 0, 10, 0),
                Width = 120,
                Height = 35
            };
            btnAdd.Click += (buttonSender, args) =>
            {
                if (!int.TryParse(txtCount.Text, out int count) || count < 1 || count > 10)
                {
                    MessageBox.Show("Введите количество классов (1-10)", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtLetters.Text))
                {
                    MessageBox.Show("Введите буквы классов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var letters = txtLetters.Text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (letters.Length != count)
                {
                    MessageBox.Show($"Количество букв ({letters.Length}) не совпадает с количеством классов ({count})",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string gradeText = cmbGrade.SelectedItem?.ToString() ?? "";
                int grade = int.Parse(gradeText.Split(' ')[0]);

                try
                {
                    var existingClasses = dbHelper.GetAllClassesWithInfo();
                    int added = 0;
                    int skipped = 0;

                    for (int i = 0; i < count; i++)
                    {
                        string className = $"{grade}{letters[i].Trim()}";

                        if (existingClasses.Any(c => c.Name == className))
                        {
                            skipped++;
                            continue;
                        }

                        int classId = dbHelper.AddClass(className, grade);
                        if (classId > 0)
                        {
                            added++;
                        }
                    }

                    string message = $"Добавлено классов: {added}\n";
                    if (skipped > 0)
                    {
                        message += $"Пропущено (уже существуют): {skipped}";
                    }

                    MessageBox.Show(message, "Результат",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    if (added > 0)
                    {
                        dialog.Close();
                        LoadClasses();
                        LoadClassPlans();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            buttonPanel.Children.Add(btnAdd);

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                Width = 100,
                Height = 35
            };
            btnCancel.Click += (buttonSender, args) => dialog.Close();
            buttonPanel.Children.Add(btnCancel);

            panel.Children.Add(buttonPanel);
            dialog.Content = panel;
            dialog.ShowDialog();
        }

        private void btnEditClass_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var classVM = button?.Tag as ClassViewModel;
            if (classVM == null) return;

            var dialog = new Window
            {
                Title = "✏️ Редактирование класса",
                Width = 400,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                ResizeMode = ResizeMode.NoResize
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var headerText = new TextBlock
            {
                Text = $"✏️ Редактирование класса: {classVM.Name}",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192))
            };
            headerBorder.Child = headerText;
            panel.Children.Add(headerBorder);

            panel.Children.Add(new TextBlock
            {
                Text = "Название класса:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtClassName = new TextBox
            {
                Text = classVM.Name,
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(txtClassName);

            panel.Children.Add(new TextBlock
            {
                Text = "Параллель:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var cmbGrade = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                FontSize = 13
            };
            for (int i = 5; i <= 11; i++)
            {
                cmbGrade.Items.Add(i);
            }
            cmbGrade.SelectedItem = classVM.Grade;
            panel.Children.Add(cmbGrade);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button btnSave = new Button
            {
                Content = "💾 Сохранить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Margin = new Thickness(0, 0, 10, 0),
                Width = 120,
                Height = 35
            };
            btnSave.Click += (buttonSender, args) =>
            {
                if (string.IsNullOrWhiteSpace(txtClassName.Text))
                {
                    MessageBox.Show("Введите название класса", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (cmbGrade.SelectedItem == null)
                {
                    MessageBox.Show("Выберите параллель", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string newName = txtClassName.Text.Trim();
                int grade = (int)cmbGrade.SelectedItem;

                try
                {
                    var existingClasses = dbHelper.GetAllClassesWithInfo();
                    if (existingClasses.Any(c => c.Name == newName && c.Id != classVM.Id))
                    {
                        MessageBox.Show("Класс с таким названием уже существует", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    bool success = dbHelper.UpdateClass(classVM.Id, newName, grade);

                    if (success)
                    {
                        MessageBox.Show("Данные класса обновлены!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        dialog.Close();
                        LoadClasses();
                        LoadClassPlans();
                    }
                    else
                    {
                        MessageBox.Show("Ошибка обновления данных", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            buttonPanel.Children.Add(btnSave);

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                Width = 100,
                Height = 35
            };
            btnCancel.Click += (buttonSender, args) => dialog.Close();
            buttonPanel.Children.Add(btnCancel);

            panel.Children.Add(buttonPanel);
            dialog.Content = panel;
            dialog.ShowDialog();
        }

        private void btnDeleteClass_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var classVM = button?.Tag as ClassViewModel;

            if (classVM != null)
            {
                var result = MessageBox.Show($"Вы действительно хотите удалить класс {classVM.Name}?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        bool deleted = dbHelper.DeleteClass(classVM.Id);
                        if (deleted)
                        {
                            MessageBox.Show("Класс удален", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadClasses();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        // ============================================================
        // ШКАЛА ТРУДНОСТИ
        // ============================================================

        private void btnSaveDifficulty_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var difficulties = dgDifficulty.ItemsSource as List<DbHelper.SubjectDifficulty>;
                if (difficulties == null) return;

                int updated = 0;
                foreach (var difficulty in difficulties)
                {
                    if (dbHelper.UpdateSubjectDifficulty(difficulty))
                        updated++;
                }

                MessageBox.Show($"Обновлено {updated} записей", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                LoadDifficultyScale();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnRefreshDifficulty_Click(object sender, RoutedEventArgs e)
        {
            LoadDifficultyScale();
        }

        // ============================================================
        // УДАЛЕНИЕ ПРЕДМЕТА
        // ============================================================

        private void btnDeleteSubject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var allSubjects = dbHelper.GetAllSubjectsWithHours();

                if (!allSubjects.Any())
                {
                    MessageBox.Show("Нет предметов для удаления", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dialog = new Window
                {
                    Title = "🗑 Удаление предмета",
                    Width = 450,
                    Height = 350,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                    ResizeMode = ResizeMode.NoResize
                };

                var panel = new StackPanel { Margin = new Thickness(20) };

                var headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(255, 235, 238)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                var headerText = new TextBlock
                {
                    Text = "⚠️ Удаление предмета",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40))
                };
                headerBorder.Child = headerText;
                panel.Children.Add(headerBorder);

                panel.Children.Add(new TextBlock
                {
                    Text = "Выберите предмет для удаления:",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                    Margin = new Thickness(0, 0, 0, 10)
                });

                var cmbSubject = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10),
                    FontSize = 13
                };

                foreach (var subject in allSubjects.OrderBy(s => s.SortOrder ?? 999).ThenBy(s => s.Name))
                {
                    cmbSubject.Items.Add(subject.Name);
                }

                if (cmbSubject.Items.Count > 0) cmbSubject.SelectedIndex = 0;
                panel.Children.Add(cmbSubject);

                var infoBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                var infoText = new TextBlock
                {
                    Text = "⚠️ ВНИМАНИЕ! Удаление предмета приведет к:\n" +
                           "• Удалению из всех учебных планов классов\n" +
                           "• Удалению из шкалы трудности\n" +
                           "• Все данные по этому предмету будут потеряны",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(230, 81, 0)),
                    TextWrapping = TextWrapping.Wrap
                };
                infoBorder.Child = infoText;
                panel.Children.Add(infoBorder);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                Button btnDelete = new Button
                {
                    Content = "🗑 Удалить",
                    Style = (Style)FindResource("MainButtonStyle"),
                    Background = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    Margin = new Thickness(0, 0, 10, 0),
                    Width = 100,
                    Height = 32
                };
                btnDelete.Click += (buttonSender, args) =>
                {
                    if (cmbSubject.SelectedItem == null)
                    {
                        MessageBox.Show("Выберите предмет для удаления", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    string subjectName = cmbSubject.SelectedItem.ToString();

                    var result = MessageBox.Show(
                        $"Вы действительно хотите удалить предмет '{subjectName}'?\n\n" +
                        "Это действие необратимо! Предмет будет удален из:\n" +
                        "• Всех учебных планов классов\n" +
                        "• Шкалы трудности\n\n" +
                        "Продолжить?",
                        "Подтверждение удаления",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        bool success = DeleteSubjectFromAllPlans(subjectName);

                        if (success)
                        {
                            MessageBox.Show($"Предмет '{subjectName}' успешно удален!", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                            dialog.Close();
                            LoadClassPlans();
                            LoadDifficultyScale();
                        }
                        else
                        {
                            MessageBox.Show($"Ошибка при удалении предмета '{subjectName}'", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                };
                buttonPanel.Children.Add(btnDelete);

                Button btnCancel = new Button
                {
                    Content = "Отмена",
                    Style = (Style)FindResource("MainButtonStyle"),
                    Background = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    Width = 100,
                    Height = 32
                };
                btnCancel.Click += (buttonSender, args) => dialog.Close();
                buttonPanel.Children.Add(btnCancel);

                panel.Children.Add(buttonPanel);
                dialog.Content = panel;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool DeleteSubjectFromAllPlans(string subjectName)
        {
            try
            {
                var allClasses = dbHelper.GetAllClassesWithInfo();
                bool success = true;

                foreach (var classItem in allClasses)
                {
                    var plan = dbHelper.GetPlanForClass(classItem.Name);
                    if (plan != null)
                    {
                        var subjectToRemove = plan.Subjects.FirstOrDefault(s => s.SubjectName == subjectName);
                        if (subjectToRemove != null)
                        {
                            plan.Subjects.Remove(subjectToRemove);
                            int order = 1;
                            foreach (var subj in plan.Subjects.OrderBy(s => s.SortOrder))
                            {
                                subj.SortOrder = order++;
                            }

                            if (!dbHelper.UpdateStudyPlan(plan))
                            {
                                success = false;
                            }
                        }
                    }
                }

                if (success)
                {
                    success = dbHelper.DeleteSubjectFromDifficulty(subjectName);
                }

                if (success)
                {
                    success = dbHelper.DeleteSubject(subjectName);
                }

                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка удаления предмета: {ex.Message}");
                return false;
            }
        }
        private void btnEditTeacher_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var teacher = button?.Tag as TeacherViewModel;
            if (teacher == null) return;

            var dialog = new Window
            {
                Title = $"✏️ Редактирование учителя: {teacher.FullName}",
                Width = 850,
                Height = 780,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(240, 242, 245)),
                ResizeMode = ResizeMode.CanResize
            };

            // ============================================================
            // СОЗДАЕМ СТИЛИ ПРОГРАММНО
            // ============================================================
            // Стиль для TextBox
            var textBoxStyle = new Style(typeof(TextBox));
            textBoxStyle.Setters.Add(new Setter(TextBox.FontSizeProperty, 13.0));
            textBoxStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(12, 10, 12, 10)));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(200, 200, 200))));
            textBoxStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(1)));
            textBoxStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 255, 255))));
            textBoxStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, new SolidColorBrush(Color.FromRgb(51, 51, 51))));

            // Стиль для PasswordBox
            var passwordBoxStyle = new Style(typeof(PasswordBox));
            passwordBoxStyle.Setters.Add(new Setter(PasswordBox.FontSizeProperty, 13.0));
            passwordBoxStyle.Setters.Add(new Setter(PasswordBox.PaddingProperty, new Thickness(12, 10, 12, 10)));
            passwordBoxStyle.Setters.Add(new Setter(PasswordBox.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(200, 200, 200))));
            passwordBoxStyle.Setters.Add(new Setter(PasswordBox.BorderThicknessProperty, new Thickness(1)));
            passwordBoxStyle.Setters.Add(new Setter(PasswordBox.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 255, 255))));

            // Стиль для ComboBox
            var comboBoxStyle = new Style(typeof(ComboBox));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.FontSizeProperty, 13.0));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.PaddingProperty, new Thickness(10, 8, 10, 8)));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.HeightProperty, 36.0));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 255, 255))));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(200, 200, 200))));
            comboBoxStyle.Setters.Add(new Setter(ComboBox.BorderThicknessProperty, new Thickness(1)));

            // Стиль для Button (зеленый)
            var greenButtonStyle = new Style(typeof(Button));
            greenButtonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(76, 175, 80))));
            greenButtonStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
            greenButtonStyle.Setters.Add(new Setter(Button.FontWeightProperty, FontWeights.SemiBold));
            greenButtonStyle.Setters.Add(new Setter(Button.FontSizeProperty, 13.0));
            greenButtonStyle.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
            greenButtonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            greenButtonStyle.Setters.Add(new Setter(Button.HeightProperty, 36.0));

            var greenTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            greenTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(56, 142, 60))));
            greenButtonStyle.Triggers.Add(greenTrigger);

            // Стиль для Button (красный)
            var redButtonStyle = new Style(typeof(Button));
            redButtonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(244, 67, 54))));
            redButtonStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
            redButtonStyle.Setters.Add(new Setter(Button.FontWeightProperty, FontWeights.SemiBold));
            redButtonStyle.Setters.Add(new Setter(Button.FontSizeProperty, 13.0));
            redButtonStyle.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
            redButtonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            redButtonStyle.Setters.Add(new Setter(Button.HeightProperty, 36.0));

            var redTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            redTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(211, 47, 47))));
            redButtonStyle.Triggers.Add(redTrigger);

            // Стиль для Button (синий)
            var blueButtonStyle = new Style(typeof(Button));
            blueButtonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(33, 150, 243))));
            blueButtonStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
            blueButtonStyle.Setters.Add(new Setter(Button.FontWeightProperty, FontWeights.Bold));
            blueButtonStyle.Setters.Add(new Setter(Button.FontSizeProperty, 14.0));
            blueButtonStyle.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
            blueButtonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            blueButtonStyle.Setters.Add(new Setter(Button.HeightProperty, 42.0));
            blueButtonStyle.Setters.Add(new Setter(Button.WidthProperty, 150.0));

            var blueTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            blueTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(21, 101, 192))));
            blueButtonStyle.Triggers.Add(blueTrigger);

            // Стиль для Button (серый)
            var grayButtonStyle = new Style(typeof(Button));
            grayButtonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(158, 158, 158))));
            grayButtonStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
            grayButtonStyle.Setters.Add(new Setter(Button.FontSizeProperty, 14.0));
            grayButtonStyle.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
            grayButtonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            grayButtonStyle.Setters.Add(new Setter(Button.HeightProperty, 42.0));
            grayButtonStyle.Setters.Add(new Setter(Button.WidthProperty, 120.0));

            var grayTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            grayTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(117, 117, 117))));
            grayButtonStyle.Triggers.Add(grayTrigger);

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(240, 242, 245))
            };

            var mainPanel = new StackPanel { Margin = new Thickness(25, 20, 25, 20) };

            // ============================================================
            // ЗАГОЛОВОК С АВАТАРОМ
            // ============================================================
            var headerPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var avatarBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                CornerRadius = new CornerRadius(30),
                Width = 60,
                Height = 60,
                Margin = new Thickness(0, 0, 15, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var avatarText = new TextBlock
            {
                Text = "👨‍🏫",
                FontSize = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            avatarBorder.Child = avatarText;

            var headerTextPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            headerTextPanel.Children.Add(new TextBlock
            {
                Text = "Редактирование учителя",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192))
            });
            headerTextPanel.Children.Add(new TextBlock
            {
                Text = teacher.FullName,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 2, 0, 0)
            });

            headerPanel.Children.Add(avatarBorder);
            headerPanel.Children.Add(headerTextPanel);
            mainPanel.Children.Add(headerPanel);

            // ============================================================
            // КАРТОЧКА "ОСНОВНЫЕ ДАННЫЕ" - ВСЕ ПОЛЯ УЧИТЕЛЯ
            // ============================================================
            var infoBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 15),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 15,
                    Opacity = 0.08,
                    ShadowDepth = 3
                }
            };

            var infoPanel = new StackPanel();

            // Заголовок
            infoPanel.Children.Add(new TextBlock
            {
                Text = "📋 Основные данные",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33)),
                Margin = new Thickness(0, 0, 0, 15)
            });

            // ФИО
            infoPanel.Children.Add(new TextBlock
            {
                Text = "👤 ФИО учителя:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtFullName = new TextBox
            {
                Text = teacher.FullName,
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            infoPanel.Children.Add(txtFullName);

            // Логин
            infoPanel.Children.Add(new TextBlock
            {
                Text = "🔑 Логин:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtUsername = new TextBox
            {
                Text = teacher.Username,
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            infoPanel.Children.Add(txtUsername);

            // Пароль
            infoPanel.Children.Add(new TextBlock
            {
                Text = "🔒 Новый пароль (оставьте пустым, если не менять):",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtPassword = new PasswordBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            infoPanel.Children.Add(txtPassword);

            // Подтверждение пароля
            infoPanel.Children.Add(new TextBlock
            {
                Text = "✅ Подтверждение нового пароля:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtConfirmPassword = new PasswordBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            infoPanel.Children.Add(txtConfirmPassword);

            // Максимум часов
            infoPanel.Children.Add(new TextBlock
            {
                Text = "⏱ Максимальное количество часов в неделю:",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtMaxHours = new TextBox
            {
                Text = teacher.MaxHours.ToString(),
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            infoPanel.Children.Add(txtMaxHours);

            // Кабинет
            infoPanel.Children.Add(new TextBlock
            {
                Text = "🚪 Кабинет (номер):",
                Foreground = new SolidColorBrush(Color.FromRgb(85, 85, 85)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtRoom = new TextBox
            {
                Text = teacher.Room,
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 13,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            infoPanel.Children.Add(txtRoom);

            infoBorder.Child = infoPanel;
            mainPanel.Children.Add(infoBorder);

            // ============================================================
            // КАРТОЧКА "ПРЕДМЕТЫ ПО КЛАССАМ"
            // ============================================================
            var subjectsCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 15),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 15,
                    Opacity = 0.08,
                    ShadowDepth = 3
                }
            };

            var subjectsStack = new StackPanel();

            var subjectsTitle = new TextBlock
            {
                Text = "📚 Предметы по классам",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33)),
                Margin = new Thickness(0, 0, 0, 15)
            };
            subjectsStack.Children.Add(subjectsTitle);

            // Хранилище для данных
            var classSubjectPairs = new List<ClassSubjectPair>();
            var allClasses = dbHelper.GetAllClasses();
            var allSubjects = dbHelper.GetAllSubjects();
            var currentSubjectsByClass = dbHelper.GetTeacherSubjectsByClass(teacher.Id);

            if (currentSubjectsByClass != null)
            {
                foreach (var kvp in currentSubjectsByClass)
                {
                    foreach (var subject in kvp.Value)
                    {
                        classSubjectPairs.Add(new ClassSubjectPair
                        {
                            ClassName = kvp.Key,
                            SubjectName = subject
                        });
                    }
                }
            }

            // Панель добавления
            var addPanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var addGrid = new Grid();
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            addGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            addGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ComboBox класс
            var cmbClass = new ComboBox
            {
                Margin = new Thickness(0, 0, 8, 0),
                Style = comboBoxStyle
            };
            if (allClasses != null)
            {
                foreach (var c in allClasses.OrderBy(x => x.Name))
                {
                    cmbClass.Items.Add(c.Name);
                }
                if (cmbClass.Items.Count > 0) cmbClass.SelectedIndex = 0;
            }
            Grid.SetRow(cmbClass, 0);
            Grid.SetColumn(cmbClass, 0);
            addGrid.Children.Add(cmbClass);

            // ComboBox предмет
            var cmbSubject = new ComboBox
            {
                Margin = new Thickness(8, 0, 8, 0),
                Style = comboBoxStyle
            };
            if (allSubjects != null)
            {
                foreach (var s in allSubjects.OrderBy(x => x.Name))
                {
                    cmbSubject.Items.Add(s.Name);
                }
                if (cmbSubject.Items.Count > 0) cmbSubject.SelectedIndex = 0;
            }
            Grid.SetRow(cmbSubject, 0);
            Grid.SetColumn(cmbSubject, 1);
            addGrid.Children.Add(cmbSubject);

            // Кнопка Добавить
            var btnAdd = new Button
            {
                Content = "➕ Добавить",
                Width = 100,
                Margin = new Thickness(8, 0, 5, 0),
                Style = greenButtonStyle
            };
            Grid.SetRow(btnAdd, 0);
            Grid.SetColumn(btnAdd, 2);
            addGrid.Children.Add(btnAdd);

            // Кнопка Удалить
            var btnRemove = new Button
            {
                Content = "➖ Удалить",
                Width = 100,
                Margin = new Thickness(5, 0, 0, 0),
                Style = redButtonStyle
            };
            Grid.SetRow(btnRemove, 0);
            Grid.SetColumn(btnRemove, 3);
            addGrid.Children.Add(btnRemove);

            addPanel.Child = addGrid;
            subjectsStack.Children.Add(addPanel);

            // Список назначенных предметов
            var listView = new ListView
            {
                Margin = new Thickness(0, 0, 0, 0),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                Height = 180,
                SelectionMode = SelectionMode.Single
            };

            var gridView = new GridView();
            gridView.Columns.Add(new GridViewColumn
            {
                Header = "🏫 Класс",
                Width = 180,
                DisplayMemberBinding = new System.Windows.Data.Binding("ClassName")
            });
            gridView.Columns.Add(new GridViewColumn
            {
                Header = "📖 Предмет",
                Width = 250,
                DisplayMemberBinding = new System.Windows.Data.Binding("SubjectName")
            });
            listView.View = gridView;

            listView.ItemsSource = classSubjectPairs.OrderBy(x => x.ClassName).ThenBy(x => x.SubjectName).ToList();
            subjectsStack.Children.Add(listView);

            // Счетчик
            var counterPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var counterText = new TextBlock
            {
                Text = $"📊 Всего назначений: {classSubjectPairs.Count}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
            };
            counterPanel.Children.Add(counterText);

            void UpdateCounter()
            {
                counterText.Text = $"📊 Всего назначений: {classSubjectPairs.Count}";
            }

            subjectsStack.Children.Add(counterPanel);
            subjectsCard.Child = subjectsStack;
            mainPanel.Children.Add(subjectsCard);

            // ============================================================
            // ИНФОРМАЦИОННАЯ ПАНЕЛЬ
            // ============================================================
            var infoPanelBottom = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };
            var infoTextBlock = new TextBlock
            {
                Text = "💡 Выберите класс и предмет → нажмите 'Добавить'. Для удаления выберите строку и нажмите 'Удалить' или клавишу Delete.",
                Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            infoPanelBottom.Child = infoTextBlock;
            mainPanel.Children.Add(infoPanelBottom);

            // ============================================================
            // КНОПКИ ДЕЙСТВИЙ
            // ============================================================
            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 5, 0, 0)
            };

            // Кнопка Сохранить
            var btnSave = new Button
            {
                Content = "💾 Сохранить",
                Margin = new Thickness(0, 0, 10, 0),
                Style = blueButtonStyle
            };

            btnSave.Click += (senderBtn, args) =>
            {
                if (string.IsNullOrWhiteSpace(txtFullName.Text))
                {
                    MessageBox.Show("Введите ФИО учителя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(txtUsername.Text))
                {
                    MessageBox.Show("Введите логин", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(txtMaxHours.Text, out int maxHours) || maxHours <= 0 || maxHours > 40)
                {
                    MessageBox.Show("Введите корректное количество часов (1-40)", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string newPassword = txtPassword.Password;
                if (!string.IsNullOrEmpty(newPassword))
                {
                    if (newPassword.Length < 6)
                    {
                        MessageBox.Show("Пароль должен содержать минимум 6 символов", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (newPassword != txtConfirmPassword.Password)
                    {
                        MessageBox.Show("Пароли не совпадают", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                try
                {
                    var allSubjectsList = dbHelper.GetAllSubjects();
                    var selectedData = new Dictionary<int, List<int>>();

                    foreach (var pair in classSubjectPairs)
                    {
                        var classItem = allClasses.FirstOrDefault(c => c.Name == pair.ClassName);
                        if (classItem == null) continue;

                        var subject = allSubjectsList.FirstOrDefault(s => s.Name == pair.SubjectName);
                        if (subject == null) continue;

                        if (!selectedData.ContainsKey(classItem.Id))
                        {
                            selectedData[classItem.Id] = new List<int>();
                        }

                        if (!selectedData[classItem.Id].Contains(subject.Id))
                        {
                            selectedData[classItem.Id].Add(subject.Id);
                        }
                    }

                    string hashedPassword = string.IsNullOrEmpty(newPassword) ? null : HashPassword(newPassword);

                    bool updated = dbHelper.UpdateTeacherFull(
                        teacher.Id,
                        txtFullName.Text.Trim(),
                        txtUsername.Text.Trim(),
                        hashedPassword,
                        new List<string>(),
                        maxHours,
                        txtRoom.Text.Trim()
                    );

                    if (updated)
                    {
                        dbHelper.SaveTeacherSubjects(teacher.Id, selectedData);

                        MessageBox.Show("✅ Данные учителя обновлены!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        dialog.Close();
                        LoadTeachers();
                    }
                    else
                    {
                        MessageBox.Show("Ошибка обновления данных", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            actionPanel.Children.Add(btnSave);

            // Кнопка Отмена
            var btnCancel = new Button
            {
                Content = "✕ Отмена",
                Style = grayButtonStyle
            };
            btnCancel.Click += (senderBtn, args) => dialog.Close();
            actionPanel.Children.Add(btnCancel);

            mainPanel.Children.Add(actionPanel);

            // ============================================================
            // ОБРАБОТЧИКИ
            // ============================================================

            btnAdd.Click += (s, args) =>
            {
                if (cmbClass.SelectedItem != null && cmbSubject.SelectedItem != null)
                {
                    string className = cmbClass.SelectedItem.ToString();
                    string subjectName = cmbSubject.SelectedItem.ToString();

                    if (!classSubjectPairs.Any(x => x.ClassName == className && x.SubjectName == subjectName))
                    {
                        classSubjectPairs.Add(new ClassSubjectPair
                        {
                            ClassName = className,
                            SubjectName = subjectName
                        });
                        RefreshListView(listView, classSubjectPairs);
                        UpdateCounter();
                    }
                    else
                    {
                        MessageBox.Show("Эта пара уже добавлена!", "Информация",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            };

            btnRemove.Click += (s, args) =>
            {
                if (listView.SelectedItem is ClassSubjectPair selected)
                {
                    classSubjectPairs.Remove(selected);
                    RefreshListView(listView, classSubjectPairs);
                    UpdateCounter();
                }
                else
                {
                    MessageBox.Show("Выберите элемент для удаления", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            listView.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Delete && listView.SelectedItem is ClassSubjectPair selected)
                {
                    classSubjectPairs.Remove(selected);
                    RefreshListView(listView, classSubjectPairs);
                    UpdateCounter();
                    args.Handled = true;
                }
            };

            scrollViewer.Content = mainPanel;
            dialog.Content = scrollViewer;
            dialog.ShowDialog();
        }

        // ============================================================
        // ВСПОМОГАТЕЛЬНЫЙ КЛАСС
        // ============================================================
        public class ClassSubjectPair
        {
            public string ClassName { get; set; }
            public string SubjectName { get; set; }
        }

        // ============================================================
        // ВСПОМОГАТЕЛЬНЫЙ МЕТОД
        // ============================================================
        private void RefreshListView(ListView listView, List<ClassSubjectPair> data)
        {
            listView.ItemsSource = null;
            listView.ItemsSource = data.OrderBy(x => x.ClassName).ThenBy(x => x.SubjectName).ToList();
        }

        // ============================================================
        // ГРАФИК РАБОТЫ УЧИТЕЛЕЙ
        // ============================================================

        /// <summary>
        /// Модель для отображения графика работы учителя
        /// </summary>

        public class TeacherScheduleViewModel : System.ComponentModel.INotifyPropertyChanged
        {
            private int _teacherId;
            private string _fullName;
            private bool _monday;
            private bool _tuesday;
            private bool _wednesday;
            private bool _thursday;
            private bool _friday;

            public int TeacherId
            {
                get => _teacherId;
                set { _teacherId = value; OnPropertyChanged(); }
            }

            public string FullName
            {
                get => _fullName;
                set { _fullName = value; OnPropertyChanged(); }
            }

            public bool Monday
            {
                get => _monday;
                set
                {
                    _monday = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DaysOff));
                }
            }

            public bool Tuesday
            {
                get => _tuesday;
                set
                {
                    _tuesday = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DaysOff));
                }
            }

            public bool Wednesday
            {
                get => _wednesday;
                set
                {
                    _wednesday = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DaysOff));
                }
            }

            public bool Thursday
            {
                get => _thursday;
                set
                {
                    _thursday = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DaysOff));
                }
            }

            public bool Friday
            {
                get => _friday;
                set
                {
                    _friday = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DaysOff));
                }
            }

            public string DaysOff
            {
                get
                {
                    var days = new List<string>();
                    if (Monday) days.Add("Пн");
                    if (Tuesday) days.Add("Вт");
                    if (Wednesday) days.Add("Ср");
                    if (Thursday) days.Add("Чт");
                    if (Friday) days.Add("Пт");
                    return days.Any() ? string.Join(", ", days) : "Нет";
                }
            }

            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
            {
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
            }
        }

        /// <summary>
        /// Загружает график работы учителей
        /// </summary>
        private void LoadTeacherSchedule()
        {
            try
            {
                var teachers = dbHelper.GetAllTeachersWithDetails();
                var scheduleList = new List<TeacherScheduleViewModel>();

                foreach (var teacher in teachers)
                {
                    var daysOff = dbHelper.GetTeacherDaysOff(teacher.Id);

                    var schedule = new TeacherScheduleViewModel
                    {
                        TeacherId = teacher.Id,
                        FullName = teacher.FullName,
                        Monday = daysOff.Contains("Monday"),
                        Tuesday = daysOff.Contains("Tuesday"),
                        Wednesday = daysOff.Contains("Wednesday"),
                        Thursday = daysOff.Contains("Thursday"),
                        Friday = daysOff.Contains("Friday")
                    };

                    scheduleList.Add(schedule);
                }

                dgTeacherSchedule.ItemsSource = scheduleList;
                UpdateScheduleStats(scheduleList);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadTeacherSchedule: {ex.Message}");
            }
        }

        /// <summary>
        /// Обновляет статистику
        /// </summary>
        private void UpdateScheduleStats(List<TeacherScheduleViewModel> schedules)
        {
            try
            {
                if (schedules == null || !schedules.Any())
                {
                    txtScheduleStats.Text = "Нет учителей для отображения";
                    return;
                }

                int totalTeachers = schedules.Count;
                int totalDaysOff = 0;

                foreach (var s in schedules)
                {
                    if (s.Monday) totalDaysOff++;
                    if (s.Tuesday) totalDaysOff++;
                    if (s.Wednesday) totalDaysOff++;
                    if (s.Thursday) totalDaysOff++;
                    if (s.Friday) totalDaysOff++;
                }

                txtScheduleStats.Text = $"👨‍🏫 Всего учителей: {totalTeachers} | " +
                                       $"📅 Всего выходных дней: {totalDaysOff} | " +
                                       $"📊 В среднем: {totalDaysOff / (double)totalTeachers:F1} дней на учителя";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка UpdateScheduleStats: {ex.Message}");
            }
        }

        /// <summary>
        /// Сохраняет график работы
        /// </summary>
        private void btnSaveTeacherSchedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var schedules = new List<TeacherScheduleViewModel>();

                if (dgTeacherSchedule.ItemsSource != null)
                {
                    foreach (var item in dgTeacherSchedule.ItemsSource)
                    {
                        var schedule = item as TeacherScheduleViewModel;
                        if (schedule != null)
                        {
                            schedules.Add(schedule);
                        }
                    }
                }

                if (!schedules.Any())
                {
                    MessageBox.Show("Нет данных для сохранения", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                int saved = 0;
                foreach (var s in schedules)
                {
                    bool result = dbHelper.SaveTeacherDaysOff(
                        s.TeacherId,
                        s.Monday,
                        s.Tuesday,
                        s.Wednesday,
                        s.Thursday,
                        s.Friday
                    );
                    if (result) saved++;
                }

                MessageBox.Show($"График работы сохранен!\nОбновлено: {saved} учителей",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                LoadTeacherSchedule();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обновляет график работы
        /// </summary>
        private void btnRefreshTeacherSchedule_Click(object sender, RoutedEventArgs e)
        {
            LoadTeacherSchedule();
            MessageBox.Show("Данные обновлены", "Успех",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        // ============================================================
        // ПОКАЗАТЬ ПРЕДМЕТЫ УЧИТЕЛЯ ПО КЛАССАМ
        // ============================================================

        private void btnShowTeacherSubjects_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var teacher = button?.Tag as TeacherViewModel;
            if (teacher == null) return;

            try
            {
                // Получаем предметы учителя по классам из расписания
                var subjectsByClass = dbHelper.GetTeacherSubjectsByClass(teacher.Id);

                if (!subjectsByClass.Any())
                {
                    MessageBox.Show($"У учителя {teacher.FullName} нет уроков в расписании",
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Создаем окно для отображения
                var dialog = new Window
                {
                    Title = $"📚 Предметы учителя: {teacher.FullName}",
                    Width = 550,
                    Height = 450,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                    ResizeMode = ResizeMode.NoResize
                };

                var mainPanel = new StackPanel { Margin = new Thickness(20) };

                // ============================================================
                // ЗАГОЛОВОК
                // ============================================================
                var headerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                var headerText = new TextBlock
                {
                    Text = $"👨‍🏫 {teacher.FullName}",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192))
                };
                headerBorder.Child = headerText;
                mainPanel.Children.Add(headerBorder);

                // Информация
                var infoPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"📊 Всего классов: {subjectsByClass.Count}",
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 20, 0)
                });

                int totalSubjects = subjectsByClass.Values.Sum(list => list.Count);
                infoPanel.Children.Add(new TextBlock
                {
                    Text = $"📖 Всего предметов: {totalSubjects}",
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    FontSize = 13
                });
                mainPanel.Children.Add(infoPanel);

                // ============================================================
                // СПИСОК КЛАССОВ И ПРЕДМЕТОВ
                // ============================================================
                var listView = new ListView
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    BorderThickness = new Thickness(1),
                    Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                    Height = 250
                };

                var gridView = new GridView();
                gridView.Columns.Add(new GridViewColumn
                {
                    Header = "🏫 Класс",
                    Width = 120,
                    DisplayMemberBinding = new System.Windows.Data.Binding("ClassName")
                });
                gridView.Columns.Add(new GridViewColumn
                {
                    Header = "📖 Предметы",
                    Width = 320,
                    DisplayMemberBinding = new System.Windows.Data.Binding("Subjects")
                });

                listView.View = gridView;

                // Заполняем данные
                var items = new List<ClassSubjectItem>();
                foreach (var item in subjectsByClass.OrderBy(x => x.Key))
                {
                    items.Add(new ClassSubjectItem
                    {
                        ClassName = item.Key,
                        Subjects = string.Join(", ", item.Value)
                    });
                }
                listView.ItemsSource = items;
                mainPanel.Children.Add(listView);

                // ============================================================
                // СТАТИСТИКА ПО КАЖДОМУ ПРЕДМЕТУ
                // ============================================================
                var statsBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 15)
                };

                var statsText = new TextBlock
                {
                    Text = GetTeacherSubjectStats(subjectsByClass),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(230, 81, 0)),
                    TextWrapping = TextWrapping.Wrap
                };
                statsBorder.Child = statsText;
                mainPanel.Children.Add(statsBorder);

                // ============================================================
                // КНОПКА ЗАКРЫТИЯ
                // ============================================================
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                Button btnClose = new Button
                {
                    Content = "Закрыть",
                    Style = (Style)FindResource("MainButtonStyle"),
                    Background = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                    Width = 100,
                    Height = 32
                };
                btnClose.Click += (closeSender, closeArgs) => dialog.Close();
                buttonPanel.Children.Add(btnClose);

                mainPanel.Children.Add(buttonPanel);
                dialog.Content = mainPanel;
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Вспомогательный класс для отображения предметов по классам
        /// </summary>
       

        /// <summary>
        /// Формирует статистику по предметам учителя
        /// </summary>
        private string GetTeacherSubjectStats(Dictionary<string, List<string>> subjectsByClass)
        {
            if (subjectsByClass == null || !subjectsByClass.Any())
                return "Нет данных";

            // Собираем все предметы и считаем в каких классах они ведутся
            var subjectStats = new Dictionary<string, List<string>>();
            foreach (var kvp in subjectsByClass)
            {
                string className = kvp.Key;
                foreach (var subject in kvp.Value)
                {
                    if (!subjectStats.ContainsKey(subject))
                        subjectStats[subject] = new List<string>();
                    if (!subjectStats[subject].Contains(className))
                        subjectStats[subject].Add(className);
                }
            }

            var lines = new List<string>();
            foreach (var kvp in subjectStats.OrderBy(x => x.Key))
            {
                lines.Add($"• {kvp.Key} — {string.Join(", ", kvp.Value.OrderBy(c => c))}");
            }

            return $"📊 Статистика по предметам:\n{string.Join("\n", lines)}";
        }
     
    }
}
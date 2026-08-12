using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using SchoolPlanner.Database;
using SchoolPlanner.Services;
using Microsoft.Win32;
using System.IO;

namespace SchoolPlanner.Pages
{
    public partial class SchedulePage
    {
        private DbHelper dbHelper;
        private User currentUser;
        private List<Schedule> allSchedules;
        private Schedule currentSchedule;
        private ScheduleLesson selectedLesson;
        private bool isLoaded = false;
        private bool isEditMode = false;

        private string currentSearchQuery = "";
        private List<ScheduleLesson> searchResults = new List<ScheduleLesson>();
        private string selectedClass = "Все классы";
        private bool teacherFilterEnabled = true;

        private int fontSizeLevel = 2;
        private bool compactMode = false;
        private bool showLessonPlans = true;

        public SchedulePage()
        {
            try
            {
                InitializeComponent();
                dbHelper = new DbHelper();
                currentUser = App.CurrentUser;

                EnsureScheduleGridExists();
                SetSearchPlaceholder();

                txtSearch.KeyDown += (searchSender, searchArgs) =>
                {
                    if (searchArgs.Key == Key.Enter)
                    {
                        PerformSearch();
                        searchArgs.Handled = true;
                    }
                };

                txtSearch.GotFocus += (focusSender, focusArgs) => RemoveSearchPlaceholder();
                txtSearch.LostFocus += (focusSender, focusArgs) => SetSearchPlaceholder();

                SetupRoleBasedUI();
                Loaded += Page_Loaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnsureScheduleGridExists()
        {
            try
            {
                if (scheduleTableGrid == null)
                {
                    var mainStackPanel = FindVisualChild<StackPanel>(this, "MainStackPanel");
                    if (mainStackPanel != null)
                    {
                        scheduleTableGrid = new Grid
                        {
                            Name = "scheduleTableGrid",
                            Margin = new Thickness(0, 0, 0, 20),
                            ShowGridLines = true,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch
                        };

                        scheduleTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                        scheduleTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        scheduleTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        scheduleTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        scheduleTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        scheduleTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        int index = mainStackPanel.Children.IndexOf(txtScheduleTitle);
                        if (index >= 0 && index + 1 <= mainStackPanel.Children.Count)
                        {
                            mainStackPanel.Children.Insert(index + 1, scheduleTableGrid);
                        }
                        else
                        {
                            mainStackPanel.Children.Add(scheduleTableGrid);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in EnsureScheduleGridExists: {ex.Message}");
            }
        }

        private T FindVisualChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T frameworkElement && (string.IsNullOrEmpty(childName) || frameworkElement.Name == childName))
                {
                    return frameworkElement;
                }
                var foundChild = FindVisualChild<T>(child, childName);
                if (foundChild != null) return foundChild;
            }
            return null;
        }

        private void SetSearchPlaceholder()
        {
            if (txtSearch != null && string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                txtSearch.Text = "Поиск по предмету, учителю, классу...";
                txtSearch.Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153));
            }
        }

        private void RemoveSearchPlaceholder()
        {
            if (txtSearch != null && txtSearch.Text == "Поиск по предмету, учителю, классу...")
            {
                txtSearch.Text = "";
                txtSearch.Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51));
            }
        }

        private void SetupRoleBasedUI()
        {
            try
            {
                if (currentUser.Role == UserRole.Admin)
                {
                    if (uvrPanel != null) uvrPanel.Visibility = Visibility.Visible;
                    if (teacherPanel != null) teacherPanel.Visibility = Visibility.Collapsed;
                    if (txtRoleIndicator != null) txtRoleIndicator.Text = "УВР - полный доступ";
                    if (statusBadge != null) statusBadge.Background = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    if (txtModeInfo != null)
                    {
                        txtModeInfo.Text = "Режим: редактирование (УВР)";
                        txtModeInfo.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                    }

                    if (btnEditSchedule != null) btnEditSchedule.Visibility = Visibility.Visible;
                    if (btnApproveSchedule != null) btnApproveSchedule.Visibility = Visibility.Visible;
                    if (btnGenerateSchedule != null) btnGenerateSchedule.Visibility = Visibility.Visible;
                    if (btnNewSchedule != null) btnNewSchedule.Visibility = Visibility.Visible;
                    if (btnDeleteSchedule != null) btnDeleteSchedule.Visibility = Visibility.Visible;
                }
                else
                {
                    if (uvrPanel != null) uvrPanel.Visibility = Visibility.Collapsed;
                    if (teacherPanel != null) teacherPanel.Visibility = Visibility.Visible;
                    if (txtRoleIndicator != null) txtRoleIndicator.Text = "Учитель - только свои уроки";
                    if (statusBadge != null) statusBadge.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    if (txtModeInfo != null)
                    {
                        txtModeInfo.Text = "Режим: просмотр только своих уроков";
                        txtModeInfo.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                        txtModeInfo.FontWeight = FontWeights.Normal;
                    }

                    if (btnEditSchedule != null) btnEditSchedule.Visibility = Visibility.Collapsed;
                    if (btnApproveSchedule != null) btnApproveSchedule.Visibility = Visibility.Collapsed;
                    if (btnGenerateSchedule != null) btnGenerateSchedule.Visibility = Visibility.Collapsed;
                    if (btnNewSchedule != null) btnNewSchedule.Visibility = Visibility.Collapsed;
                    if (btnDeleteSchedule != null) btnDeleteSchedule.Visibility = Visibility.Collapsed;

                    AddTeacherFilterCheckbox();
                }

                UpdateFontButtons();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка SetupRoleBasedUI: {ex.Message}");
            }
        }

        private void AddTeacherFilterCheckbox()
        {
            try
            {
                if (teacherPanel != null)
                {
                    var chkShowAllLessons = new CheckBox
                    {
                        Content = "Показывать все уроки",
                        IsChecked = false,
                        Margin = new Thickness(0, 10, 0, 10),
                        Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                        FontWeight = FontWeights.SemiBold
                    };
                    chkShowAllLessons.Checked += (s, e) =>
                    {
                        teacherFilterEnabled = false;
                        ApplyFilters();
                    };
                    chkShowAllLessons.Unchecked += (s, e) =>
                    {
                        teacherFilterEnabled = true;
                        ApplyFilters();
                    };

                    teacherPanel.Children.Insert(0, chkShowAllLessons);

                    var infoText = new TextBlock
                    {
                        Text = "По умолчанию показываются только уроки, где вы - учитель",
                        Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    teacherPanel.Children.Insert(1, infoText);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка AddTeacherFilterCheckbox: {ex.Message}");
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!isLoaded)
            {
                LoadTeachersDirectly();
                LoadSchedules();
                LoadClasses();

                if (currentSchedule != null)
                {
                    UpdateTeacherFilter();
                }

                isLoaded = true;
            }
        }

        private void LoadTeachersDirectly()
        {
            try
            {
                if (cmbTeacherFilter == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: cmbTeacherFilter is null!");
                    return;
                }

                var teachers = dbHelper.GetAllTeachers();
                System.Diagnostics.Debug.WriteLine($"LoadTeachersDirectly: найдено {teachers?.Count ?? 0} учителей");

                var teacherNames = new List<string> { "Все учителя" };

                if (teachers != null && teachers.Any())
                {
                    foreach (var t in teachers.OrderBy(t => t.FullName))
                    {
                        teacherNames.Add(t.FullName);
                        System.Diagnostics.Debug.WriteLine($"  - {t.FullName}");
                    }
                }

                cmbTeacherFilter.ItemsSource = teacherNames;
                cmbTeacherFilter.SelectedIndex = 0;
                cmbTeacherFilter.Items.Refresh();

                System.Diagnostics.Debug.WriteLine($"Загружено {teacherNames.Count} учителей");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadTeachersDirectly: {ex.Message}");
                if (cmbTeacherFilter != null)
                {
                    cmbTeacherFilter.ItemsSource = new List<string> { "Все учителя" };
                    cmbTeacherFilter.SelectedIndex = 0;
                }
            }
        }

        private void LoadSchedules()
        {
            try
            {
                allSchedules = dbHelper.GetAllSchedules() ?? new List<Schedule>();

                if (allSchedules.Any())
                {
                    if (cmbSchedule != null)
                    {
                        cmbSchedule.ItemsSource = allSchedules.Select(s =>
                            $"{s.Name} ({s.StartDate:dd.MM.yyyy} - {s.EndDate:dd.MM.yyyy})").ToList();
                        cmbSchedule.SelectedIndex = 0;
                    }
                    currentSchedule = allSchedules[0];
                    DisplaySchedule(currentSchedule);
                    if (txtNoSchedule != null) txtNoSchedule.Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (txtScheduleTitle != null) txtScheduleTitle.Text = "Расписание";
                    if (txtNoSchedule != null) txtNoSchedule.Visibility = Visibility.Visible;
                    if (scheduleTableGrid != null)
                    {
                        scheduleTableGrid.Children.Clear();
                        scheduleTableGrid.RowDefinitions.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadSchedules: {ex.Message}");
                if (txtScheduleTitle != null) txtScheduleTitle.Text = "Расписание";
                if (txtNoSchedule != null) txtNoSchedule.Visibility = Visibility.Visible;
            }
        }

        private void LoadClasses()
        {
            try
            {
                var classes = dbHelper.GetAllClasses() ?? new List<SchoolClass>();
                var classNames = classes.Select(c => c.Name).ToList();
                classNames.Insert(0, "Все классы");
                if (cmbClassFilter != null)
                {
                    cmbClassFilter.ItemsSource = classNames;
                    cmbClassFilter.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadClasses: {ex.Message}");
                if (cmbClassFilter != null)
                {
                    cmbClassFilter.ItemsSource = new List<string> { "Все классы" };
                    cmbClassFilter.SelectedIndex = 0;
                }
            }
        }

        private void UpdateTeacherFilter()
        {
            try
            {
                if (cmbTeacherFilter == null) return;

                var allTeachers = dbHelper.GetAllTeachers();
                var teacherNames = new List<string> { "Все учителя" };

                if (allTeachers != null && allTeachers.Any())
                {
                    foreach (var t in allTeachers.OrderBy(t => t.FullName))
                    {
                        teacherNames.Add(t.FullName);
                    }
                }

                string currentSelection = cmbTeacherFilter.SelectedItem?.ToString();

                cmbTeacherFilter.ItemsSource = teacherNames;
                cmbTeacherFilter.Items.Refresh();

                if (!string.IsNullOrEmpty(currentSelection) && teacherNames.Contains(currentSelection))
                {
                    cmbTeacherFilter.SelectedItem = currentSelection;
                }
                else
                {
                    cmbTeacherFilter.SelectedIndex = 0;
                }

                System.Diagnostics.Debug.WriteLine($"UpdateTeacherFilter: загружено {teacherNames.Count} учителей");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка UpdateTeacherFilter: {ex.Message}");
            }
        }
        private void DisplaySchedule(Schedule schedule)
        {
            try
            {
                EnsureScheduleGridExists();

                currentSchedule = schedule;

                LoadTeachersDirectly();
                UpdateTeacherFilter();

                if (currentUser.Role == UserRole.Teacher && teacherFilterEnabled)
                {
                    int originalCount = schedule.Lessons?.Count ?? 0;
                    var filteredLessons = schedule.Lessons?.Where(l => l.Teacher == currentUser.FullName).ToList() ?? new List<ScheduleLesson>();

                    var displaySchedule = new Schedule
                    {
                        Id = schedule.Id,
                        Name = schedule.Name,
                        StartDate = schedule.StartDate,
                        EndDate = schedule.EndDate,
                        Status = schedule.Status,
                        CreatedAt = schedule.CreatedAt,
                        IsActive = schedule.IsActive,
                        CreatedBy = schedule.CreatedBy,
                        ApprovedBy = schedule.ApprovedBy,
                        ApprovedDate = schedule.ApprovedDate,
                        Lessons = filteredLessons
                    };

                    if (txtScheduleTitle != null)
                        txtScheduleTitle.Text = $"{schedule.Name} (только ваши уроки: {filteredLessons.Count} из {originalCount})";
                    RenderScheduleTable(displaySchedule);
                }
                else
                {
                    if (txtScheduleTitle != null)
                        txtScheduleTitle.Text = $"{schedule.Name}";
                    RenderScheduleTable(schedule);
                }

                UpdateStatusDisplay(schedule.Status);

                if (currentUser.Role == UserRole.Admin)
                {
                    UpdateUvrButtonsByStatus(schedule.Status);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка DisplaySchedule: {ex.Message}");
            }
        }
/// <summary>
/// Обновляет отображение статуса расписания
/// </summary>
private void UpdateStatusDisplay(ScheduleStatus status)
{
    try
    {
        // Обновляем текст статуса
        if (txtScheduleStatus != null)
        {
            switch (status)
            {
                case ScheduleStatus.Draft:
                    txtScheduleStatus.Text = "📄 Черновик";
                    txtScheduleStatus.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                    break;
                case ScheduleStatus.Pending:
                    txtScheduleStatus.Text = "⏳ На проверке";
                    txtScheduleStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    break;
                case ScheduleStatus.Approved:
                    txtScheduleStatus.Text = "✅ Утверждено";
                    txtScheduleStatus.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    break;
                case ScheduleStatus.RequiresCorrection:
                    txtScheduleStatus.Text = "⚠️ Требует корректировки";
                    txtScheduleStatus.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    break;
                default:
                    txtScheduleStatus.Text = status.ToString();
                    txtScheduleStatus.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                    break;
            }
        }

        // Обновляем бейдж статуса
        if (statusBadge != null)
        {
            switch (status)
            {
                case ScheduleStatus.Draft:
                    statusBadge.Background = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                    break;
                case ScheduleStatus.Pending:
                    statusBadge.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    break;
                case ScheduleStatus.Approved:
                    statusBadge.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    break;
                case ScheduleStatus.RequiresCorrection:
                    statusBadge.Background = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    break;
                default:
                    statusBadge.Background = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                    break;
            }
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Ошибка UpdateStatusDisplay: {ex.Message}");
    }
}
        private void UpdateUvrButtonsByStatus(ScheduleStatus status)
        {
            if (currentUser.Role != UserRole.Admin) return;

            switch (status)
            {
                case ScheduleStatus.Approved:
                    if (btnEditSchedule != null)
                    {
                        btnEditSchedule.Content = "✏ Редактировать расписание";
                        btnEditSchedule.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                        btnEditSchedule.IsEnabled = true;
                    }
                    if (btnApproveSchedule != null)
                    {
                        btnApproveSchedule.Content = "✓ Утверждено";
                        btnApproveSchedule.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                        btnApproveSchedule.IsEnabled = false;
                    }
                    break;
                default:
                    if (btnEditSchedule != null)
                    {
                        btnEditSchedule.Content = "✏ Редактировать расписание";
                        btnEditSchedule.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                        btnEditSchedule.IsEnabled = true;
                    }
                    if (btnApproveSchedule != null)
                    {
                        btnApproveSchedule.Content = "✓ Утвердить расписание";
                        btnApproveSchedule.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                        btnApproveSchedule.IsEnabled = true;
                    }
                    break;
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private void RenderScheduleTable(Schedule schedule)
        {
            try
            {
                EnsureScheduleGridExists();

                if (scheduleTableGrid == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: scheduleTableGrid is still null in RenderScheduleTable!");
                    return;
                }

                scheduleTableGrid.Children.Clear();
                scheduleTableGrid.RowDefinitions.Clear();
                scheduleTableGrid.ColumnDefinitions.Clear();

                scheduleTableGrid.ShowGridLines = false;
                scheduleTableGrid.Background = Brushes.White;

                string[] allDays = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница" };

                string selectedDay = "Все дни";
                if (cmbDayFilter?.SelectedItem != null)
                {
                    if (cmbDayFilter.SelectedItem is ComboBoxItem item)
                        selectedDay = item.Content.ToString();
                    else
                        selectedDay = cmbDayFilter.SelectedItem.ToString();
                }

                string[] daysToShow;
                if (selectedDay != "Все дни")
                    daysToShow = new string[] { selectedDay };
                else
                    daysToShow = allDays;

                // ✅ ПОЛУЧАЕМ ВСЕ КЛАССЫ ИЗ СИСТЕМЫ, А НЕ ТОЛЬКО ТЕ, У КОТОРЫХ ЕСТЬ УРОКИ
                var allClasses = dbHelper.GetAllClasses() ?? new List<SchoolClass>();
                var classNames = allClasses.Select(c => c.Name).OrderBy(c => c).ToList();

                string selectedClassFilter = cmbClassFilter?.SelectedItem?.ToString() ?? "Все классы";
                if (selectedClassFilter != "Все классы")
                {
                    classNames = classNames.Where(c => c == selectedClassFilter).ToList();
                }

                string selectedTeacher = cmbTeacherFilter?.SelectedItem?.ToString() ?? "Все учителя";

                // Фильтруем уроки
                var filteredLessons = schedule.Lessons ?? new List<ScheduleLesson>();
                if (selectedTeacher != "Все учителя")
                {
                    filteredLessons = filteredLessons.Where(l => l.Teacher == selectedTeacher).ToList();
                }
                if (selectedClassFilter != "Все классы")
                {
                    filteredLessons = filteredLessons.Where(l => l.Class == selectedClassFilter).ToList();
                }

                // Если нет классов - показываем сообщение
                if (!classNames.Any())
                {
                    TextBlock noClassesText = new TextBlock
                    {
                        Text = "Нет классов в системе. Создайте классы в панели управления.",
                        FontSize = 16,
                        Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 50, 0, 0)
                    };
                    scheduleTableGrid.Children.Add(noClassesText);
                    return;
                }

                // Колонки: Время + дни недели
                scheduleTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compactMode ? 70 : 90) });
                for (int i = 0; i < daysToShow.Length; i++)
                {
                    scheduleTableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                }

                // Заголовки
                AddScheduleHeaders(daysToShow);

                int rowIndex = scheduleTableGrid.RowDefinitions.Count;

                // ✅ ПРОХОДИМ ПО ВСЕМ КЛАССАМ, А НЕ ТОЛЬКО ПО ТЕМ, У КОТОРЫХ ЕСТЬ УРОКИ
                foreach (var className in classNames)
                {
                    var classLessons = filteredLessons.Where(l => l.Class == className).ToList();

                    // Строка с названием класса
                    AddClassHeaderRow(className, rowIndex, daysToShow);
                    rowIndex++;

                    // Определяем максимальный номер урока (минимум 8 уроков)
                    int maxLessonNumber = classLessons.Any() ? classLessons.Max(l => l.LessonNumber) : 8;

                    for (int lessonNum = 1; lessonNum <= maxLessonNumber; lessonNum++)
                    {
                        var lessonsByDay = classLessons
                            .Where(l => l.LessonNumber == lessonNum)
                            .ToDictionary(l => l.DayOfWeek, l => l);

                        AddLessonRow(lessonNum, lessonsByDay, daysToShow, className, rowIndex);
                        rowIndex++;
                    }

                    // Добавляем разделитель между классами
                    if (rowIndex < scheduleTableGrid.RowDefinitions.Count)
                    {
                        var separatorRow = new RowDefinition { Height = new GridLength(6) };
                        scheduleTableGrid.RowDefinitions.Insert(rowIndex, separatorRow);
                        rowIndex++;
                    }
                }

                if (searchResults.Any())
                {
                    HighlightSearchResults();
                }

                if (compactMode)
                {
                    ApplyCompactMode();
                }
                else
                {
                    ApplyNormalMode();
                }

                UpdateFontSize();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка RenderScheduleTable: {ex.Message}");
            }
        }

        private void AddScheduleHeaders(string[] daysToShow)
        {
            if (scheduleTableGrid == null) return;

            scheduleTableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border timeHeader = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                CornerRadius = new CornerRadius(5, 0, 0, 0),
                Padding = new Thickness(compactMode ? 3 : 5, compactMode ? 5 : 8, compactMode ? 3 : 5, compactMode ? 5 : 8),
                Child = new TextBlock
                {
                    Text = "Время",
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 12
                }
            };
            Grid.SetRow(timeHeader, 0);
            Grid.SetColumn(timeHeader, 0);
            scheduleTableGrid.Children.Add(timeHeader);

            for (int i = 0; i < daysToShow.Length; i++)
            {
                Border dayHeader = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    Padding = new Thickness(compactMode ? 3 : 5, compactMode ? 5 : 8, compactMode ? 3 : 5, compactMode ? 5 : 8),
                    Child = new TextBlock
                    {
                        Text = daysToShow[i],
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        FontSize = 12
                    }
                };
                if (i == daysToShow.Length - 1)
                    dayHeader.CornerRadius = new CornerRadius(0, 5, 0, 0);

                Grid.SetRow(dayHeader, 0);
                Grid.SetColumn(dayHeader, i + 1);
                scheduleTableGrid.Children.Add(dayHeader);
            }
        }

        private void AddClassHeaderRow(string className, int rowIndex, string[] daysToShow)
        {
            if (scheduleTableGrid == null) return;

            scheduleTableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border headerCell = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                Padding = new Thickness(compactMode ? 5 : 10, compactMode ? 3 : 5, compactMode ? 5 : 10, compactMode ? 3 : 5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(187, 222, 251)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            Expander expander = new Expander
            {
                Header = $"📚 {className}",
                IsExpanded = true,
                Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                FontWeight = FontWeights.SemiBold,
                FontSize = compactMode ? 12 : 14
            };

            expander.Content = new Border { Height = 0 };
            headerCell.Child = expander;

            Grid.SetRow(headerCell, rowIndex);
            Grid.SetColumn(headerCell, 0);
            Grid.SetColumnSpan(headerCell, daysToShow.Length + 1);
            scheduleTableGrid.Children.Add(headerCell);
        }

        private void AddLessonRow(int lessonNumber, Dictionary<string, ScheduleLesson> dayLessons,
         string[] daysToShow, string className, int rowIndex)
        {
            if (scheduleTableGrid == null) return;

            scheduleTableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Ячейка времени
            string timeText = $"{GetLessonTime(lessonNumber)}\n{GetLessonEndTime(lessonNumber)}";
            Border timeCell = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 249, 250)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(compactMode ? 3 : 6, compactMode ? 2 : 4, compactMode ? 3 : 6, compactMode ? 2 : 4),
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
            {
                new TextBlock
                {
                    Text = timeText.Split('\n')[0],
                    FontWeight = FontWeights.Bold,
                    FontSize = compactMode ? 10 : 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    HorizontalAlignment = HorizontalAlignment.Center
                },
                new TextBlock
                {
                    Text = timeText.Split('\n')[1],
                    FontSize = compactMode ? 8 : 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
                }
            };
            Grid.SetRow(timeCell, rowIndex);
            Grid.SetColumn(timeCell, 0);
            scheduleTableGrid.Children.Add(timeCell);

            for (int i = 0; i < daysToShow.Length; i++)
            {
                string currentDay = daysToShow[i];
                ScheduleLesson lesson = null;
                if (dayLessons != null && dayLessons.ContainsKey(currentDay))
                {
                    lesson = dayLessons[currentDay];
                }

                Border lessonCell = CreateLessonCell(lesson, lessonNumber, currentDay, className);
                Grid.SetRow(lessonCell, rowIndex);
                Grid.SetColumn(lessonCell, i + 1);
                scheduleTableGrid.Children.Add(lessonCell);
            }
        }

        // ============================================================
        // СОЗДАНИЕ ЯЧЕЙКИ УРОКА (С КАБИНЕТОМ ИЗ БД)
        // ============================================================

        private Border CreateLessonCell(ScheduleLesson lesson, int lessonNumber, string dayOfWeek, string className)
        {
            Border cell = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(compactMode ? 4 : 8, compactMode ? 2 : 4, compactMode ? 4 : 8, compactMode ? 2 : 4),
                Tag = lesson,
                CornerRadius = new CornerRadius(0)
            };

            if (lesson != null)
            {
                // Определяем цвет фона
                SolidColorBrush backgroundColor;
                if (lesson.IsCanceled)
                    backgroundColor = new SolidColorBrush(Color.FromRgb(255, 235, 238));
                else if (!string.IsNullOrEmpty(lesson.LessonPlanTitle))
                    backgroundColor = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                else if (!string.IsNullOrEmpty(lesson.Note))
                    backgroundColor = new SolidColorBrush(Color.FromRgb(255, 243, 224));
                else
                    backgroundColor = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                cell.Background = backgroundColor;

                // Левая полоса-индикатор
                Border indicator = new Border
                {
                    Width = 4,
                    Background = GetSubjectColor(lesson.Subject),
                    CornerRadius = new CornerRadius(0, 2, 2, 0),
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 0)
                };

                Grid mainGrid = new Grid();
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                mainGrid.Children.Add(indicator);
                Grid.SetColumn(indicator, 0);

                StackPanel content = new StackPanel { Margin = new Thickness(8, 2, 4, 2) };
                Grid.SetColumn(content, 1);

                // Предмет
                content.Children.Add(new TextBlock
                {
                    Text = lesson.Subject,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = compactMode ? 11 : 14,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(33, 33, 33))
                });

                // Время
                string startTime = GetLessonTime(lessonNumber);
                string endTime = GetLessonEndTime(lessonNumber);
                content.Children.Add(new TextBlock
                {
                    Text = $"⏰ {startTime} - {endTime}",
                    FontSize = compactMode ? 8 : 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    TextWrapping = TextWrapping.Wrap
                });

                // Учитель (только для УВР)
                if (currentUser.Role == UserRole.Admin)
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = $"👨‍🏫 {lesson.Teacher}",
                        FontSize = compactMode ? 8 : 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                        TextWrapping = TextWrapping.Wrap
                    });
                }

                // Кабинет
                if (!string.IsNullOrEmpty(lesson.Room))
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = $"🚪 {lesson.Room}",
                        FontSize = compactMode ? 8 : 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                        TextWrapping = TextWrapping.Wrap
                    });
                }

                // Класс (показываем, если фильтр "Все классы")
                if (selectedClass == "Все классы" || string.IsNullOrEmpty(selectedClass))
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = $"📚 {lesson.Class}",
                        FontSize = compactMode ? 8 : 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                        TextWrapping = TextWrapping.Wrap
                    });
                }

                // План урока
                if (showLessonPlans && !string.IsNullOrEmpty(lesson.LessonPlanTitle))
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = $"📘 {lesson.LessonPlanTitle}",
                        FontSize = compactMode ? 8 : 9,
                        FontStyle = FontStyles.Italic,
                        Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                        TextWrapping = TextWrapping.Wrap,
                        MaxHeight = 20,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                }

                if (lesson.IsCanceled)
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = "❌ ОТМЕНЕН",
                        FontSize = compactMode ? 8 : 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                        TextWrapping = TextWrapping.Wrap
                    });
                }

                mainGrid.Children.Add(content);
                cell.Child = mainGrid;
                cell.Cursor = Cursors.Hand;

                // События
                cell.MouseLeftButtonUp += (cellSender, cellArgs) => LessonCell_Click(cellSender, cellArgs, lesson);
                if (currentUser.Role == UserRole.Admin && isEditMode)
                {
                    cell.Cursor = Cursors.Pen;
                    cell.MouseLeftButtonUp += (cellSender, cellArgs) => EditLesson_Click(cellSender, cellArgs, lesson);
                }
            }
            else
            {
                if (currentUser.Role == UserRole.Admin && isEditMode)
                {
                    cell.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                    cell.Cursor = Cursors.Hand;
                    cell.MouseLeftButtonUp += (cellSender, cellArgs) =>
                        AddLesson_Click(cellSender, cellArgs, lessonNumber, dayOfWeek);
                    cell.Child = new TextBlock
                    {
                        Text = "+",
                        Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = compactMode ? 18 : 24,
                        FontWeight = FontWeights.Light
                    };
                }
                else
                {
                    cell.Background = new SolidColorBrush(Color.FromRgb(250, 250, 250));
                    cell.Child = new TextBlock
                    {
                        Text = "—",
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = compactMode ? 12 : 16
                    };
                }
            }

            return cell;
        }
        private SolidColorBrush GetSubjectColor(string subject)
        {
            // Цвета для разных предметов
            var colors = new Dictionary<string, string>
    {
        { "Русский язык", "#1976D2" },
        { "Литература", "#7B1FA2" },
        { "Математика", "#D32F2F" },
        { "Алгебра", "#D32F2F" },
        { "Геометрия", "#D32F2F" },
        { "Иностранный язык", "#2E7D32" },
        { "История", "#F57C00" },
        { "Обществознание", "#F57C00" },
        { "География", "#00897B" },
        { "Физика", "#0288D1" },
        { "Химия", "#8D6E63" },
        { "Биология", "#388E3C" },
        { "Информатика", "#00838F" },
        { "Труд", "#6D4C41" },
        { "Адаптивная физическая культура", "#F4511E" },
        { "Основы безопасности", "#FF6F00" }
    };

            if (!string.IsNullOrEmpty(subject) && colors.ContainsKey(subject))
            {
                var color = ColorConverter.ConvertFromString(colors[subject]);
                if (color is Color c)
                    return new SolidColorBrush(c);
            }
            return new SolidColorBrush(Color.FromRgb(158, 158, 158));
        }

        // Исправленные методы для 40-минутных уроков
        private string GetLessonTime(int lessonNumber)
        {
            switch (lessonNumber)
            {
                case 1: return "08:30";
                case 2: return "09:20";
                case 3: return "10:10";
                case 4: return "11:00";
                case 5: return "11:50";
                case 6: return "12:40";
                case 7: return "13:30";
                case 8: return "14:20";
                case 9: return "15:10";
                default: return "";
            }
        }

        private string GetLessonEndTime(int lessonNumber)
        {
            switch (lessonNumber)
            {
                case 1: return "09:10";
                case 2: return "10:00";
                case 3: return "10:50";
                case 4: return "11:40";
                case 5: return "12:30";
                case 6: return "13:20";
                case 7: return "14:10";
                case 8: return "15:00";
                case 9: return "15:50";
                default: return "";
            }
        }
        private void LessonCell_Click(object sender, RoutedEventArgs e, ScheduleLesson lesson)
        {
            try
            {
                selectedLesson = lesson;

                if (currentUser.Role == UserRole.Teacher && lesson != null)
                {
                    if (lesson.Teacher == currentUser.FullName) // ✅ Исправлено: currentUser.Name → currentUser.FullName
                    {
                        if (txtNote != null)
                        {
                            txtNote.IsEnabled = true;
                            txtNote.Text = lesson.Note ?? "";
                            txtNote.Focus();
                        }
                        if (btnSaveNote != null) btnSaveNote.IsEnabled = true;

                        MessageBox.Show($"Выбран урок: {lesson.Subject}\nКласс: {lesson.Class}\nКабинет: {lesson.Room ?? "не указан"}",
                            "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Это урок другого учителя", "Информация",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else if (currentUser.Role == UserRole.Admin && lesson != null)
                {
                    string info = $"Урок: {lesson.Subject}\nКласс: {lesson.Class}\nУчитель: {lesson.Teacher}\nКабинет: {lesson.Room ?? "не указан"}";
                    if (!string.IsNullOrEmpty(lesson.Homework))
                        info += $"\nДЗ: {lesson.Homework}";
                    if (!string.IsNullOrEmpty(lesson.Note))
                        info += $"\nЗаметка: {lesson.Note}";
                    MessageBox.Show(info, "Информация об уроке", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LessonCell_Click: {ex.Message}");
            }
        }

     private void EditLesson_Click(object sender, RoutedEventArgs e, ScheduleLesson lesson)
{
    if (currentUser.Role != UserRole.Admin) return;

    var dialog = new Window
    {
        Title = "Редактирование урока",
        Width = 450,
        Height = 600,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Owner = Window.GetWindow(this),
        Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
        ResizeMode = ResizeMode.NoResize
    };

    var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    StackPanel panel = new StackPanel { Margin = new Thickness(20) };

    // Заголовок
    panel.Children.Add(new TextBlock
    {
        Text = $"{lesson.DayOfWeek}, {lesson.LessonNumber} урок",
        FontSize = 16,
        FontWeight = FontWeights.Bold,
        Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
        Margin = new Thickness(0, 0, 0, 15)
    });

    // ============================================================
    // КЛАСС (ComboBox)
    // ============================================================
    panel.Children.Add(new TextBlock
    {
        Text = "📚 Класс:",
        Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
        Margin = new Thickness(0, 0, 0, 5),
        FontWeight = FontWeights.SemiBold
    });

    var classItems = dbHelper.GetAllClasses()?.Select(c => c.Name).ToList() ?? new List<string>();
    var cmbClass = new ComboBox
    {
        Margin = new Thickness(0, 0, 0, 15),
        Padding = new Thickness(10),
        ItemsSource = classItems,
        Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
        SelectedItem = lesson.Class
    };
    if (cmbClass.SelectedItem == null && classItems.Any())
        cmbClass.SelectedIndex = 0;
    panel.Children.Add(cmbClass);

    // ============================================================
    // ПРЕДМЕТ (ComboBox)
    // ============================================================
    panel.Children.Add(new TextBlock
    {
        Text = "📖 Предмет:",
        Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
        Margin = new Thickness(0, 0, 0, 5),
        FontWeight = FontWeights.SemiBold
    });

    var subjectItems = dbHelper.GetAllSubjects()?.Select(s => s.Name).ToList() ?? new List<string>();
    var cmbSubject = new ComboBox
    {
        Margin = new Thickness(0, 0, 0, 15),
        Padding = new Thickness(10),
        ItemsSource = subjectItems,
        Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
        SelectedItem = lesson.Subject
    };
    if (cmbSubject.SelectedItem == null && subjectItems.Any())
        cmbSubject.SelectedIndex = 0;
    panel.Children.Add(cmbSubject);

    // ============================================================
    // УЧИТЕЛЬ (ComboBox)
    // ============================================================
    panel.Children.Add(new TextBlock
    {
        Text = "👨‍🏫 Учитель:",
        Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
        Margin = new Thickness(0, 0, 0, 5),
        FontWeight = FontWeights.SemiBold
    });

    var teacherItems = dbHelper.GetAllTeachers()?.Select(t => t.FullName).ToList() ?? new List<string>();
    var cmbTeacher = new ComboBox
    {
        Margin = new Thickness(0, 0, 0, 15),
        Padding = new Thickness(10),
        ItemsSource = teacherItems,
        Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
        SelectedItem = lesson.Teacher
    };
    if (cmbTeacher.SelectedItem == null && teacherItems.Any())
        cmbTeacher.SelectedIndex = 0;
    panel.Children.Add(cmbTeacher);

    // ============================================================
    // КАБИНЕТ (TextBox + кнопка для автозаполнения)
    // ============================================================
    panel.Children.Add(new TextBlock
    {
        Text = "🚪 Кабинет:",
        Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
        Margin = new Thickness(0, 0, 0, 5),
        FontWeight = FontWeights.SemiBold
    });

    Grid roomGrid = new Grid();
    roomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
    roomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

    var txtRoom = new TextBox
    {
        Margin = new Thickness(0, 0, 0, 15),
        Padding = new Thickness(10),
        BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
        BorderThickness = new Thickness(1),
        Text = lesson.Room ?? ""
    };
    Grid.SetColumn(txtRoom, 0);
    roomGrid.Children.Add(txtRoom);

    Button btnAutoRoom = new Button
    {
        Content = "🔍",
        Style = (Style)FindResource("MainButtonStyle"),
        Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
        Width = 30,
        Height = 30,
        Margin = new Thickness(5, 0, 0, 0),
        Cursor = Cursors.Hand,
        FontSize = 14
    };
    btnAutoRoom.Click += (autoSender, autoArgs) =>
    {
        // Автозаполнение кабинета по учителю
        if (cmbTeacher.SelectedItem != null)
        {
            string teacherName = cmbTeacher.SelectedItem.ToString();
            var teacher = dbHelper.GetAllTeachers()?.FirstOrDefault(t => t.FullName == teacherName);
            if (teacher != null && !string.IsNullOrEmpty(teacher.Room))
            {
                txtRoom.Text = teacher.Room;
            }
            else
            {
                // Если у учителя нет кабинета, ищем по предмету
                var rooms = dbHelper.GetAllRooms();
                if (rooms != null)
                {
                    var room = rooms.FirstOrDefault(r => r.Subject == cmbSubject.SelectedItem?.ToString());
                    if (room != null)
                    {
                        txtRoom.Text = room.Number;
                    }
                }
            }
        }
    };
    Grid.SetColumn(btnAutoRoom, 1);
    roomGrid.Children.Add(btnAutoRoom);
    panel.Children.Add(roomGrid);

    // ============================================================
    // ДОМАШНЕЕ ЗАДАНИЕ
    // ============================================================
    panel.Children.Add(new TextBlock
    {
        Text = "📝 Домашнее задание:",
        Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
        Margin = new Thickness(0, 0, 0, 5),
        FontWeight = FontWeights.SemiBold
    });

    var txtHomework = new TextBox
    {
        Margin = new Thickness(0, 0, 0, 15),
        Padding = new Thickness(10),
        Text = lesson.Homework ?? "",
        TextWrapping = TextWrapping.Wrap,
        AcceptsReturn = true,
        Height = 60,
        BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
        BorderThickness = new Thickness(1)
    };
    panel.Children.Add(txtHomework);

    // ============================================================
    // ОТМЕНА УРОКА
    // ============================================================
    var chkCanceled = new CheckBox
    {
        Content = "❌ Урок отменен",
        IsChecked = lesson.IsCanceled,
        Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
        Margin = new Thickness(0, 0, 0, 15)
    };
    panel.Children.Add(chkCanceled);

    // ============================================================
    // ИНФОРМАЦИЯ
    // ============================================================
    Border infoBorder = new Border
    {
        Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
        CornerRadius = new CornerRadius(5),
        Padding = new Thickness(10),
        Margin = new Thickness(0, 0, 0, 15)
    };
    TextBlock infoText = new TextBlock
    {
        Text = "💡 При выборе учителя кабинет можно заполнить автоматически",
        Foreground = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
        FontSize = 11,
        TextWrapping = TextWrapping.Wrap
    };
    infoBorder.Child = infoText;
    panel.Children.Add(infoBorder);

    // ============================================================
    // КНОПКИ
    // ============================================================
    StackPanel buttonPanel = new StackPanel
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
        Height = 35,
        FontWeight = FontWeights.SemiBold,
        Cursor = Cursors.Hand
    };
    btnSave.Click += (saveSender, saveArgs) =>
    {
        try
        {
            // Проверка выбора
            if (cmbClass.SelectedItem == null)
            {
                MessageBox.Show("Выберите класс", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbSubject.SelectedItem == null)
            {
                MessageBox.Show("Выберите предмет", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbTeacher.SelectedItem == null)
            {
                MessageBox.Show("Выберите учителя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Обновляем урок
            lesson.Class = cmbClass.SelectedItem.ToString();
            lesson.Subject = cmbSubject.SelectedItem.ToString();
            lesson.Teacher = cmbTeacher.SelectedItem.ToString();
            lesson.Room = txtRoom.Text.Trim();

            // Получаем TeacherId
            var teacher = dbHelper.GetAllTeachers()?.FirstOrDefault(t => t.FullName == lesson.Teacher);
            lesson.TeacherId = teacher?.UserId ?? 1;

            lesson.Homework = txtHomework.Text;
            lesson.IsCanceled = chkCanceled.IsChecked == true;

            dbHelper.UpdateScheduleLesson(lesson);

            // Обновляем отображение
            if (currentSchedule != null)
            {
                var scheduleLesson = currentSchedule.Lessons.FirstOrDefault(l => l.Id == lesson.Id);
                if (scheduleLesson != null)
                {
                    scheduleLesson.Class = lesson.Class;
                    scheduleLesson.Subject = lesson.Subject;
                    scheduleLesson.Teacher = lesson.Teacher;
                    scheduleLesson.TeacherId = lesson.TeacherId;
                    scheduleLesson.Room = lesson.Room;
                    scheduleLesson.Homework = lesson.Homework;
                    scheduleLesson.IsCanceled = lesson.IsCanceled;
                }
            }

            RenderScheduleTable(currentSchedule);
            dialog.Close();

            MessageBox.Show("✅ Урок успешно обновлен!", "Успех",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    };
    buttonPanel.Children.Add(btnSave);

    Button btnCancel = new Button
    {
        Content = "Отмена",
        Style = (Style)FindResource("MainButtonStyle"),
        Background = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
        Width = 100,
        Height = 35,
        Cursor = Cursors.Hand
    };
    btnCancel.Click += (cancelSender, cancelArgs) => dialog.Close();
    buttonPanel.Children.Add(btnCancel);

    panel.Children.Add(buttonPanel);
    scrollViewer.Content = panel;
    dialog.Content = scrollViewer;
    dialog.ShowDialog();
}

        private void AddLesson_Click(object sender, RoutedEventArgs e, int lessonNumber, string dayOfWeek)
        {
            if (currentUser.Role != UserRole.Admin) return;

            var dialog = new Window
            {
                Title = "Добавление урока",
                Width = 450,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                ResizeMode = ResizeMode.NoResize
            };

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel panel = new StackPanel { Margin = new Thickness(20) };

            // Заголовок
            panel.Children.Add(new TextBlock
            {
                Text = $"{dayOfWeek}, {lessonNumber} урок",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Margin = new Thickness(0, 0, 0, 15)
            });

            // ============================================================
            // КЛАСС
            // ============================================================
            panel.Children.Add(new TextBlock
            {
                Text = "📚 Класс:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });

            var classItems = dbHelper.GetAllClasses()?.Select(c => c.Name).ToList() ?? new List<string>();
            var cmbClass = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                ItemsSource = classItems,
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255))
            };
            if (classItems.Any()) cmbClass.SelectedIndex = 0;
            panel.Children.Add(cmbClass);

            // ============================================================
            // ПРЕДМЕТЫ (ListBox с множественным выбором)
            // ============================================================
            panel.Children.Add(new TextBlock
            {
                Text = "📖 Предметы (можно выбрать несколько с Ctrl+клик):",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });

            var lbSubjects = new ListBox
            {
                Height = 100,
                SelectionMode = SelectionMode.Multiple,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255))
            };

            var subjectItems = dbHelper.GetAllSubjects()?.Select(s => s.Name).ToList() ?? new List<string>();
            foreach (var subject in subjectItems.OrderBy(s => s))
            {
                lbSubjects.Items.Add(subject);
            }
            if (lbSubjects.Items.Count > 0) lbSubjects.SelectedIndex = 0;
            panel.Children.Add(lbSubjects);

            // Кнопки для предметов
            StackPanel subjectButtonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15)
            };

            Button btnSelectAllSubjects = new Button
            {
                Content = "Выбрать все",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Width = 100,
                Height = 28,
                FontSize = 11,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            btnSelectAllSubjects.Click += (s, args) => lbSubjects.SelectAll();
            subjectButtonPanel.Children.Add(btnSelectAllSubjects);

            Button btnClearSubjects = new Button
            {
                Content = "Очистить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                Width = 80,
                Height = 28,
                FontSize = 11,
                Cursor = Cursors.Hand
            };
            btnClearSubjects.Click += (s, args) => lbSubjects.SelectedItems.Clear();
            subjectButtonPanel.Children.Add(btnClearSubjects);

            panel.Children.Add(subjectButtonPanel);

            // ============================================================
            // УЧИТЕЛЯ (ListBox с множественным выбором)
            // ============================================================
            panel.Children.Add(new TextBlock
            {
                Text = "👨‍🏫 Учителя (можно выбрать несколько с Ctrl+клик):",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });

            var lbTeachers = new ListBox
            {
                Height = 100,
                SelectionMode = SelectionMode.Multiple,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255))
            };

            var teacherItems = dbHelper.GetAllTeachers()?.Select(t => t.FullName).ToList() ?? new List<string>();
            foreach (var teacher in teacherItems.OrderBy(t => t))
            {
                lbTeachers.Items.Add(teacher);
            }
            if (lbTeachers.Items.Count > 0) lbTeachers.SelectedIndex = 0;
            panel.Children.Add(lbTeachers);

            // Кнопки для учителей
            StackPanel teacherButtonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15)
            };

            Button btnSelectAllTeachers = new Button
            {
                Content = "Выбрать всех",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Width = 100,
                Height = 28,
                FontSize = 11,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            btnSelectAllTeachers.Click += (s, args) => lbTeachers.SelectAll();
            teacherButtonPanel.Children.Add(btnSelectAllTeachers);

            Button btnClearTeachers = new Button
            {
                Content = "Очистить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                Width = 80,
                Height = 28,
                FontSize = 11,
                Cursor = Cursors.Hand
            };
            btnClearTeachers.Click += (s, args) => lbTeachers.SelectedItems.Clear();
            teacherButtonPanel.Children.Add(btnClearTeachers);

            panel.Children.Add(teacherButtonPanel);

            // ============================================================
            // КАБИНЕТ
            // ============================================================
            panel.Children.Add(new TextBlock
            {
                Text = "🚪 Кабинет:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });

            var txtRoom = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1)
            };
            panel.Children.Add(txtRoom);

            // Информация о выбранном
            TextBlock infoText = new TextBlock
            {
                Text = "💡 Выберите предметы и учителей (можно несколько)",
                Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(infoText);

            // ============================================================
            // КНОПКИ
            // ============================================================
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button btnAdd = new Button
            {
                Content = "➕ Добавить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Margin = new Thickness(0, 0, 10, 0),
                Width = 120,
                Height = 35,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            btnAdd.Click += (addSender, addArgs) =>
            {
                // Проверка выбора
                if (cmbClass.SelectedItem == null)
                {
                    MessageBox.Show("Выберите класс", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (lbSubjects.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Выберите хотя бы один предмет", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (lbTeachers.SelectedItems.Count == 0)
                {
                    MessageBox.Show("Выберите хотя бы одного учителя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string className = cmbClass.SelectedItem.ToString();
                string room = txtRoom.Text.Trim();

                // Получаем выбранные предметы и учителей
                var selectedSubjects = lbSubjects.SelectedItems.Cast<string>().ToList();
                var selectedTeachers = lbTeachers.SelectedItems.Cast<string>().ToList();

                try
                {
                    int addedCount = 0;

                    // Создаем урок для каждой комбинации предмет-учитель
                    foreach (var subject in selectedSubjects)
                    {
                        foreach (var teacher in selectedTeachers)
                        {
                            var newLesson = new ScheduleLesson
                            {
                                ScheduleId = currentSchedule.Id,
                                Class = className,
                                Subject = subject,
                                Teacher = teacher,
                                TeacherId = 1,
                                DayOfWeek = dayOfWeek,
                                LessonNumber = lessonNumber,
                                StartTime = TimeSpan.Parse(GetLessonTime(lessonNumber)),
                                EndTime = TimeSpan.Parse(GetLessonEndTime(lessonNumber)),
                                Room = room,
                                Homework = "",
                                Note = "",
                                IsCanceled = false
                            };
                            dbHelper.AddScheduleLesson(newLesson);
                            currentSchedule.Lessons.Add(newLesson);
                            addedCount++;
                        }
                    }

                    MessageBox.Show($"✅ Добавлено {addedCount} уроков\n" +
                        $"Класс: {className}\n" +
                        $"Предметы: {string.Join(", ", selectedSubjects)}\n" +
                        $"Учителя: {string.Join(", ", selectedTeachers)}",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    dialog.Close();
                    RenderScheduleTable(currentSchedule);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка добавления урока: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            buttonPanel.Children.Add(btnAdd);

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                Width = 100,
                Height = 35,
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (cancelSender, cancelArgs) => dialog.Close();
            buttonPanel.Children.Add(btnCancel);

            panel.Children.Add(buttonPanel);
            scrollViewer.Content = panel;
            dialog.Content = scrollViewer;
            dialog.ShowDialog();
        }

        private void ApplyFilters()
        {
            if (currentSchedule != null)
            {
                RenderScheduleTable(currentSchedule);
            }
        }

        private void cmbSchedule_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (cmbSchedule.SelectedIndex >= 0 && allSchedules != null && allSchedules.Any())
                {
                    currentSchedule = allSchedules[cmbSchedule.SelectedIndex];
                    DisplaySchedule(currentSchedule);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка cmbSchedule_SelectionChanged: {ex.Message}");
            }
        }

        private void cmbClassFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void cmbTeacherFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"cmbTeacherFilter_SelectionChanged: выбран {cmbTeacherFilter?.SelectedItem}");
                ApplyFilters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка cmbTeacherFilter_SelectionChanged: {ex.Message}");
            }
        }

        private void cmbDayFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void btnResetFilters_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cmbClassFilter != null && cmbClassFilter.Items.Count > 0)
                    cmbClassFilter.SelectedIndex = 0;
                if (cmbTeacherFilter != null && cmbTeacherFilter.Items.Count > 0)
                    cmbTeacherFilter.SelectedIndex = 0;
                if (cmbDayFilter != null && cmbDayFilter.Items.Count > 0)
                    cmbDayFilter.SelectedIndex = 0;

                ApplyFilters();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка btnResetFilters_Click: {ex.Message}");
            }
        }

        // ============================================================
        // УПРАВЛЕНИЕ РАЗМЕРОМ ШРИФТА
        // ============================================================

        private void btnFontSmall_Click(object sender, RoutedEventArgs e)
        {
            fontSizeLevel = 0;
            UpdateFontSize();
            UpdateFontButtons();
        }

        private void btnFontMedium_Click(object sender, RoutedEventArgs e)
        {
            fontSizeLevel = 1;
            UpdateFontSize();
            UpdateFontButtons();
        }

        private void btnFontLarge_Click(object sender, RoutedEventArgs e)
        {
            fontSizeLevel = 3;
            UpdateFontSize();
            UpdateFontButtons();
        }

        private void btnFontXLarge_Click(object sender, RoutedEventArgs e)
        {
            fontSizeLevel = 4;
            UpdateFontSize();
            UpdateFontButtons();
        }

        private void UpdateFontSize()
        {
            if (scheduleTableGrid == null) return;

            double baseSize = 13;
            switch (fontSizeLevel)
            {
                case 0: baseSize = 9; break;
                case 1: baseSize = 11; break;
                case 2: baseSize = 13; break;
                case 3: baseSize = 16; break;
                case 4: baseSize = 20; break;
            }

            foreach (var child in scheduleTableGrid.Children)
            {
                if (child is Border border)
                {
                    if (border.Child is TextBlock textBlock)
                    {
                        textBlock.FontSize = baseSize;
                    }
                    else if (border.Child is StackPanel panel)
                    {
                        SetFontSizeForPanel(panel, baseSize);
                    }
                }
            }

            foreach (var child in scheduleTableGrid.Children)
            {
                if (child is Border border && border.Child is TextBlock tb)
                {
                    if (tb.Text == "Время" || tb.Text == "Понедельник" || tb.Text == "Вторник" ||
                        tb.Text == "Среда" || tb.Text == "Четверг" || tb.Text == "Пятница")
                    {
                        tb.FontSize = baseSize + 2;
                    }
                }
            }
        }

        private void SetFontSizeForPanel(StackPanel panel, double size)
        {
            if (panel == null) return;
            foreach (var child in panel.Children)
            {
                if (child is TextBlock tb) tb.FontSize = size;
                else if (child is StackPanel subPanel) SetFontSizeForPanel(subPanel, size);
                else if (child is Grid grid) SetFontSizeForGrid(grid, size);
            }
        }

        private void SetFontSizeForGrid(Grid grid, double size)
        {
            if (grid == null) return;
            foreach (var child in grid.Children)
            {
                if (child is TextBlock tb) tb.FontSize = size;
                else if (child is StackPanel panel) SetFontSizeForPanel(panel, size);
            }
        }

        private void UpdateFontButtons()
        {
            var buttons = new[] { btnFontSmall, btnFontMedium, btnFontLarge, btnFontXLarge };
            var colors = new[] { "#666666", "#666666", "#666666", "#666666" };
            var activeColors = new[] { "#666666", "#2196F3", "#4CAF50", "#F44336" };

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null)
                {
                    buttons[i].Background = new SolidColorBrush(
                        Color.FromRgb(
                            Convert.ToByte(i == fontSizeLevel ? activeColors[i].Substring(1, 2) : colors[i].Substring(1, 2), 16),
                            Convert.ToByte(i == fontSizeLevel ? activeColors[i].Substring(3, 2) : colors[i].Substring(3, 2), 16),
                            Convert.ToByte(i == fontSizeLevel ? activeColors[i].Substring(5, 2) : colors[i].Substring(5, 2), 16)
                        )
                    );
                }
            }
        }

        // ============================================================
        // РЕЖИМЫ КОМПАКТНОСТИ
        // ============================================================

        private void btnCompactMode_Click(object sender, RoutedEventArgs e)
        {
            compactMode = true;
            if (btnCompactMode != null) btnCompactMode.Visibility = Visibility.Collapsed;
            if (btnNormalMode != null) btnNormalMode.Visibility = Visibility.Visible;
            ApplyCompactMode();
        }

        private void btnNormalMode_Click(object sender, RoutedEventArgs e)
        {
            compactMode = false;
            if (btnCompactMode != null) btnCompactMode.Visibility = Visibility.Visible;
            if (btnNormalMode != null) btnNormalMode.Visibility = Visibility.Collapsed;
            ApplyNormalMode();
        }

        private void ApplyCompactMode()
        {
            if (scheduleTableGrid == null) return;
            foreach (var child in scheduleTableGrid.Children)
            {
                if (child is Border border)
                {
                    border.Padding = new Thickness(3, 1, 3, 1);
                    if (border.Child is StackPanel panel)
                    {
                        foreach (var panelChild in panel.Children)
                        {
                            if (panelChild is TextBlock tb) tb.Margin = new Thickness(0, 1, 0, 1);
                        }
                    }
                }
            }
            for (int i = 0; i < scheduleTableGrid.RowDefinitions.Count; i++)
            {
                scheduleTableGrid.RowDefinitions[i].Height = GridLength.Auto;
            }
            if (fontSizeLevel > 2)
            {
                fontSizeLevel = 2;
                UpdateFontSize();
                UpdateFontButtons();
            }
        }

        private void ApplyNormalMode()
        {
            if (scheduleTableGrid == null) return;
            foreach (var child in scheduleTableGrid.Children)
            {
                if (child is Border border)
                {
                    border.Padding = new Thickness(5, 2, 5, 2);
                    if (border.Child is StackPanel panel)
                    {
                        foreach (var panelChild in panel.Children)
                        {
                            if (panelChild is TextBlock tb) tb.Margin = new Thickness(0, 2, 0, 2);
                        }
                    }
                }
            }
            for (int i = 0; i < scheduleTableGrid.RowDefinitions.Count; i++)
            {
                scheduleTableGrid.RowDefinitions[i].Height = GridLength.Auto;
            }
            UpdateFontSize();
        }

        // ============================================================
        // ЧЕКБОКСЫ
        // ============================================================

        private void chkShowLessonPlans_Checked(object sender, RoutedEventArgs e)
        {
            showLessonPlans = true;
            if (currentSchedule != null) RenderScheduleTable(currentSchedule);
        }

        private void chkShowLessonPlans_Unchecked(object sender, RoutedEventArgs e)
        {
            showLessonPlans = false;
            if (currentSchedule != null) RenderScheduleTable(currentSchedule);
        }

        // ============================================================
        // УДАЛЕНИЕ РАСПИСАНИЯ
        // ============================================================

        private void btnDeleteSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser.Role != UserRole.Admin)
            {
                MessageBox.Show("Доступ запрещен", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (currentSchedule == null)
            {
                MessageBox.Show("Выберите расписание для удаления", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Вы действительно хотите удалить расписание \"{currentSchedule.Name}\"?\n\n" +
                "Это действие нельзя отменить.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    currentSchedule.IsActive = false;
                    dbHelper.UpdateSchedule(currentSchedule);
                    allSchedules = dbHelper.GetAllSchedules();

                    if (allSchedules.Any())
                    {
                        if (cmbSchedule != null)
                        {
                            cmbSchedule.ItemsSource = allSchedules.Select(s =>
                                $"{s.Name} ({s.StartDate:dd.MM.yyyy} - {s.EndDate:dd.MM.yyyy})").ToList();
                            cmbSchedule.SelectedIndex = 0;
                        }
                        currentSchedule = allSchedules[0];
                        DisplaySchedule(currentSchedule);
                    }
                    else
                    {
                        currentSchedule = null;
                        if (txtScheduleTitle != null) txtScheduleTitle.Text = "Расписание";
                        if (txtNoSchedule != null) txtNoSchedule.Visibility = Visibility.Visible;
                        if (scheduleTableGrid != null)
                        {
                            scheduleTableGrid.Children.Clear();
                            scheduleTableGrid.RowDefinitions.Clear();
                        }
                        if (cmbSchedule != null) cmbSchedule.ItemsSource = null;
                    }

                    MessageBox.Show("Расписание успешно удалено", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении расписания: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ============================================================
        // УПРАВЛЕНИЕ РАСПИСАНИЕМ (КНОПКИ)
        // ============================================================

        private void btnNewSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser.Role != UserRole.Admin)
            {
                MessageBox.Show("Доступ запрещен", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new Window
            {
                Title = "Создание нового расписания",
                Width = 450,
                Height = 750,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
            };

            StackPanel panel = new StackPanel { Margin = new Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = "📅 Создание нового расписания",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Margin = new Thickness(0, 0, 0, 15)
            });

            // Название
            panel.Children.Add(new TextBlock
            {
                Text = "Название расписания:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var txtName = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10),
                Text = $"Расписание на {DateTime.Now:dd.MM.yyyy}"
            };
            panel.Children.Add(txtName);

            // Дата начала
            panel.Children.Add(new TextBlock
            {
                Text = "Дата начала:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var dpStart = new DatePicker
            {
                Margin = new Thickness(0, 0, 0, 15),
                SelectedDate = DateTime.Now,
                Padding = new Thickness(10)
            };
            panel.Children.Add(dpStart);

            // Дата окончания
            panel.Children.Add(new TextBlock
            {
                Text = "Дата окончания:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });
            var dpEnd = new DatePicker
            {
                Margin = new Thickness(0, 0, 0, 15),
                SelectedDate = DateTime.Now.AddMonths(1),
                Padding = new Thickness(10)
            };
            panel.Children.Add(dpEnd);

            // ✅ Выбор классов для добавления в расписание
            panel.Children.Add(new TextBlock
            {
                Text = "🏫 Выберите классы для расписания:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5),
                FontWeight = FontWeights.SemiBold
            });

            var allClasses = dbHelper.GetAllClasses() ?? new List<SchoolClass>();
            var classItems = allClasses.Select(c => c.Name).OrderBy(c => c).ToList();

            var lbClasses = new ListBox
            {
                Height = 120,
                SelectionMode = SelectionMode.Multiple,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255))
            };

            foreach (var className in classItems)
            {
                lbClasses.Items.Add(className);
            }
            panel.Children.Add(lbClasses);

            // Кнопки выбора классов
            StackPanel classButtonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15)
            };

            Button btnSelectAllClasses = new Button
            {
                Content = "Выбрать все",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Width = 100,
                Height = 28,
                FontSize = 11,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            btnSelectAllClasses.Click += (s, args) => lbClasses.SelectAll();
            classButtonPanel.Children.Add(btnSelectAllClasses);

            Button btnClearAllClasses = new Button
            {
                Content = "Очистить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                Width = 80,
                Height = 28,
                FontSize = 11,
                Cursor = Cursors.Hand
            };
            btnClearAllClasses.Click += (s, args) => lbClasses.SelectedItems.Clear();
            classButtonPanel.Children.Add(btnClearAllClasses);

            panel.Children.Add(classButtonPanel);

            // Информация
            Border infoBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 15)
            };
            TextBlock infoText = new TextBlock
            {
                Text = "💡 После создания расписания вы сможете добавлять уроки через кнопку \"+\" в ячейках",
                Foreground = new SolidColorBrush(Color.FromRgb(230, 81, 0)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            infoBorder.Child = infoText;
            panel.Children.Add(infoBorder);

            // Кнопки
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Button btnCreate = new Button
            {
                Content = "✅ Создать",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Margin = new Thickness(0, 0, 10, 0),
                Width = 120,
                Height = 35,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            btnCreate.Click += (createSender, createArgs) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(txtName.Text))
                    {
                        MessageBox.Show("Введите название расписания", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (lbClasses.SelectedItems.Count == 0)
                    {
                        MessageBox.Show("Выберите хотя бы один класс", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (dpStart.SelectedDate == null || dpEnd.SelectedDate == null)
                    {
                        MessageBox.Show("Выберите даты", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (dpStart.SelectedDate > dpEnd.SelectedDate)
                    {
                        MessageBox.Show("Дата начала не может быть позже даты окончания", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var selectedClasses = lbClasses.SelectedItems.Cast<string>().ToList();

                    // Создаем пустое расписание
                    var newSchedule = new Schedule
                    {
                        Name = txtName.Text.Trim(),
                        StartDate = dpStart.SelectedDate.Value,
                        EndDate = dpEnd.SelectedDate.Value,
                        Status = ScheduleStatus.Draft,
                        CreatedBy = currentUser.Id,
                        CreatedAt = DateTime.Now,
                        IsActive = true,
                        Lessons = new List<ScheduleLesson>()
                    };

                    int scheduleId = dbHelper.SaveSchedule(newSchedule);

                    if (scheduleId > 0)
                    {
                        newSchedule.Id = scheduleId;
                        currentSchedule = newSchedule;
                        allSchedules = dbHelper.GetAllSchedules();

                        // Обновляем ComboBox
                        if (cmbSchedule != null)
                        {
                            cmbSchedule.ItemsSource = allSchedules.Select(s =>
                                $"{s.Name} ({s.StartDate:dd.MM.yyyy} - {s.EndDate:dd.MM.yyyy})").ToList();

                            var createdSchedule = allSchedules.FirstOrDefault(s => s.Id == scheduleId);
                            if (createdSchedule != null)
                            {
                                int index = allSchedules.IndexOf(createdSchedule);
                                if (index >= 0) cmbSchedule.SelectedIndex = index;
                            }
                        }

                        // ✅ Отображаем пустое расписание с ячейками для добавления
                        RenderScheduleTable(currentSchedule);

                        // Автоматически включаем режим редактирования
                        isEditMode = true;
                        if (btnEditSchedule != null)
                        {
                            btnEditSchedule.Content = "✓ Завершить редактирование";
                            btnEditSchedule.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                        }
                        if (txtModeInfo != null)
                        {
                            txtModeInfo.Text = "Режим: РЕДАКТИРОВАНИЕ (можно добавлять уроки)";
                            txtModeInfo.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                            txtModeInfo.FontWeight = FontWeights.Bold;
                        }

                        dialog.Close();

                        MessageBox.Show($"✅ Расписание создано!\n\n" +
                            $"📚 Добавлено классов: {selectedClasses.Count}\n" +
                            $"💡 Нажмите на ячейку с \"+\" чтобы добавить урок",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Ошибка при создании расписания", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            buttonPanel.Children.Add(btnCreate);

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                Width = 100,
                Height = 35,
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (cancelSender, cancelArgs) => dialog.Close();
            buttonPanel.Children.Add(btnCancel);

            panel.Children.Add(buttonPanel);
            dialog.Content = panel;
            dialog.ShowDialog();
        }

        private void btnEditSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser.Role != UserRole.Admin)
            {
                MessageBox.Show("Доступ запрещен", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (currentSchedule == null)
            {
                MessageBox.Show("Выберите расписание для редактирования", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            isEditMode = !isEditMode;

            if (isEditMode)
            {
                if (btnEditSchedule != null)
                {
                    btnEditSchedule.Content = "✓ Завершить редактирование";
                    btnEditSchedule.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                }
                if (txtModeInfo != null)
                {
                    txtModeInfo.Text = "Режим: РЕДАКТИРОВАНИЕ (можно изменять уроки)";
                    txtModeInfo.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    txtModeInfo.FontWeight = FontWeights.Bold;
                }
            }
            else
            {
                if (btnEditSchedule != null)
                {
                    btnEditSchedule.Content = "✏ Редактировать расписание";
                    btnEditSchedule.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                }
                if (txtModeInfo != null)
                {
                    txtModeInfo.Text = "Режим: редактирование (УВР)";
                    txtModeInfo.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                    txtModeInfo.FontWeight = FontWeights.Normal;
                }

                dbHelper.UpdateSchedule(currentSchedule);
                allSchedules = dbHelper.GetAllSchedules();
                currentSchedule = allSchedules.FirstOrDefault(s => s.Id == currentSchedule.Id);
                DisplaySchedule(currentSchedule);

                MessageBox.Show("Изменения сохранены", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            RenderScheduleTable(currentSchedule);
        }

        private void btnApproveSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser.Role != UserRole.Admin)
            {
                MessageBox.Show("Доступ запрещен", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (currentSchedule == null)
            {
                MessageBox.Show("Выберите расписание для утверждения", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                currentSchedule.Status = ScheduleStatus.Approved;
                currentSchedule.ApprovedBy = currentUser.Id;
                currentSchedule.ApprovedDate = DateTime.Now;

                dbHelper.UpdateSchedule(currentSchedule);

                allSchedules = dbHelper.GetAllSchedules();
                currentSchedule = allSchedules.FirstOrDefault(s => s.Id == currentSchedule.Id);
                DisplaySchedule(currentSchedule);

                UpdateStatusDisplay(ScheduleStatus.Approved);
                UpdateUvrButtonsByStatus(ScheduleStatus.Approved);

                MessageBox.Show("Расписание успешно утверждено", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка утверждения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // АВТОГЕНЕРАЦИЯ
        // ============================================================

        private void btnGenerateSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser.Role != UserRole.Admin)
            {
                MessageBox.Show("Доступ запрещен", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var allTeachers = dbHelper.GetAllTeachersWithDetails();
            var subjectDifficulties = dbHelper.GetAllSubjectDifficulties();

            var dialog = new Window
            {
                Title = "Автоматическая генерация расписания",
                Width = 750,
                Height = 800,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                ResizeMode = ResizeMode.NoResize
            };

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            StackPanel panel = new StackPanel { Margin = new Thickness(25) };

            panel.Children.Add(new TextBlock
            {
                Text = "⚙️ Настройка автоматической генерации",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Margin = new Thickness(0, 0, 0, 20)
            });

            // Основные параметры
            Border mainParamsBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                Padding = new Thickness(20),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 20),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1)
            };

            StackPanel mainParamsPanel = new StackPanel();

            StackPanel mainTitlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            mainTitlePanel.Children.Add(new TextBlock { Text = "📅", FontSize = 18, Margin = new Thickness(0, 0, 10, 0) });
            mainTitlePanel.Children.Add(new TextBlock { Text = "Основные параметры", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)), FontSize = 16 });
            mainParamsPanel.Children.Add(mainTitlePanel);

            // Название
            Grid nameGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            nameGrid.Children.Add(new TextBlock
            {
                Text = "Название:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0)
            });

            var txtScheduleName = new TextBox
            {
                Text = $"Расписание по ФАОП ООО {DateTime.Now:dd.MM.yyyy}",
                Padding = new Thickness(10),
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                BorderThickness = new Thickness(1)
            };
            Grid.SetColumn(txtScheduleName, 1);
            nameGrid.Children.Add(txtScheduleName);
            mainParamsPanel.Children.Add(nameGrid);

            // Даты
            Grid dateGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
            dateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            dateGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel startDatePanel = new StackPanel();
            startDatePanel.Children.Add(new TextBlock
            {
                Text = "Дата начала:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5)
            });

            var dpStart = new DatePicker
            {
                SelectedDate = DateTime.Now,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51))
            };
            startDatePanel.Children.Add(dpStart);
            Grid.SetColumn(startDatePanel, 0);
            dateGrid.Children.Add(startDatePanel);

            StackPanel endDatePanel = new StackPanel();
            endDatePanel.Children.Add(new TextBlock
            {
                Text = "Дата окончания:",
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 0, 0, 5)
            });

            var dpEnd = new DatePicker
            {
                SelectedDate = DateTime.Now.AddMonths(1),
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51))
            };
            endDatePanel.Children.Add(dpEnd);
            Grid.SetColumn(endDatePanel, 2);
            dateGrid.Children.Add(endDatePanel);

            mainParamsPanel.Children.Add(dateGrid);
            mainParamsBorder.Child = mainParamsPanel;
            panel.Children.Add(mainParamsBorder);

            // Выбор классов
            Border classesBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                Padding = new Thickness(20),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 20),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1)
            };

            StackPanel classesMainPanel = new StackPanel();

            StackPanel classesTitlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            classesTitlePanel.Children.Add(new TextBlock { Text = "🏫", FontSize = 18, Margin = new Thickness(0, 0, 10, 0) });
            classesTitlePanel.Children.Add(new TextBlock { Text = "Выберите классы для генерации", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)), FontSize = 16 });
            classesMainPanel.Children.Add(classesTitlePanel);

            var allClasses = dbHelper.GetAllClasses() ?? new List<SchoolClass>();
            var classItems = allClasses.Select(c => c.Name).OrderBy(c => c).ToList();

            TextBlock hintText = new TextBlock
            {
                Text = "Используйте Ctrl+клик для выбора нескольких классов",
                Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 153)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            classesMainPanel.Children.Add(hintText);

            ListBox lbClasses = new ListBox
            {
                Height = 120,
                SelectionMode = SelectionMode.Multiple,
                BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                Margin = new Thickness(0, 0, 0, 10)
            };

            foreach (var className in classItems)
            {
                lbClasses.Items.Add(className);
            }

            for (int i = 0; i < lbClasses.Items.Count; i++)
            {
                lbClasses.SelectedItems.Add(lbClasses.Items[i]);
            }

            classesMainPanel.Children.Add(lbClasses);

            StackPanel classButtonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 10)
            };

            Button btnSelectAllClasses = new Button
            {
                Content = "Выбрать все",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Width = 100,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                FontSize = 12,
                Cursor = Cursors.Hand
            };
            btnSelectAllClasses.Click += (s, args) =>
            {
                lbClasses.SelectAll();
            };
            classButtonPanel.Children.Add(btnSelectAllClasses);

            Button btnClearAllClasses = new Button
            {
                Content = "Очистить",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                Width = 80,
                Height = 30,
                FontSize = 12,
                Cursor = Cursors.Hand
            };
            btnClearAllClasses.Click += (s, args) =>
            {
                lbClasses.SelectedItems.Clear();
            };
            classButtonPanel.Children.Add(btnClearAllClasses);

            classesMainPanel.Children.Add(classButtonPanel);

            TextBlock selectedCountText = new TextBlock
            {
                Text = $"Выбрано классов: {lbClasses.SelectedItems.Count}",
                Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };

            lbClasses.SelectionChanged += (s, args) =>
            {
                selectedCountText.Text = $"Выбрано классов: {lbClasses.SelectedItems.Count}";
            };

            classesMainPanel.Children.Add(selectedCountText);

            classesBorder.Child = classesMainPanel;
            panel.Children.Add(classesBorder);

            // Исключенные дни
            Border daysBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                Padding = new Thickness(20),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 0, 20),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1)
            };

            StackPanel daysMainPanel = new StackPanel();

            StackPanel daysTitlePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            daysTitlePanel.Children.Add(new TextBlock { Text = "📆", FontSize = 18, Margin = new Thickness(0, 0, 10, 0) });
            daysTitlePanel.Children.Add(new TextBlock { Text = "Исключить дни недели", FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)), FontSize = 16 });
            daysMainPanel.Children.Add(daysTitlePanel);

            WrapPanel daysWrapPanel = new WrapPanel();

            string[] dayNames = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница" };
            var dayCheckBoxes = new List<CheckBox>();

            for (int i = 0; i < dayNames.Length; i++)
            {
                Border dayBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Margin = new Thickness(0, 0, 15, 10),
                    Padding = new Thickness(10, 5, 10, 5)
                };

                CheckBox chkDay = new CheckBox
                {
                    Content = dayNames[i],
                    Tag = dayNames[i],
                    Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                    IsChecked = false
                };
                dayCheckBoxes.Add(chkDay);
                dayBorder.Child = chkDay;
                daysWrapPanel.Children.Add(dayBorder);
            }

            daysMainPanel.Children.Add(daysWrapPanel);
            daysBorder.Child = daysMainPanel;
            panel.Children.Add(daysBorder);

            var chkRespectHours = new CheckBox
            {
                Content = "✓ Учитывать нагрузку учителей",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(chkRespectHours);

            var chkConsiderDifficulty = new CheckBox
            {
                Content = "📊 Учитывать сложность предметов",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                Margin = new Thickness(0, 0, 0, 15)
            };
            panel.Children.Add(chkConsiderDifficulty);

            // Кнопки действий
            Border actionsBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                Padding = new Thickness(20),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                BorderThickness = new Thickness(1)
            };

            StackPanel actionsPanel = new StackPanel();
            StackPanel actionButtonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Button btnGenerate = new Button
            {
                Content = "⚡ СГЕНЕРИРОВАТЬ РАСПИСАНИЕ",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Margin = new Thickness(0, 0, 15, 0),
                Width = 250,
                Height = 45,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };

            Button btnCancel = new Button
            {
                Content = "Отмена",
                Style = (Style)FindResource("MainButtonStyle"),
                Background = new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                Width = 100,
                Height = 45,
                FontSize = 14,
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (cancelSender, cancelArgs) => dialog.Close();

            actionButtonsPanel.Children.Add(btnGenerate);
            actionButtonsPanel.Children.Add(btnCancel);
            actionsPanel.Children.Add(actionButtonsPanel);

            actionsBorder.Child = actionsPanel;
            panel.Children.Add(actionsBorder);

            // Обработчик генерации
            btnGenerate.Click += (generateSender, generateArgs) =>
            {
                var selectedClasses = new List<string>();
                foreach (var item in lbClasses.SelectedItems)
                {
                    selectedClasses.Add(item.ToString());
                }

                if (!selectedClasses.Any())
                {
                    MessageBox.Show("⚠️ Выберите хотя бы один класс для генерации",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var excludedDays = dayCheckBoxes
                    .Where(cb => cb.IsChecked == true)
                    .Select(cb => cb.Tag.ToString())
                    .ToList();

                if (excludedDays.Count >= 5)
                {
                    MessageBox.Show("⚠️ Нельзя исключить все дни недели",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var settings = new ScheduleGenerator.GenerationSettings
                {
                    ScheduleName = txtScheduleName.Text,
                    StartDate = dpStart.SelectedDate ?? DateTime.Now,
                    EndDate = dpEnd.SelectedDate ?? DateTime.Now.AddMonths(1),
                    LessonsPerDay = 7,
                    StartHour = 8,
                    StartMinute = 30,
                    LessonDuration = 40,
                    BreakDuration = 10,
                    Classes = selectedClasses,
                    ExcludedDays = excludedDays,
                    RespectTeacherHours = chkRespectHours.IsChecked == true,
                    ConsiderDifficulty = chkConsiderDifficulty.IsChecked == true
                };

                dialog.Close();

                // Окно загрузки
                var loadingWindow = new Window
                {
                    Title = "Генерация расписания",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    WindowStyle = WindowStyle.ToolWindow,
                    ResizeMode = ResizeMode.NoResize,
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
                };

                var loadingPanel = new StackPanel { Margin = new Thickness(25), VerticalAlignment = VerticalAlignment.Center };

                TextBlock loadingText = new TextBlock
                {
                    Text = "⏳ Генерация расписания...",
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                loadingPanel.Children.Add(loadingText);

                TextBlock infoText = new TextBlock
                {
                    Text = $"Выбрано классов: {selectedClasses.Count}",
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                loadingPanel.Children.Add(infoText);

                var progressBar = new ProgressBar
                {
                    IsIndeterminate = true,
                    Height = 20,
                    Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    Margin = new Thickness(0, 0, 0, 15)
                };
                loadingPanel.Children.Add(progressBar);

                loadingWindow.Content = loadingPanel;
                loadingWindow.Show();

                var generator = new ScheduleGenerator();
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("Начинаем генерацию расписания...");
                        var result = generator.GenerateSchedule(settings, dbHelper);
                        System.Diagnostics.Debug.WriteLine($"Генерация завершена. Уроков: {result.GeneratedSchedule?.Lessons?.Count ?? 0}");

                        Dispatcher.Invoke(() =>
                        {
                            loadingWindow.Close();

                            if (result.Success)
                            {
                                if (result.GeneratedSchedule.Lessons == null || !result.GeneratedSchedule.Lessons.Any())
                                {
                                    MessageBox.Show("⚠️ Уроки не были сгенерированы!\nПроверьте наличие учебных планов для выбранных классов.",
                                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    return;
                                }

                                result.GeneratedSchedule.CreatedBy = currentUser?.Id ?? 1;
                                int scheduleId = dbHelper.SaveSchedule(result.GeneratedSchedule);

                                if (scheduleId > 0)
                                {
                                    result.GeneratedSchedule.Id = scheduleId;
                                    allSchedules = dbHelper.GetAllSchedules();

                                    cmbSchedule.ItemsSource = allSchedules.Select(s =>
                                        $"{s.Name} ({s.StartDate:dd.MM.yyyy} - {s.EndDate:dd.MM.yyyy})").ToList();

                                    var newSchedule = allSchedules.FirstOrDefault(s => s.Id == scheduleId);
                                    if (newSchedule != null)
                                    {
                                        currentSchedule = newSchedule;
                                        int index = allSchedules.IndexOf(newSchedule);
                                        if (index >= 0)
                                            cmbSchedule.SelectedIndex = index;

                                        DisplaySchedule(currentSchedule);
                                    }

                                    MessageBox.Show($"✅ Расписание успешно сгенерировано!\n\n" +
                                        $"📊 Статистика:\n" +
                                        $"• Уроков: {result.GeneratedSchedule.Lessons.Count}\n" +
                                        $"• Классов: {result.GeneratedSchedule.Lessons.Select(l => l.Class).Distinct().Count()}\n" +
                                        $"• Учителей: {result.GeneratedSchedule.Lessons.Select(l => l.Teacher).Distinct().Count()}",
                                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                                else
                                {
                                    MessageBox.Show("❌ Ошибка при сохранении расписания в базу данных",
                                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            else
                            {
                                MessageBox.Show($"❌ Ошибка генерации: {result.Message}",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Исключение при генерации: {ex.Message}");
                        Dispatcher.Invoke(() =>
                        {
                            loadingWindow.Close();
                            MessageBox.Show($"❌ Ошибка: {ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            };

            scrollViewer.Content = panel;
            dialog.Content = scrollViewer;
            dialog.ShowDialog();
        }

        // ============================================================
        // ПОИСК
        // ============================================================

        private void PerformSearch()
        {
            try
            {
                if (currentSchedule == null || currentSchedule.Lessons == null || !currentSchedule.Lessons.Any())
                {
                    MessageBox.Show("Нет расписания для поиска", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string searchText = txtSearch.Text.Trim();

                if (string.IsNullOrWhiteSpace(searchText) ||
                    searchText == "Поиск по предмету, учителю, классу...")
                {
                    ClearSearch();
                    return;
                }

                currentSearchQuery = searchText;
                string searchLower = searchText.ToLower();

                searchResults = currentSchedule.Lessons
                    .Where(l =>
                        (l.Subject?.ToLower().Contains(searchLower) ?? false) ||
                        (l.Teacher?.ToLower().Contains(searchLower) ?? false) ||
                        (l.Class?.ToLower().Contains(searchLower) ?? false) ||
                        (l.Room?.ToLower().Contains(searchLower) ?? false) ||
                        (l.LessonPlanTitle?.ToLower().Contains(searchLower) ?? false) ||
                        (l.Note?.ToLower().Contains(searchLower) ?? false) ||
                        l.DayOfWeek?.ToLower().Contains(searchLower) == true ||
                        l.LessonNumber.ToString().Contains(searchLower)
                    )
                    .OrderBy(l => l.DayOfWeek)
                    .ThenBy(l => l.LessonNumber)
                    .ToList();

                ShowSearchResults();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка поиска: {ex.Message}");
            }
        }

        private void ShowSearchResults()
        {
            if (searchResults.Any())
            {
                if (lvSearchResults != null) lvSearchResults.ItemsSource = searchResults;
                if (txtSearchResultsCount != null) txtSearchResultsCount.Text = $"Найдено: {searchResults.Count}";
                if (searchResultsPanel != null) searchResultsPanel.Visibility = Visibility.Visible;

                HighlightSearchResults();

                if (txtSearchQuery != null) txtSearchQuery.Text = $"\"{currentSearchQuery}\"";
                if (searchHighlightPanel != null) searchHighlightPanel.Visibility = Visibility.Visible;
            }
            else
            {
                if (lvSearchResults != null) lvSearchResults.ItemsSource = null;
                if (txtSearchResultsCount != null) txtSearchResultsCount.Text = "Найдено: 0";
                if (searchResultsPanel != null) searchResultsPanel.Visibility = Visibility.Visible;

                MessageBox.Show($"По запросу \"{currentSearchQuery}\" ничего не найдено",
                    "Результаты поиска", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void HighlightSearchResults()
        {
            try
            {
                if (scheduleTableGrid == null) return;

                foreach (var child in scheduleTableGrid.Children)
                {
                    if (child is Border border && border.Tag is ScheduleLesson)
                    {
                        border.Background = Brushes.White;
                        border.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                        border.BorderThickness = new Thickness(0, 0, 0, 1);
                    }
                }

                foreach (var lesson in searchResults)
                {
                    HighlightLessonCell(lesson);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка подсветки результатов: {ex.Message}");
            }
        }

        private void HighlightLessonCell(ScheduleLesson lesson)
        {
            try
            {
                if (scheduleTableGrid == null) return;

                foreach (var child in scheduleTableGrid.Children)
                {
                    if (child is Border border && border.Tag is ScheduleLesson cellLesson)
                    {
                        if (cellLesson.Id == lesson.Id ||
                            (cellLesson.DayOfWeek == lesson.DayOfWeek &&
                             cellLesson.LessonNumber == lesson.LessonNumber &&
                             cellLesson.Class == lesson.Class))
                        {
                            border.Background = new SolidColorBrush(Color.FromRgb(255, 243, 224));
                            border.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                            border.BorderThickness = new Thickness(2);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка подсветки ячейки: {ex.Message}");
            }
        }

        private void ClearSearch()
        {
            currentSearchQuery = "";
            searchResults.Clear();
            if (searchResultsPanel != null) searchResultsPanel.Visibility = Visibility.Collapsed;
            if (searchHighlightPanel != null) searchHighlightPanel.Visibility = Visibility.Collapsed;

            if (scheduleTableGrid != null)
            {
                foreach (var child in scheduleTableGrid.Children)
                {
                    if (child is Border border)
                    {
                        border.Background = Brushes.White;
                        border.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                        border.BorderThickness = new Thickness(0, 0, 0, 1);
                    }
                }
            }

            SetSearchPlaceholder();
        }

        private void btnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            ClearSearch();
            if (currentSchedule != null) RenderScheduleTable(currentSchedule);
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void lvSearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lvSearchResults?.SelectedItem is ScheduleLesson selectedLesson)
            {
                HighlightLessonCell(selectedLesson);
            }
        }

        private void SearchResult_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            var lesson = border?.DataContext as ScheduleLesson;

            if (lesson != null)
            {
                HighlightLessonCell(lesson);
                if (lvSearchResults != null) lvSearchResults.SelectedItem = lesson;

                MessageBox.Show(
                    $"Урок: {lesson.Subject}\n" +
                    $"Класс: {lesson.Class}\n" +
                    $"День: {lesson.DayOfWeek}, {lesson.LessonNumber} урок\n" +
                    $"Кабинет: {lesson.Room ?? "нет"}\n" +
                    $"План урока: {(string.IsNullOrEmpty(lesson.LessonPlanTitle) ? "нет" : lesson.LessonPlanTitle)}",
                    "Информация об уроке",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        // ============================================================
        // ЭКСПОРТ
        // ============================================================

        private void btnExportSchedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentSchedule == null)
                {
                    MessageBox.Show("Нет расписания для экспорта", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "PDF файлы (*.pdf)|*.pdf",
                    FilterIndex = 1,
                    FileName = $"Расписание_{currentSchedule.Name}_{DateTime.Now:yyyyMMdd_HHmm}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var pdfExporter = new PdfExporter();
                    pdfExporter.ExportSchedule(currentSchedule);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // ЗАМЕТКИ УЧИТЕЛЯ
        // ============================================================

        private void btnSaveNote_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser.Role != UserRole.Teacher)
            {
                MessageBox.Show("Доступ запрещен", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selectedLesson == null)
            {
                MessageBox.Show("Выберите урок в расписании", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (selectedLesson.Teacher != currentUser.FullName && teacherFilterEnabled) // ✅ Исправлено: currentUser.Name → currentUser.FullName
            {
                MessageBox.Show("Вы можете добавлять заметки только к своим урокам", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                selectedLesson.Note = txtNote.Text;

                dbHelper.SaveTeacherNote(currentUser.Id, selectedLesson.Id, txtNote.Text);

                DisplaySchedule(currentSchedule);

                MessageBox.Show("Заметка сохранена", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                if (txtNote != null) txtNote.Text = "";
                if (txtNote != null) txtNote.IsEnabled = false;
                if (btnSaveNote != null) btnSaveNote.IsEnabled = false;
                selectedLesson = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения заметки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
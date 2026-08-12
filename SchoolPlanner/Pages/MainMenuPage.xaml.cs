using SchoolPlanner.Database;
using SchoolPlanner.Page;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SchoolPlanner.Pages
{
    public partial class MainMenuPage
    {
        private DbHelper dbHelper;
        private User currentUser;

        public MainMenuPage()
        {
            try
            {
                InitializeComponent();
                dbHelper = new DbHelper();
                currentUser = App.CurrentUser;

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
                txtWelcome.Text = "Добро пожаловать!";
                txtWelcomeName.Text = currentUser.FullName;
                txtDate.Text = $"Сегодня: {DateTime.Now:dd MMMM yyyy}";

                LoadStatistics();

                if (currentUser.Role == UserRole.Admin)
                {
                    uvrPanel.Visibility = Visibility.Visible;
                    teacherPanel.Visibility = Visibility.Collapsed;
                    LoadPendingPlans();
                }
                else
                {
                    uvrPanel.Visibility = Visibility.Collapsed;
                    teacherPanel.Visibility = Visibility.Visible;
                }

                LoadRecentPlans();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка Page_Loaded: {ex.Message}");
            }
        }

        private void LoadStatistics()
        {
            try
            {
                var allPlans = dbHelper.GetAllLessonPlans();
                txtPlansCount.Text = allPlans.Count.ToString();

                var allSchedules = dbHelper.GetAllSchedules();
                txtSchedulesCount.Text = allSchedules.Count.ToString();

                var allStudents = dbHelper.GetAllStudents();
                txtStudentsCount.Text = allStudents.Count.ToString();
                txtStudentsCountSmall.Text = $"{allStudents.Count} уч.";

                LoadFgosStatistics();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadStatistics: {ex.Message}");
                txtPlansCount.Text = "0";
                txtSchedulesCount.Text = "0";
                txtStudentsCount.Text = "0";
                txtStudentsCountSmall.Text = "0 уч.";
            }
        }

        private void LoadFgosStatistics()
        {
            try
            {
                var fgosFiles = dbHelper.GetAllFgosFiles(null, null);
                int fgosCount = fgosFiles?.Count ?? 0;

                if (txtFgosCount != null)
                    txtFgosCount.Text = fgosCount.ToString();

                if (txtFgosCountSmall != null)
                    txtFgosCountSmall.Text = $"{fgosCount} файлов";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadFgosStatistics: {ex.Message}");
                if (txtFgosCount != null)
                    txtFgosCount.Text = "0";
                if (txtFgosCountSmall != null)
                    txtFgosCountSmall.Text = "0 файлов";
            }
        }

        private void LoadPendingPlans()
        {
            try
            {
                var allPlans = dbHelper.GetAllLessonPlans();
                var pendingPlans = allPlans.Where(p => p.Status == LessonStatus.Pending).ToList();
                lvPendingPlans.ItemsSource = pendingPlans;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadPendingPlans: {ex.Message}");
                lvPendingPlans.ItemsSource = null;
            }
        }

        private void LoadRecentPlans()
        {
            try
            {
                if (currentUser.Role == UserRole.Teacher)
                {
                    var teacherPlans = dbHelper.GetLessonPlansByTeacher(currentUser.Id);
                    lvRecentPlans.ItemsSource = teacherPlans.OrderByDescending(p => p.CreatedDate).Take(5).ToList();
                }
                else
                {
                    var allPlans = dbHelper.GetAllLessonPlans();
                    lvRecentPlans.ItemsSource = allPlans.OrderByDescending(p => p.CreatedDate).Take(5).ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadRecentPlans: {ex.Message}");
                lvRecentPlans.ItemsSource = null;
            }
        }

        private string GetStatusText(LessonStatus status)
        {
            switch (status)
            {
                case LessonStatus.Draft: return "Черновик";
                case LessonStatus.Pending: return "На проверке";
                case LessonStatus.Approved: return "Утверждено";
                case LessonStatus.RequiresRevision: return "Требует доработки";
                default: return status.ToString();
            }
        }

        // ============================================================
        // НАВИГАЦИЯ
        // ============================================================

        private void btnConstructor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService?.Navigate(new ConstructorPage());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка навигации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConstructorCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            btnConstructor_Click(sender, e);
        }

        private void btnSchedule_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService?.Navigate(new SchedulePage());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка навигации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ScheduleCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            btnSchedule_Click(sender, e);
        }

        private void btnStudents_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService?.Navigate(new StudentsPage());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка навигации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StudentsCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            btnStudents_Click(sender, e);
        }

        // Файлы ФГОС - только для УВР
        private void btnFgosFiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentUser.Role != UserRole.Admin)
                {
                    MessageBox.Show("Доступ запрещен. Только для УВР.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                NavigationService?.Navigate(new FgosFilesPage());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка навигации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FgosCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            btnFgosFiles_Click(sender, e);
        }

        // Для учителей - просмотр файлов ФГОС
        private void btnFgosView_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MessageBox.Show("Функция просмотра файлов ФГОС для учителей будет доступна в следующей версии",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Админ панель - ОТКРЫВАЕМ ОКНО ВМЕСТО СТРАНИЦЫ
        private void btnAdminPanel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentUser.Role != UserRole.Admin)
                {
                    MessageBox.Show("Доступ запрещен", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Открываем AdminPanelPage как страницу
                NavigationService?.Navigate(new AdminPanelPage());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Проверка плана
        private void btnCheckPlan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag != null)
                {
                    int planId = (int)button.Tag;

                    var allPlans = dbHelper.GetAllLessonPlans();
                    var plan = allPlans.FirstOrDefault(p => p.Id == planId);

                    if (plan != null)
                    {
                        var constructorPage = new ConstructorPage();
                        NavigationService?.Navigate(constructorPage);

                        MessageBox.Show($"Открыт план: {plan.Title}", "Информация",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ОТЧЕТ ПО НАГРУЗКЕ - ОТКРЫВАЕМ ОКНО
        private void btnTeacherLoadReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentUser.Role != UserRole.Admin)
                {
                    MessageBox.Show("Доступ запрещен", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var reportWindow = new TeacherLoadReportWindow();
                reportWindow.Owner = Window.GetWindow(this);
                reportWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // СПИСОК КЛАССОВ - ОТКРЫВАЕМ ОКНО
        private void btnClassList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentUser.Role != UserRole.Admin)
                {
                    MessageBox.Show("Доступ запрещен", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var classListWindow = new ClassListWindow();
                classListWindow.Owner = Window.GetWindow(this);
                classListWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // АРХИВ РАСПИСАНИЙ - ОТКРЫВАЕМ ОКНО
        private void btnArchive_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentUser.Role != UserRole.Admin)
                {
                    MessageBox.Show("Доступ запрещен", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var archiveWindow = new ScheduleArchiveWindow();
                archiveWindow.Owner = Window.GetWindow(this);
                archiveWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ИМПОРТ УЧЕНИКОВ
        private void btnImportStudents_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentUser.Role != UserRole.Admin)
                {
                    MessageBox.Show("Доступ запрещен", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var studentsPage = new StudentsPage();

                studentsPage.Loaded += (loadSender, loadArgs) =>
                {
                    try
                    {
                        var method = studentsPage.GetType().GetMethod("ImportStudentsAutomatically");
                        if (method != null)
                        {
                            method.Invoke(studentsPage, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка импорта: {ex.Message}");
                    }
                };

                NavigationService?.Navigate(studentsPage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
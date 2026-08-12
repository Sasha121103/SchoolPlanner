using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using System.IO;
using SchoolPlanner.Database;
using SchoolPlanner.Services;

namespace SchoolPlanner.Pages
{
    // Конвертеры для статусов
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is LessonStatus status)
            {
                switch (status)
                {
                    case LessonStatus.Draft: return new SolidColorBrush(Color.FromRgb(117, 117, 117));
                    case LessonStatus.Pending: return new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    case LessonStatus.Approved: return new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    case LessonStatus.RequiresRevision: return new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    default: return new SolidColorBrush(Color.FromRgb(117, 117, 117));
                }
            }
            return new SolidColorBrush(Color.FromRgb(117, 117, 117));
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is LessonStatus status)
            {
                switch (status)
                {
                    case LessonStatus.Draft: return "Черновик";
                    case LessonStatus.Pending: return "На проверке";
                    case LessonStatus.Approved: return "Утвержден";
                    case LessonStatus.RequiresRevision: return "На доработке";
                    default: return status.ToString();
                }
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class ConstructorPage
    {
        private DbHelper dbHelper;
        private User currentUser;
        private LessonPlan currentPlan;
        private List<LessonPlan> allPlans;
        private bool isLoaded = false;
        private bool isEditMode = false;

        private List<Subject> allSubjects;
        private List<SchoolClass> allClasses;

        private List<FgosFileViewModel> availableFgosFiles = new List<FgosFileViewModel>();
        private List<DbHelper.FgosFile> attachedFgosFiles = new List<DbHelper.FgosFile>();

        public class FgosFileViewModel : DbHelper.FgosFile
        {
            public bool IsSelected { get; set; }
            public string FileSizeDisplay => GetFileSizeDisplay(FileSize);

            private string GetFileSizeDisplay(long bytes)
            {
                if (bytes < 1024) return $"{bytes} Б";
                if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} КБ";
                return $"{bytes / (1024.0 * 1024.0):F1} МБ";
            }
        }

        public ConstructorPage()
        {
            try
            {
                InitializeComponent();

                dbHelper = new DbHelper();

                if (App.CurrentUser == null)
                {
                    MessageBox.Show("Ошибка авторизации. Пожалуйста, войдите в систему.", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    NavigationService.GoBack();
                    return;
                }

                currentUser = App.CurrentUser;

                LoadSubjects();
                LoadClasses();
                SetupRoleBasedUI();
                Loaded += Page_Loaded;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSubjects()
        {
            try
            {
                allSubjects = dbHelper.GetAllSubjects() ?? new List<Subject>();
                if (allSubjects.Any())
                {
                    cmbSubject.ItemsSource = allSubjects;
                    cmbSubject.DisplayMemberPath = "Name";
                    cmbSubject.SelectedValuePath = "Id";
                    cmbSubject.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки предметов: {ex.Message}");
                allSubjects = new List<Subject>();
            }
        }

        private void LoadClasses()
        {
            try
            {
                allClasses = dbHelper.GetAllClasses() ?? new List<SchoolClass>();
                if (allClasses.Any())
                {
                    cmbClass.ItemsSource = allClasses;
                    cmbClass.DisplayMemberPath = "Name";
                    cmbClass.SelectedValuePath = "Id";
                    cmbClass.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки классов: {ex.Message}");
                allClasses = new List<SchoolClass>();
            }
        }

        private void SetupRoleBasedUI()
        {
            try
            {
                if (currentUser.Role == UserRole.Admin) // УВР
                {
                    if (uvrCommentsPanel != null)
                        uvrCommentsPanel.Visibility = Visibility.Visible;

                    if (uvrButtons != null)
                        uvrButtons.Visibility = Visibility.Visible;

                    if (teacherButtons != null)
                        teacherButtons.Visibility = Visibility.Visible;

                    if (btnManageFgosFiles != null)
                        btnManageFgosFiles.Visibility = Visibility.Visible;

                    // ✅ Кнопка удаления ВИДНА только для УВР
                    if (btnDeletePlan != null)
                        btnDeletePlan.Visibility = Visibility.Visible;

                    if (txtRoleIndicator != null)
                        txtRoleIndicator.Text = "👤 УВР";

                    SetFormReadOnly(false);
                }
                else // Учитель
                {
                    if (uvrCommentsPanel != null)
                        uvrCommentsPanel.Visibility = Visibility.Collapsed;

                    if (uvrButtons != null)
                        uvrButtons.Visibility = Visibility.Collapsed;

                    if (teacherButtons != null)
                        teacherButtons.Visibility = Visibility.Visible;

                    if (btnManageFgosFiles != null)
                        btnManageFgosFiles.Visibility = Visibility.Collapsed;

                    // ❌ Кнопка удаления СКРЫТА для учителей
                    if (btnDeletePlan != null)
                        btnDeletePlan.Visibility = Visibility.Collapsed;

                    if (txtRoleIndicator != null)
                        txtRoleIndicator.Text = "👤 Учитель";

                    SetFormReadOnly(false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка SetupRoleBasedUI: {ex.Message}");
            }
        }

        private void SetFormReadOnly(bool isReadOnly)
        {
            try
            {
                txtTitle.IsReadOnly = isReadOnly;
                txtGoal.IsReadOnly = isReadOnly;
                cmbSubject.IsEnabled = !isReadOnly;
                cmbClass.IsEnabled = !isReadOnly;
                txtNewTask.IsReadOnly = isReadOnly;
                txtStageName.IsReadOnly = isReadOnly;
                txtStageDuration.IsReadOnly = isReadOnly;
                txtStageDescription.IsReadOnly = isReadOnly;
                txtStageExample.IsReadOnly = isReadOnly;
                btnAddStage.IsEnabled = !isReadOnly;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка SetFormReadOnly: {ex.Message}");
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (!isLoaded)
            {
                LoadPlans();
                isLoaded = true;
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private void LoadPlans()
        {
            try
            {
                if (currentUser.Role == UserRole.Teacher)
                {
                    allPlans = dbHelper.GetLessonPlansByTeacher(currentUser.Id) ?? new List<LessonPlan>();
                }
                else
                {
                    allPlans = dbHelper.GetAllLessonPlans() ?? new List<LessonPlan>();
                }

                ApplyFilter();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadPlans: {ex.Message}");
                allPlans = new List<LessonPlan>();
                if (lvLessonPlans != null)
                    lvLessonPlans.ItemsSource = allPlans;
            }
        }

        private void ApplyFilter()
        {
            try
            {
                if (lvLessonPlans == null || allPlans == null)
                    return;

                var filter = (cmbFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
                List<LessonPlan> filteredPlans;

                switch (filter)
                {
                    case "📄 Черновики":
                        filteredPlans = allPlans.Where(p => p.Status == LessonStatus.Draft).ToList();
                        break;
                    case "🔄 На проверке":
                        filteredPlans = allPlans.Where(p => p.Status == LessonStatus.Pending).ToList();
                        break;
                    case "✅ Утверждено":
                        filteredPlans = allPlans.Where(p => p.Status == LessonStatus.Approved).ToList();
                        break;
                    case "⚠️ Требуют доработки":
                        filteredPlans = allPlans.Where(p => p.Status == LessonStatus.RequiresRevision).ToList();
                        break;
                    default:
                        filteredPlans = allPlans;
                        break;
                }

                lvLessonPlans.ItemsSource = filteredPlans;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка ApplyFilter: {ex.Message}");
                if (lvLessonPlans != null)
                    lvLessonPlans.ItemsSource = allPlans ?? new List<LessonPlan>();
            }
        }

        private void ClearForm()
        {
            try
            {
                txtTitle.Text = "";
                if (txtTitleHint != null) txtTitleHint.Visibility = Visibility.Visible;

                txtGoal.Text = "";
                if (txtGoalHint != null) txtGoalHint.Visibility = Visibility.Visible;

                if (cmbSubject.Items.Count > 0) cmbSubject.SelectedIndex = 0;
                if (cmbClass.Items.Count > 0) cmbClass.SelectedIndex = 0;

                lvTasks.ItemsSource = null;
                lvStages.ItemsSource = null;

                txtNewTask.Text = "";
                if (txtNewTaskHint != null) txtNewTaskHint.Visibility = Visibility.Visible;

                txtStageName.Text = "";
                if (txtStageNameHint != null) txtStageNameHint.Visibility = Visibility.Visible;

                txtStageDuration.Text = "5";
                if (txtStageDurationHint != null) txtStageDurationHint.Visibility = Visibility.Collapsed;

                txtStageDescription.Text = "";
                if (txtStageDescriptionHint != null) txtStageDescriptionHint.Visibility = Visibility.Visible;

                txtStageExample.Text = "";
                if (txtStageExampleHint != null) txtStageExampleHint.Visibility = Visibility.Visible;

                txtUvrComment.Text = "";
                if (txtUvrCommentHint != null) txtUvrCommentHint.Visibility = Visibility.Visible;

                availableFgosFiles.Clear();
                if (lvAvailableFgosFiles != null) lvAvailableFgosFiles.ItemsSource = null;
                attachedFgosFiles.Clear();
                if (lvAttachedFgosFiles != null) lvAttachedFgosFiles.ItemsSource = null;

                currentPlan = null;
                isEditMode = false;
                txtEditorTitle.Text = "Создание нового плана";
                btnSavePlan.Content = "💾 Сохранить";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка ClearForm: {ex.Message}");
            }
        }

        private void btnNewPlan_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            if (currentUser.Role == UserRole.Admin)
            {
                SetFormReadOnly(false);
            }
        }

        private void btnEditPlan_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser.Role == UserRole.Admin && currentPlan != null)
            {
                isEditMode = true;
                SetFormReadOnly(false);
                txtEditorTitle.Text = $"✏ Редактирование: {currentPlan.Title}";
                btnSavePlan.Content = "💾 Сохранить изменения";
            }
        }

        private void btnDeletePlan_Click(object sender, RoutedEventArgs e)
        {
            // ❌ ТОЛЬКО УВР (ADMIN) может удалять планы
            if (currentUser.Role != UserRole.Admin)
            {
                MessageBox.Show("❌ Удаление планов уроков доступно только УВР!",
                    "Доступ запрещен",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (currentPlan == null)
            {
                MessageBox.Show("Выберите план для удаления", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Вы действительно хотите удалить план урока:\n\n" +
                $"📚 Предмет: {currentPlan.Subject}\n" +
                $"📖 Тема: {currentPlan.Title}\n" +
                $"🏫 Класс: {currentPlan.Class}\n" +
                $"👤 Автор: {currentPlan.TeacherName}\n" +
                $"📅 Дата создания: {currentPlan.CreatedDate:dd.MM.yyyy}\n" +
                $"📊 Статус: {GetStatusText(currentPlan.Status)}\n\n" +
                "⚠️ Это действие нельзя отменить!",
                "⚠️ Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    bool deleted = dbHelper.DeleteLessonPlan(currentPlan.Id);

                    if (deleted)
                    {
                        MessageBox.Show("✅ План урока успешно удален", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        LoadPlans();
                        ClearForm();
                    }
                    else
                    {
                        MessageBox.Show("❌ Не удалось удалить план. Возможно, он используется в расписании.", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Ошибка удаления: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Вспомогательный метод для получения текста статуса
        private string GetStatusText(LessonStatus status)
        {
            switch (status)
            {
                case LessonStatus.Draft: return "Черновик";
                case LessonStatus.Pending: return "На проверке";
                case LessonStatus.Approved: return "Утвержден";
                case LessonStatus.RequiresRevision: return "На доработке";
                default: return status.ToString();
            }
        }

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(txtNewTask?.Text))
                {
                    var tasks = lvTasks?.ItemsSource?.Cast<string>().ToList() ?? new List<string>();
                    tasks.Add(txtNewTask.Text);
                    lvTasks.ItemsSource = null;
                    lvTasks.ItemsSource = tasks;
                    txtNewTask.Text = "";
                    if (txtNewTaskHint != null) txtNewTaskHint.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveTask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var task = button?.Tag as string;

                if (task != null && lvTasks != null)
                {
                    var tasks = lvTasks.ItemsSource?.Cast<string>().ToList() ?? new List<string>();
                    tasks.Remove(task);
                    lvTasks.ItemsSource = null;
                    lvTasks.ItemsSource = tasks;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка RemoveTask: {ex.Message}");
            }
        }

        private void AddStage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(txtStageName?.Text))
                {
                    var stages = lvStages?.ItemsSource?.Cast<LessonStage>().ToList() ?? new List<LessonStage>();
                    stages.Add(new LessonStage
                    {
                        Name = txtStageName.Text,
                        Duration = int.TryParse(txtStageDuration?.Text, out int d) ? d : 5,
                        Description = txtStageDescription?.Text ?? "",
                        Example = txtStageExample?.Text ?? ""
                    });
                    lvStages.ItemsSource = null;
                    lvStages.ItemsSource = stages;

                    txtStageName.Text = "";
                    if (txtStageNameHint != null) txtStageNameHint.Visibility = Visibility.Visible;

                    txtStageDescription.Text = "";
                    if (txtStageDescriptionHint != null) txtStageDescriptionHint.Visibility = Visibility.Visible;

                    txtStageExample.Text = "";
                    if (txtStageExampleHint != null) txtStageExampleHint.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveStage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var stage = button?.Tag as LessonStage;

                if (stage != null && lvStages != null)
                {
                    var stages = lvStages.ItemsSource?.Cast<LessonStage>().ToList() ?? new List<LessonStage>();
                    stages.Remove(stage);
                    lvStages.ItemsSource = null;
                    lvStages.ItemsSource = stages;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка RemoveStage: {ex.Message}");
            }
        }

        private void btnSavePlan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ValidateForm())
                {
                    var plan = CreatePlanFromForm();

                    if (currentUser.Role == UserRole.Admin && plan.Id == 0)
                    {
                        plan.TeacherId = currentUser.Id;
                        plan.TeacherName = currentUser.FullName;
                    }

                    var planId = dbHelper.SaveLessonPlan(plan);

                    if (planId > 0)
                    {
                        MessageBox.Show("✅ План успешно сохранен!", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        if (currentUser.Role == UserRole.Admin && isEditMode)
                        {
                            isEditMode = false;
                            btnSavePlan.Content = "💾 Сохранить";
                        }

                        LoadPlans();
                        ClearForm();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(txtTitle?.Text))
            {
                MessageBox.Show("Введите тему урока", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtGoal?.Text))
            {
                MessageBox.Show("Введите цель урока", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (cmbSubject?.SelectedItem == null)
            {
                MessageBox.Show("Выберите предмет", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (cmbClass?.SelectedItem == null)
            {
                MessageBox.Show("Выберите класс", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var tasks = lvTasks?.ItemsSource?.Cast<string>().ToList();
            if (tasks == null || !tasks.Any())
            {
                MessageBox.Show("Добавьте хотя бы одну задачу", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var stages = lvStages?.ItemsSource?.Cast<LessonStage>().ToList();
            if (stages == null || !stages.Any())
            {
                MessageBox.Show("Добавьте хотя бы один этап урока", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private LessonPlan CreatePlanFromForm()
        {
            string subjectName = "";
            string className = "";

            if (cmbSubject.SelectedItem is Subject subject)
                subjectName = subject.Name;
            else if (cmbSubject.SelectedItem != null)
                subjectName = cmbSubject.SelectedItem.ToString();

            if (cmbClass.SelectedItem is SchoolClass schoolClass)
                className = schoolClass.Name;
            else if (cmbClass.SelectedItem != null)
                className = cmbClass.SelectedItem.ToString();

            return new LessonPlan
            {
                Id = currentPlan?.Id ?? 0,
                Title = txtTitle.Text,
                Subject = subjectName,
                Class = className,
                Goal = txtGoal.Text,
                Tasks = lvTasks?.ItemsSource?.Cast<string>().ToList() ?? new List<string>(),
                Stages = lvStages?.ItemsSource?.Cast<LessonStage>().ToList() ?? new List<LessonStage>(),
                TeacherId = currentPlan?.TeacherId ?? currentUser.Id,
                TeacherName = currentPlan?.TeacherName ?? currentUser.FullName,
                CreatedDate = currentPlan?.CreatedDate ?? DateTime.Now,
                Status = currentPlan?.Status ?? LessonStatus.Draft
            };
        }

        private void btnSendToCheck_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentPlan != null)
                {
                    if (ValidateForm())
                    {
                        currentPlan.Title = txtTitle.Text;
                        currentPlan.Goal = txtGoal.Text;

                        if (cmbSubject.SelectedItem is Subject subject)
                            currentPlan.Subject = subject.Name;

                        if (cmbClass.SelectedItem is SchoolClass schoolClass)
                            currentPlan.Class = schoolClass.Name;

                        currentPlan.Tasks = lvTasks?.ItemsSource?.Cast<string>().ToList() ?? new List<string>();
                        currentPlan.Stages = lvStages?.ItemsSource?.Cast<LessonStage>().ToList() ?? new List<LessonStage>();

                        dbHelper.SaveLessonPlan(currentPlan);
                        dbHelper.UpdateLessonPlanStatus(currentPlan.Id, LessonStatus.Pending);

                        MessageBox.Show("📤 План отправлен на проверку", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadPlans();
                        ClearForm();
                    }
                }
                else
                {
                    MessageBox.Show("Сначала сохраните план", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnApprovePlan_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser.Role != UserRole.Admin) return;

            try
            {
                if (currentPlan != null)
                {
                    if (string.IsNullOrWhiteSpace(txtUvrComment?.Text))
                    {
                        MessageBox.Show("Добавьте комментарий к плану", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    dbHelper.UpdateLessonPlanStatus(currentPlan.Id, LessonStatus.Approved, currentUser.Id);
                    dbHelper.AddComment(currentPlan.Id, null, currentUser.Id, currentUser.FullName, txtUvrComment.Text);
                    MessageBox.Show("✅ План утвержден", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadPlans();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnRejectPlan_Click(object sender, RoutedEventArgs e)
        {
            if (currentUser.Role != UserRole.Admin) return;

            try
            {
                if (currentPlan != null)
                {
                    if (string.IsNullOrWhiteSpace(txtUvrComment?.Text))
                    {
                        MessageBox.Show("Добавьте комментарий с замечаниями", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    dbHelper.UpdateLessonPlanStatus(currentPlan.Id, LessonStatus.RequiresRevision);
                    dbHelper.AddComment(currentPlan.Id, null, currentUser.Id, currentUser.FullName, txtUvrComment.Text);
                    MessageBox.Show("↩️ План отправлен на доработку", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadPlans();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentPlan != null)
                {
                    var pdfExporter = new PdfExporter();
                    pdfExporter.ExportLessonPlan(currentPlan);
                }
                else
                {
                    MessageBox.Show("Выберите план для экспорта", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void lvLessonPlans_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (lvLessonPlans?.SelectedItem is LessonPlan selectedPlan)
                {
                    currentPlan = selectedPlan;

                    txtEditorTitle.Text = selectedPlan.Title;
                    txtTitle.Text = selectedPlan.Title;
                    if (txtTitleHint != null) txtTitleHint.Visibility = string.IsNullOrEmpty(txtTitle.Text) ? Visibility.Visible : Visibility.Collapsed;

                    txtGoal.Text = selectedPlan.Goal;
                    if (txtGoalHint != null) txtGoalHint.Visibility = string.IsNullOrEmpty(txtGoal.Text) ? Visibility.Visible : Visibility.Collapsed;

                    if (cmbSubject != null && allSubjects != null)
                    {
                        var subject = allSubjects.FirstOrDefault(s => s.Name == selectedPlan.Subject);
                        if (subject != null)
                            cmbSubject.SelectedItem = subject;
                    }

                    if (cmbClass != null && allClasses != null)
                    {
                        var schoolClass = allClasses.FirstOrDefault(c => c.Name == selectedPlan.Class);
                        if (schoolClass != null)
                            cmbClass.SelectedItem = schoolClass;
                    }

                    lvTasks.ItemsSource = selectedPlan.Tasks;
                    lvStages.ItemsSource = selectedPlan.Stages;

                    if (currentUser.Role == UserRole.Admin)
                    {
                        SetFormReadOnly(false);
                    }

                    LoadFgosFilesForSubject();
                    LoadAttachedFiles();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка SelectionChanged: {ex.Message}");
            }
        }

        private void cmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void cmbSubject_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadFgosFilesForSubject();
        }

        // Методы для работы с файлами ФГОС
        private void LoadFgosFilesForSubject()
        {
            try
            {
                string subjectName = "";

                if (cmbSubject?.SelectedItem is Subject subject)
                {
                    subjectName = subject.Name;
                }
                else if (cmbSubject?.SelectedItem != null)
                {
                    subjectName = cmbSubject.SelectedItem.ToString();
                }

                if (string.IsNullOrEmpty(subjectName))
                    return;

                var subjectInfo = allSubjects?.FirstOrDefault(s => s.Name == subjectName);
                if (subjectInfo != null)
                {
                    var files = dbHelper.GetAllFgosFiles(subjectInfo.Id, null);
                    availableFgosFiles = files.Select(f => new FgosFileViewModel
                    {
                        Id = f.Id,
                        SubjectId = f.SubjectId,
                        SubjectName = f.SubjectName,
                        FileName = f.FileName,
                        FilePath = f.FilePath,
                        FileSize = f.FileSize,
                        FileType = f.FileType,
                        GradeLevel = f.GradeLevel,
                        Variant = f.Variant,
                        Description = f.Description,
                        UploadDate = f.UploadDate,
                        UploadedBy = f.UploadedBy,
                        UploadedByName = f.UploadedByName,
                        DownloadCount = f.DownloadCount,
                        IsActive = f.IsActive,
                        IsSelected = false
                    }).ToList();
                }
                else
                {
                    availableFgosFiles = new List<FgosFileViewModel>();
                }

                lvAvailableFgosFiles.ItemsSource = availableFgosFiles;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки файлов ФГОС: {ex.Message}");
                availableFgosFiles = new List<FgosFileViewModel>();
                if (lvAvailableFgosFiles != null)
                    lvAvailableFgosFiles.ItemsSource = availableFgosFiles;
            }
        }

        private void LoadAttachedFiles()
        {
            try
            {
                if (currentPlan != null)
                {
                    attachedFgosFiles = dbHelper.GetFgosFilesForLessonPlan(currentPlan.Id);
                    lvAttachedFgosFiles.ItemsSource = attachedFgosFiles;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки прикрепленных файлов: {ex.Message}");
            }
        }

        private void btnManageFgosFiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NavigationService.Navigate(new FgosFilesPage());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDownloadFgosFile_Click(object sender, RoutedEventArgs e)
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
                    dbHelper.IncrementFgosDownloadCount(file.Id);
                    MessageBox.Show("✅ Файл успешно скачан", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnAttachFiles_Click(object sender, RoutedEventArgs e)
        {
            if (currentPlan == null)
            {
                MessageBox.Show("Сначала сохраните план урока", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var selectedFiles = availableFgosFiles.Where(f => f.IsSelected).ToList();
                if (!selectedFiles.Any())
                {
                    MessageBox.Show("Выберите файлы для прикрепления", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int attached = 0;
                foreach (var file in selectedFiles)
                {
                    if (dbHelper.AttachFgosToLessonPlan(currentPlan.Id, file.Id))
                        attached++;
                }

                MessageBox.Show($"✅ Прикреплено {attached} файлов", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                LoadAttachedFiles();

                foreach (var file in availableFgosFiles)
                    file.IsSelected = false;

                if (lvAvailableFgosFiles != null)
                    lvAvailableFgosFiles.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnRefreshFiles_Click(object sender, RoutedEventArgs e)
        {
            LoadFgosFilesForSubject();
            MessageBox.Show("🔄 Список обновлен", "Информация",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnRemoveAttachedFile_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var file = button?.Tag as DbHelper.FgosFile;
            if (file == null || currentPlan == null) return;

            var result = MessageBox.Show($"Удалить файл {file.FileName} из плана урока?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    if (dbHelper.DetachFgosFromLessonPlan(currentPlan.Id, file.Id))
                    {
                        MessageBox.Show("✅ Файл удален из плана", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadAttachedFiles();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Ошибка: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #region Обработчики для подсказок (placeholders)

        private void txtTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtTitleHint != null)
                txtTitleHint.Visibility = string.IsNullOrEmpty(txtTitle.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtGoal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtGoalHint != null)
                txtGoalHint.Visibility = string.IsNullOrEmpty(txtGoal.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtNewTask_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtNewTaskHint != null)
                txtNewTaskHint.Visibility = string.IsNullOrEmpty(txtNewTask.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtStageName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtStageNameHint != null)
                txtStageNameHint.Visibility = string.IsNullOrEmpty(txtStageName.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtStageDuration_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtStageDurationHint != null)
                txtStageDurationHint.Visibility = string.IsNullOrEmpty(txtStageDuration.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtStageDescription_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtStageDescriptionHint != null)
                txtStageDescriptionHint.Visibility = string.IsNullOrEmpty(txtStageDescription.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtStageExample_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtStageExampleHint != null)
                txtStageExampleHint.Visibility = string.IsNullOrEmpty(txtStageExample.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void txtUvrComment_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtUvrCommentHint != null)
                txtUvrCommentHint.Visibility = string.IsNullOrEmpty(txtUvrComment.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion
    }
}
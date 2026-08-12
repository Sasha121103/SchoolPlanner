using SchoolPlanner.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static SchoolPlanner.Database.Attendance;
using static SchoolPlanner.Database.DbHelper;

namespace SchoolPlanner.Pages
{
    public partial class LogViewerWindow : Window
    {
        private DbHelper dbHelper;
        private string className;

        // Класс для отображения логов
        public class LogDisplayItem
        {
            public DateTime Date { get; set; }
            public string UserName { get; set; }
            public string Action { get; set; }
            public string ActionText { get; set; }
            public SolidColorBrush ActionColor { get; set; }
            public string StudentName { get; set; }
            public string Subject { get; set; }
            public string Comment { get; set; }
        }

        public LogViewerWindow(DbHelper dbHelper, string className)
        {
            InitializeComponent();
            this.dbHelper = dbHelper;
            this.className = className;

            txtClassInfo.Text = $"Класс: {className}";
            LoadLogs();
        }

        private void LoadLogs()
        {
            try
            {
                // Проверяем существование таблицы
                var logs = dbHelper.GetLogsForPeriod(DateTime.Now.AddDays(-30), DateTime.Now);

                if (logs == null)
                {
                    logs = new List<LogEntry>();
                }

                var students = dbHelper.GetStudentsByClass(className) ?? new List<Student>();
                var studentIds = students.Select(s => s.Id).ToList();
                var classLogs = logs.Where(l => studentIds.Contains(l.StudentId)).ToList();

                var displayLogs = new List<LogDisplayItem>();

                foreach (var log in classLogs)
                {
                    displayLogs.Add(new LogDisplayItem
                    {
                        Date = log.Date,
                        UserName = log.UserName,
                        Action = log.Action,
                        ActionText = GetActionText(log.Action),
                        ActionColor = GetActionColor(log.Action),
                        StudentName = log.StudentName,
                        Subject = log.Subject,
                        Comment = log.Comment
                    });
                }

                lvLogs.ItemsSource = displayLogs.OrderByDescending(l => l.Date);
                txtTotal.Text = displayLogs.Count.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadLogs: {ex.Message}");
                lvLogs.ItemsSource = new List<LogDisplayItem>();
                txtTotal.Text = "0";

                // Если таблица не существует, показываем сообщение
                if (ex.Message.Contains("doesn't exist"))
                {
                    MessageBox.Show("Таблица журнала изменений не создана. Выполните SQL запрос для создания таблицы journal_log.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private string GetActionText(string action)
        {
            switch (action)
            {
                case "add_grade": return "➕ Добавлена оценка";
                case "edit_grade": return "✏ Изменена оценка";
                case "delete_grade": return "🗑 Удалена оценка";
                case "add_attendance": return "📋 Добавлена посещаемость";
                case "edit_attendance": return "✏ Изменена посещаемость";
                default: return action;
            }
        }

        private SolidColorBrush GetActionColor(string action)
        {
            switch (action)
            {
                case "add_grade": return new SolidColorBrush(Color.FromRgb(76, 175, 80));   // Зеленый
                case "edit_grade": return new SolidColorBrush(Color.FromRgb(33, 150, 243));  // Синий
                case "delete_grade": return new SolidColorBrush(Color.FromRgb(244, 67, 54));  // Красный
                case "add_attendance": return new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Оранжевый
                case "edit_attendance": return new SolidColorBrush(Color.FromRgb(156, 39, 176)); // Фиолетовый
                default: return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Серый
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadLogs();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
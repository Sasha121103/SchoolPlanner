using SchoolPlanner.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SchoolPlanner
{
    public partial class ScheduleArchiveWindow : Window
    {
        private DbHelper _dbHelper;
        private List<ScheduleArchiveViewModel> _archiveData;

        public class ScheduleArchiveViewModel
        {
            public int Number { get; set; }
            public int ScheduleId { get; set; }
            public string ScheduleName { get; set; }
            public string Period { get; set; }
            public string Status { get; set; }
            public int LessonCount { get; set; }
            public string CreatedAt { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public Schedule OriginalSchedule { get; set; }
        }

        public ScheduleArchiveWindow()
        {
            InitializeComponent();
            _dbHelper = new DbHelper();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var schedules = _dbHelper.GetAllSchedules();

                _archiveData = new List<ScheduleArchiveViewModel>();
                int number = 1;

                foreach (var schedule in schedules.OrderByDescending(s => s.StartDate))
                {
                    _archiveData.Add(new ScheduleArchiveViewModel
                    {
                        Number = number++,
                        ScheduleId = schedule.Id,
                        ScheduleName = schedule.Name,
                        Period = $"{schedule.StartDate:dd.MM.yyyy} - {schedule.EndDate:dd.MM.yyyy}",
                        Status = GetStatusText(schedule.Status),
                        LessonCount = schedule.Lessons?.Count ?? 0,
                        CreatedAt = schedule.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                        StartDate = schedule.StartDate,
                        EndDate = schedule.EndDate,
                        OriginalSchedule = schedule
                    });
                }

                dgScheduleArchive.ItemsSource = _archiveData;
                txtTotalSchedules.Text = _archiveData.Count.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки архива: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetStatusText(ScheduleStatus status)
        {
            switch (status)
            {
                case ScheduleStatus.Draft: return "Черновик";
                case ScheduleStatus.Pending: return "На проверке";
                case ScheduleStatus.Approved: return "Утверждено ✓";
                case ScheduleStatus.RequiresCorrection: return "Требует корректировки";
                default: return status.ToString();
            }
        }

        private void dgScheduleArchive_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgScheduleArchive.SelectedItem is ScheduleArchiveViewModel selected)
            {
                // Показываем детальную информацию о расписании
                string details = $"Название: {selected.ScheduleName}\n" +
                                 $"Период: {selected.Period}\n" +
                                 $"Статус: {selected.Status}\n" +
                                 $"Всего уроков: {selected.LessonCount}\n" +
                                 $"Дата создания: {selected.CreatedAt}\n\n" +
                                 $"Уроки по дням:\n";

                var lessonsByDay = selected.OriginalSchedule.Lessons
                    .GroupBy(l => l.DayOfWeek)
                    .ToDictionary(g => g.Key, g => g.Count());

                string[] days = { "Понедельник", "Вторник", "Среда", "Четверг", "Пятница" };
                foreach (var day in days)
                {
                    int count = lessonsByDay.ContainsKey(day) ? lessonsByDay[day] : 0;
                    details += $"{day}: {count} уроков\n";
                }

                MessageBox.Show(details, $"Детали расписания #{selected.ScheduleId}",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
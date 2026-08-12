using Microsoft.Win32;
using SchoolPlanner.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace SchoolPlanner
{
    public partial class TeacherLoadReportWindow : Window
    {
        private DbHelper _dbHelper;
        private List<TeacherLoadViewModel> _loadData;

        public class TeacherLoadViewModel
        {
            public int Number { get; set; }
            public int TeacherId { get; set; }
            public string TeacherName { get; set; }
            public string Subject { get; set; }
            public int TotalLessons { get; set; }
            public int MaxHours { get; set; }
            public int LoadPercent => MaxHours > 0 ? (TotalLessons * 100) / MaxHours : 0;
        }

        public TeacherLoadReportWindow()
        {
            InitializeComponent();
            _dbHelper = new DbHelper();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                // Получаем всех учителей
                var teachers = _dbHelper.GetAllTeachersWithDetails();
                // Получаем все расписания, чтобы посчитать уроки
                var allSchedules = _dbHelper.GetAllSchedules();

                _loadData = new List<TeacherLoadViewModel>();
                int number = 1;

                foreach (var teacher in teachers.OrderBy(t => t.FullName))
                {
                    int lessonCount = 0;
                    foreach (var schedule in allSchedules)
                    {
                        lessonCount += schedule.Lessons.Count(l => l.Teacher == teacher.FullName);
                    }

                    _loadData.Add(new TeacherLoadViewModel
                    {
                        Number = number++,
                        TeacherId = teacher.Id,
                        TeacherName = teacher.FullName,
                        Subject = teacher.Subject,
                        TotalLessons = lessonCount,
                        MaxHours = teacher.MaxHours
                    });
                }

                dgTeacherLoad.ItemsSource = _loadData;

                // Обновляем итоги
                txtTotalTeachers.Text = _loadData.Count.ToString();
                txtTotalLessons.Text = _loadData.Sum(t => t.TotalLessons).ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "CSV файлы (*.csv)|*.csv",
                    FileName = $"Нагрузка_учителей_{DateTime.Now:yyyyMMdd}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (StreamWriter sw = new StreamWriter(saveDialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        // Заголовок
                        sw.WriteLine("№;ФИО Учителя;Предмет;Всего уроков;Макс. часов;Загрузка %");

                        // Данные
                        foreach (var item in _loadData)
                        {
                            sw.WriteLine($"{item.Number};{item.TeacherName};{item.Subject};{item.TotalLessons};{item.MaxHours};{item.LoadPercent}");
                        }
                    }
                    MessageBox.Show("Отчет успешно экспортирован!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
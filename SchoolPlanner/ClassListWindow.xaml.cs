using Microsoft.Win32;
using SchoolPlanner.Database;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace SchoolPlanner
{
    public partial class ClassListWindow : Window
    {
        private DbHelper _dbHelper;
        private List<ClassInfoViewModel> _classData;

        public class ClassInfoViewModel
        {
            public int Number { get; set; }
            public string ClassName { get; set; }
            public int Grade { get; set; }
            public int StudentCount { get; set; }
            public int BoysCount { get; set; }
            public int GirlsCount { get; set; }
        }

        public ClassListWindow()
        {
            InitializeComponent();
            _dbHelper = new DbHelper();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                // Получаем общую статистику по классам из DbHelper
                var classStats = _dbHelper.GetClassStats();
                var allStudents = _dbHelper.GetAllStudents();
                var allClasses = _dbHelper.GetAllClasses();

                _classData = new List<ClassInfoViewModel>();
                int number = 1;

                // Проходим по всем классам из базы
                foreach (var cls in allClasses.OrderBy(c => c.Name))
                {
                    var stat = classStats.FirstOrDefault(s => s.ClassName == cls.Name);
                    int grade = GetGradeFromClassName(cls.Name);

                    _classData.Add(new ClassInfoViewModel
                    {
                        Number = number++,
                        ClassName = cls.Name,
                        Grade = grade,
                        StudentCount = stat?.StudentCount ?? 0,
                        BoysCount = stat?.BoysCount ?? 0,
                        GirlsCount = stat?.GirlsCount ?? 0
                    });
                }

                dgClassList.ItemsSource = _classData;

                // Обновляем итоги
                txtTotalClasses.Text = _classData.Count.ToString();
                txtTotalStudents.Text = _classData.Sum(c => c.StudentCount).ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int GetGradeFromClassName(string className)
        {
            if (className.StartsWith("5")) return 5;
            if (className.StartsWith("6")) return 6;
            if (className.StartsWith("7")) return 7;
            if (className.StartsWith("8")) return 8;
            if (className.StartsWith("9")) return 9;
            if (className.StartsWith("10")) return 10;
            if (className.StartsWith("11")) return 11;
            return 0;
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "CSV файлы (*.csv)|*.csv",
                    FileName = $"Список_классов_{DateTime.Now:yyyyMMdd}.csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    using (StreamWriter sw = new StreamWriter(saveDialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        sw.WriteLine("№;Название класса;Параллель;Кол-во учеников;Мальчиков;Девочек");

                        foreach (var item in _classData)
                        {
                            sw.WriteLine($"{item.Number};{item.ClassName};{item.Grade};{item.StudentCount};{item.BoysCount};{item.GirlsCount}");
                        }
                    }
                    MessageBox.Show("Список успешно экспортирован!", "Успех",
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
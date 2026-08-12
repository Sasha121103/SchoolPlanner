using Microsoft.VisualStudio.Services.Common;
using Microsoft.Win32;
using SchoolPlanner.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using static SchoolPlanner.Database.Attendance;
using static SchoolPlanner.Database.DbHelper;

namespace SchoolPlanner.Pages
{
    // Конвертер для видимости кнопки удаления
    public class GradeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int grade)
            {
                return grade > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class JournalWindow : Window
    {
        private DbHelper dbHelper;
        private string className;
        private List<Student> students;
        private List<Grade> allGrades;
        private List<Attendance> allAttendance;
        private string selectedSubject;
        private List<DateTime> dates;
        private User currentUser;
        private List<JournalStudent> currentJournal;
        private int selectedRowIndex = -1;
        private int selectedColIndex = -1;
        private bool isAdmin;
        private List<LogEntry> allLogs;
        private DateTime lastClickTime = DateTime.MinValue;
        private object lastClickedItem = null;
        private List<string> subjects;

        public class JournalStudent
        {
            public string StudentName { get; set; }
            public int StudentId { get; set; }
            public List<GradeCell> Grades { get; set; } = new List<GradeCell>();
            public string AverageGrade { get; set; }
            public string HasGrades { get; set; }
            public string ChangeInfo { get; set; }
            public string HasChanges { get; set; }
        }

        public class GradeCell
        {
            public int Grade { get; set; }
            public string GradeDisplay { get; set; }
            public SolidColorBrush Color { get; set; }
            public SolidColorBrush BorderColor { get; set; }
            public Thickness BorderThickness { get; set; }
            public string Tooltip { get; set; }
            public DateTime Date { get; set; }
            public int StudentId { get; set; }
            public string Subject { get; set; }
            public bool IsChanged { get; set; }
            public int RowIndex { get; set; }
            public int ColIndex { get; set; }
            public Grade OriginalGrade { get; set; }

            // Свойства для посещаемости
            public string AttendanceText { get; set; }
            public string AttendanceTooltip { get; set; }
        }

        public JournalWindow(string className, DbHelper dbHelper, string subjectName = null)
        {
            try
            {
                InitializeComponent();
                this.className = className;
                this.dbHelper = dbHelper;
                this.currentUser = App.CurrentUser;
                this.isAdmin = currentUser?.Role == UserRole.Admin;

                students = new List<Student>();
                allGrades = new List<Grade>();
                allAttendance = new List<Attendance>();
                dates = new List<DateTime>();
                currentJournal = new List<JournalStudent>();
                allLogs = new List<LogEntry>();
                subjects = new List<string>();

                txtClassName.Text = $"Класс {className}";

                // Если передан предмет, сохраняем его
                if (!string.IsNullOrEmpty(subjectName))
                {
                    selectedSubject = subjectName;
                }

                teacherButtons.Visibility = Visibility.Visible;
                adminButtons.Visibility = Visibility.Collapsed;

                if (isAdmin)
                {
                    Button btnViewLogs = new Button
                    {
                        Content = "📋 История изменений",
                        Style = (Style)FindResource("MainButtonStyle"),
                        Background = new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                        Padding = new Thickness(14, 8, 14, 8),
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    btnViewLogs.Click += btnViewLogs_Click;
                    teacherButtons.Children.Add(btnViewLogs);

                    txtStatusInfo.Text = "👑 УВР - Двойной клик - редактировать | ПКМ - удалить";
                }
                else
                {
                    txtStatusInfo.Text = "📝 Двойной клик - редактировать | ПКМ - удалить | ↑↓←→ - навигация";
                }

                LoadSubjects();
                LoadJournal();

                Title = $"Электронный журнал - {className} класс";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSubjects()
        {
            try
            {
                var allSubjects = dbHelper.GetAllSubjects() ?? new List<Subject>();
                subjects = allSubjects.Select(s => s.Name).OrderBy(s => s).ToList();

                cmbSubject.Items.Clear();
                cmbSubject.Items.Add("Все предметы");
                foreach (var s in subjects)
                {
                    cmbSubject.Items.Add(s);
                }

                // Если выбран конкретный предмет, выбираем его в комбобоксе
                if (!string.IsNullOrEmpty(selectedSubject) && selectedSubject != "Все предметы")
                {
                    int index = cmbSubject.Items.IndexOf(selectedSubject);
                    if (index >= 0)
                    {
                        cmbSubject.SelectedIndex = index;
                    }
                    else
                    {
                        cmbSubject.SelectedIndex = 1;
                    }
                }
                else if (cmbSubject.Items.Count > 1)
                {
                    cmbSubject.SelectedIndex = 1;
                }
                else
                {
                    cmbSubject.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadSubjects: {ex.Message}");
                cmbSubject.Items.Clear();
                cmbSubject.Items.Add("Все предметы");
                cmbSubject.SelectedIndex = 0;
            }
        }

        private async void LoadJournal()
        {
            try
            {
                students = dbHelper.GetStudentsByClass(className) ?? new List<Student>();
                txtStudentCount.Text = $"{students.Count} уч.";

                if (!students.Any())
                {
                    MessageBox.Show($"В классе {className} нет учеников", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    lvJournal.ItemsSource = new List<JournalStudent>();
                    return;
                }

                allGrades = new List<Grade>();
                allAttendance = new List<Attendance>();
                allLogs = new List<LogEntry>();

                foreach (var student in students)
                {
                    var grades = dbHelper.GetGradesForStudent(student.Id) ?? new List<Grade>();
                    var attendance = dbHelper.GetAttendanceForStudent(student.Id) ?? new List<Attendance>();
                    var logs = dbHelper.GetLogsForStudent(student.Id);

                    allGrades.AddRange(grades);
                    allAttendance.AddRange(attendance);

                    if (logs != null && logs.Any())
                    {
                        allLogs.AddRange(logs);
                    }
                }

                selectedSubject = cmbSubject?.SelectedItem?.ToString() ?? "Все предметы";

                // Обновляем название предмета в заголовке
                if (!string.IsNullOrEmpty(selectedSubject) && selectedSubject != "Все предметы")
                {
                    txtSubjectName.Text = selectedSubject;
                    txtSubjectName.Visibility = Visibility.Visible;
                }
                else
                {
                    txtSubjectName.Visibility = Visibility.Collapsed;
                }

                // Формируем даты
                var allDates = new List<DateTime>();

                var filteredGrades = allGrades;
                if (selectedSubject != "Все предметы")
                {
                    filteredGrades = allGrades.Where(g => g.Subject == selectedSubject).ToList();
                }
                allDates.AddRange(filteredGrades.Select(g => g.Date));
                allDates.AddRange(allAttendance.Select(a => a.Date));

                dates = allDates.Distinct().OrderBy(d => d).ToList();

                if (!dates.Any())
                {
                    dates.Add(DateTime.Now.Date);
                }

                BuildJournal();
                UpdateStatistics();
                UpdateStatusInfo();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка LoadJournal: {ex.Message}");
                MessageBox.Show($"Ошибка загрузки журнала: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                lvJournal.ItemsSource = new List<JournalStudent>();
            }
        }

        private async System.Threading.Tasks.Task LoadJournalAsync()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    LoadJournal();
                });
            });
        }

        private void BuildJournal()
        {
            try
            {
                if (students == null) students = new List<Student>();
                if (dates == null || !dates.Any()) dates = new List<DateTime> { DateTime.Now.Date };

                selectedSubject = cmbSubject?.SelectedItem?.ToString() ?? "Все предметы";

                // Очищаем заголовки
                spDateHeaders.Children.Clear();

                // Добавляем заголовки дат
                foreach (var date in dates)
                {
                    var border = new Border
                    {
                        Style = (Style)FindResource("HeaderCellStyle"),
                        Margin = new Thickness(2, 0, 2, 0),
                        MinWidth = 40
                    };

                    var stackPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    stackPanel.Children.Add(new TextBlock
                    {
                        Text = date.ToString("dd"),
                        Foreground = new SolidColorBrush(Colors.White),
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    stackPanel.Children.Add(new TextBlock
                    {
                        Text = date.ToString("MMM"),
                        Foreground = new SolidColorBrush(Color.FromRgb(200, 230, 255)),
                        FontSize = 10,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });

                    border.Child = stackPanel;
                    spDateHeaders.Children.Add(border);
                }

                var journal = new List<JournalStudent>();
                int totalChanges = 0;

                int rowIndex = 0;
                foreach (var student in students)
                {
                    if (student == null) continue;

                    var journalStudent = new JournalStudent
                    {
                        StudentName = student.FullName ?? "Неизвестно",
                        StudentId = student.Id,
                        Grades = new List<GradeCell>()
                    };

                    // Получаем оценки ученика
                    var grades = allGrades?.Where(g => g.StudentId == student.Id).ToList() ?? new List<Grade>();

                    // Фильтруем по предмету
                    if (selectedSubject != "Все предметы")
                    {
                        grades = grades.Where(g => g.Subject == selectedSubject).ToList();
                    }

                    // Создаем словари для быстрого доступа
                    var gradeDict = new Dictionary<DateTime, int>();
                    var gradeDateDict = new Dictionary<DateTime, Grade>();
                    foreach (var grade in grades)
                    {
                        if (!gradeDict.ContainsKey(grade.Date))
                        {
                            gradeDict.Add(grade.Date, grade.Value);
                            gradeDateDict.Add(grade.Date, grade);
                        }
                    }

                    // Получаем посещаемость для ученика
                    var attendances = allAttendance?.Where(a => a.StudentId == student.Id).ToList() ?? new List<Attendance>();
                    var attendanceDict = new Dictionary<DateTime, Attendance>();
                    foreach (var att in attendances)
                    {
                        if (!attendanceDict.ContainsKey(att.Date))
                        {
                            attendanceDict.Add(att.Date, att);
                        }
                    }

                    int studentChanges = 0;
                    int colIndex = 0;

                    foreach (var date in dates)
                    {
                        bool isGradeChanged = false;
                        var gradeLogs = dbHelper.GetLogsForStudent(student.Id);
                        var recentGradeLog = gradeLogs
                            .Where(l => l.StudentId == student.Id &&
                                l.Date.Date == date &&
                                (l.Action == "edit_grade" || l.Action == "add_grade" || l.Action == "delete_grade"))
                            .OrderByDescending(l => l.Date)
                            .FirstOrDefault();

                        GradeCell gradeCell;

                        if (gradeDict.TryGetValue(date, out int gradeValue))
                        {
                            var gradeObj = gradeDateDict[date];

                            if (recentGradeLog != null && recentGradeLog.NewValue == gradeValue.ToString())
                            {
                                isGradeChanged = true;
                                studentChanges++;
                                totalChanges++;
                            }

                            gradeCell = new GradeCell
                            {
                                Grade = gradeValue,
                                GradeDisplay = gradeValue.ToString(),
                                Color = GetGradeColor(gradeValue),
                                BorderColor = isGradeChanged ? new SolidColorBrush(Color.FromRgb(255, 193, 7)) : new SolidColorBrush(Colors.Transparent),
                                BorderThickness = isGradeChanged ? new Thickness(2) : new Thickness(0),
                                Tooltip = $"{date:dd.MM.yyyy} - {gradeValue}" + (isGradeChanged ? " ⚠️ Изменено" : ""),
                                Date = date,
                                StudentId = student.Id,
                                Subject = gradeObj.Subject,
                                IsChanged = isGradeChanged,
                                RowIndex = rowIndex,
                                ColIndex = colIndex,
                                OriginalGrade = gradeObj
                            };
                        }
                        else
                        {
                            if (recentGradeLog != null && recentGradeLog.Action == "delete_grade")
                            {
                                isGradeChanged = true;
                                studentChanges++;
                                totalChanges++;
                            }

                            gradeCell = new GradeCell
                            {
                                Grade = 0,
                                GradeDisplay = "",
                                Color = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                                BorderColor = isGradeChanged ? new SolidColorBrush(Color.FromRgb(255, 193, 7)) : new SolidColorBrush(Colors.Transparent),
                                BorderThickness = isGradeChanged ? new Thickness(2) : new Thickness(0),
                                Tooltip = "Нет оценки" + (isGradeChanged ? " ⚠️ Удалена" : ""),
                                Date = date,
                                StudentId = student.Id,
                                Subject = selectedSubject != "Все предметы" ? selectedSubject : "",
                                IsChanged = isGradeChanged,
                                RowIndex = rowIndex,
                                ColIndex = colIndex,
                                OriginalGrade = null
                            };
                        }

                        // Заполняем данные по посещаемости
                        if (attendanceDict.TryGetValue(date, out Attendance att))
                        {
                            gradeCell.AttendanceText = GetAttendanceStatusIcon(att.Status);
                            gradeCell.AttendanceTooltip = $"{date:dd.MM.yyyy} - {GetAttendanceStatusText(att.Status)}";
                        }
                        else
                        {
                            gradeCell.AttendanceText = "?";
                            gradeCell.AttendanceTooltip = $"{date:dd.MM.yyyy} - Не отмечено";
                        }

                        journalStudent.Grades.Add(gradeCell);
                        colIndex++;
                    }

                    // Статистика по ученику
                    var studentGrades = grades.Where(g => g.StudentId == student.Id).ToList();
                    if (studentGrades.Any())
                    {
                        double avg = studentGrades.Average(g => g.Value);
                        journalStudent.AverageGrade = $"⭐ {avg:F2}";
                        journalStudent.HasGrades = "Visible";
                    }
                    else
                    {
                        journalStudent.AverageGrade = "";
                        journalStudent.HasGrades = "Collapsed";
                    }

                    if (studentChanges > 0)
                    {
                        journalStudent.ChangeInfo = $"⚠️ {studentChanges} изменений";
                        journalStudent.HasChanges = "Visible";
                    }
                    else
                    {
                        journalStudent.ChangeInfo = "";
                        journalStudent.HasChanges = "Collapsed";
                    }

                    journal.Add(journalStudent);
                    rowIndex++;
                }

                currentJournal = journal;
                lvJournal.ItemsSource = journal;
                txtChanges.Text = totalChanges.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка BuildJournal: {ex.Message}");
                lvJournal.ItemsSource = new List<JournalStudent>();
            }
        }

        private SolidColorBrush GetGradeColor(int grade)
        {
            if (grade >= 5) return new SolidColorBrush(Color.FromRgb(76, 175, 80));
            if (grade >= 4) return new SolidColorBrush(Color.FromRgb(33, 150, 243));
            if (grade >= 3) return new SolidColorBrush(Color.FromRgb(255, 152, 0));
            if (grade >= 2) return new SolidColorBrush(Color.FromRgb(244, 67, 54));
            return new SolidColorBrush(Color.FromRgb(245, 245, 245));
        }

        private string GetAttendanceStatusIcon(AttendanceStatus status)
        {
            switch (status)
            {
                case AttendanceStatus.Present: return "✅";
                case AttendanceStatus.Absent: return "❌";
                case AttendanceStatus.Late: return "⏰";
                case AttendanceStatus.Excused: return "📝";
                default: return "?";
            }
        }

        private string GetAttendanceStatusText(AttendanceStatus status)
        {
            switch (status)
            {
                case AttendanceStatus.Present: return "Присутствовал";
                case AttendanceStatus.Absent: return "Отсутствовал";
                case AttendanceStatus.Late: return "Опоздал";
                case AttendanceStatus.Excused: return "Уважительная причина";
                default: return "Не отмечено";
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                // Статистика по оценкам для выбранного предмета
                if (allGrades != null && allGrades.Any())
                {
                    var filtered = allGrades;
                    if (selectedSubject != "Все предметы")
                    {
                        filtered = filtered.Where(g => g.Subject == selectedSubject).ToList();
                    }

                    if (filtered.Any())
                    {
                        double avg = filtered.Average(g => g.Value);
                        txtAvgGrade.Text = avg.ToString("F2");
                        txtTotalGrades.Text = filtered.Count.ToString();
                    }
                    else
                    {
                        txtAvgGrade.Text = "—";
                        txtTotalGrades.Text = "0";
                    }
                }
                else
                {
                    txtAvgGrade.Text = "—";
                    txtTotalGrades.Text = "0";
                }

                // Статистика по посещаемости (не зависит от предмета)
                if (allAttendance != null && allAttendance.Any())
                {
                    int present = allAttendance.Count(a => a.Status == AttendanceStatus.Present);
                    int total = allAttendance.Count;
                    int percent = total > 0 ? (present * 100 / total) : 0;
                    txtAttendance.Text = $"{percent}% ({present}/{total})";
                }
                else
                {
                    txtAttendance.Text = "—";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка UpdateStatistics: {ex.Message}");
            }
        }

        private void UpdateStatusInfo()
        {
            try
            {
                if (isAdmin)
                {
                    txtStatusInfo.Text = "👑 УВР - Двойной клик - редактировать | ПКМ - удалить";
                }
                else
                {
                    txtStatusInfo.Text = "📝 Двойной клик - редактировать | ПКМ - удалить | ↑↓←→ - навигация";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка UpdateStatusInfo: {ex.Message}");
            }
        }

        private void cmbSubject_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Получаем выбранный предмет
                selectedSubject = cmbSubject?.SelectedItem?.ToString() ?? "Все предметы";

                // Обновляем название предмета в заголовке
                if (!string.IsNullOrEmpty(selectedSubject) && selectedSubject != "Все предметы")
                {
                    txtSubjectName.Text = selectedSubject;
                    txtSubjectName.Visibility = Visibility.Visible;
                }
                else
                {
                    txtSubjectName.Visibility = Visibility.Collapsed;
                }

                // Перестраиваем даты для нового предмета
                var allDates = new List<DateTime>();

                // Фильтруем оценки по выбранному предмету
                var filteredGrades = allGrades;
                if (selectedSubject != "Все предметы")
                {
                    filteredGrades = allGrades.Where(g => g.Subject == selectedSubject).ToList();
                }
                allDates.AddRange(filteredGrades.Select(g => g.Date));
                allDates.AddRange(allAttendance.Select(a => a.Date));

                dates = allDates.Distinct().OrderBy(d => d).ToList();

                if (!dates.Any())
                {
                    dates.Add(DateTime.Now.Date);
                }

                // Перестраиваем журнал
                BuildJournal();
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка cmbSubject_SelectionChanged: {ex.Message}");
            }
        }

        private void cmbPeriod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterByPeriod();
        }

        private void FilterByPeriod()
        {
            try
            {
                var period = cmbPeriod.SelectedItem?.ToString() ?? "Все даты";
                var now = DateTime.Now;

                var allDates = new List<DateTime>();
                allDates.AddRange(allGrades.Select(g => g.Date));
                allDates.AddRange(allAttendance.Select(a => a.Date));

                if (period.Contains("Текущая неделя"))
                {
                    var startOfWeek = now.AddDays(-(int)now.DayOfWeek + 1);
                    dates = allDates
                        .Where(d => d >= startOfWeek && d <= startOfWeek.AddDays(6))
                        .Distinct()
                        .OrderBy(d => d)
                        .ToList();
                }
                else if (period.Contains("Прошлая неделя"))
                {
                    var startOfWeek = now.AddDays(-(int)now.DayOfWeek + 1 - 7);
                    dates = allDates
                        .Where(d => d >= startOfWeek && d <= startOfWeek.AddDays(6))
                        .Distinct()
                        .OrderBy(d => d)
                        .ToList();
                }
                else if (period.Contains("Этот месяц"))
                {
                    var startOfMonth = new DateTime(now.Year, now.Month, 1);
                    dates = allDates
                        .Where(d => d >= startOfMonth && d <= now)
                        .Distinct()
                        .OrderBy(d => d)
                        .ToList();
                }
                else
                {
                    dates = allDates.Distinct().OrderBy(d => d).ToList();
                }

                if (!dates.Any())
                {
                    dates.Add(DateTime.Now.Date);
                }

                BuildJournal();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка FilterByPeriod: {ex.Message}");
            }
        }

        // ============================================================
        // РАБОТА С ОЦЕНКАМИ
        // ============================================================

        private async void SetGrade(int rowIndex, int colIndex, int grade)
        {
            try
            {
                if (rowIndex < 0 || rowIndex >= currentJournal.Count ||
                    colIndex < 0 || colIndex >= currentJournal[rowIndex].Grades.Count)
                {
                    MessageBox.Show("Ошибка: ячейка не найдена", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var cell = currentJournal[rowIndex].Grades[colIndex];
                var student = currentJournal[rowIndex];
                var date = dates[colIndex];

                string subject = selectedSubject;
                if (subject == "Все предметы" || string.IsNullOrEmpty(subject))
                {
                    if (!string.IsNullOrEmpty(cell.Subject))
                    {
                        subject = cell.Subject;
                    }
                    else
                    {
                        subject = "Основной";
                    }
                }

                var studentId = student.StudentId;

                var existingGrade = allGrades.FirstOrDefault(g =>
                    g.StudentId == studentId &&
                    g.Date == date &&
                    g.Subject == subject);

                string oldValue = existingGrade?.Value.ToString() ?? "";
                string newValue = grade > 0 ? grade.ToString() : "";

                bool success = false;

                if (grade == 0)
                {
                    if (existingGrade != null)
                    {
                        success = dbHelper.DeleteGrade(existingGrade.Id);
                        if (success)
                        {
                            SaveLog(studentId, student.StudentName, "delete_grade", subject, oldValue, "",
                                $"Удалена оценка {oldValue} по предмету {subject}");
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    var newGrade = new Grade
                    {
                        StudentId = studentId,
                        Subject = subject,
                        Value = grade,
                        Date = date,
                        Comment = "Введено через интерфейс"
                    };

                    if (existingGrade != null)
                    {
                        newGrade.Id = existingGrade.Id;
                        success = dbHelper.SaveGrade(newGrade);
                        if (success)
                        {
                            SaveLog(studentId, student.StudentName, "edit_grade", subject, oldValue, newValue,
                                $"Изменена оценка {oldValue} → {newValue} по предмету {subject}");
                        }
                    }
                    else
                    {
                        success = dbHelper.SaveGrade(newGrade);
                        if (success)
                        {
                            SaveLog(studentId, student.StudentName, "add_grade", subject, "", newValue,
                                $"Добавлена оценка {newValue} по предмету {subject}");
                        }
                    }
                }

                if (success)
                {
                    await LoadJournalAsync();
                }
                else
                {
                    MessageBox.Show("Ошибка сохранения оценки", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка SetGrade: {ex.Message}");
                MessageBox.Show($"Ошибка сохранения оценки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveLog(int studentId, string studentName, string action, string subject,
            string oldValue, string newValue, string comment)
        {
            try
            {
                var log = new LogEntry
                {
                    UserId = currentUser.Id,
                    UserName = currentUser.FullName,
                    Action = action,
                    StudentId = studentId,
                    StudentName = studentName,
                    Subject = subject,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Date = DateTime.Now,
                    Comment = comment
                };

                bool success = dbHelper.SaveLog(log);
                if (success)
                {
                    System.Diagnostics.Debug.WriteLine($"Лог сохранен: {action} - {studentName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка SaveLog: {ex.Message}");
            }
        }

        private void OpenGradeInputDialog(int rowIndex, int colIndex)
        {
            try
            {
                if (rowIndex < 0 || rowIndex >= currentJournal.Count ||
                    colIndex < 0 || colIndex >= currentJournal[rowIndex].Grades.Count)
                    return;

                var cell = currentJournal[rowIndex].Grades[colIndex];
                var student = currentJournal[rowIndex];

                var dialog = new Window
                {
                    Title = "✏ Ввод оценки",
                    Width = 350,
                    Height = 250,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    ResizeMode = ResizeMode.NoResize
                };

                var panel = new StackPanel { Margin = new Thickness(20) };

                panel.Children.Add(new TextBlock
                {
                    Text = $"Ученик: {student.StudentName}",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    Margin = new Thickness(0, 0, 0, 10)
                });

                panel.Children.Add(new TextBlock
                {
                    Text = $"Дата: {cell.Date:dd.MM.yyyy}",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    Margin = new Thickness(0, 0, 0, 10)
                });

                panel.Children.Add(new TextBlock
                {
                    Text = $"Предмет: {cell.Subject}",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    Margin = new Thickness(0, 0, 0, 15)
                });

                panel.Children.Add(new TextBlock
                {
                    Text = "Оценка (2-5):",
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    Margin = new Thickness(0, 0, 0, 5)
                });

                var txtGrade = new TextBox
                {
                    Text = cell.Grade > 0 ? cell.Grade.ToString() : "",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Height = 40,
                    Margin = new Thickness(0, 0, 0, 15),
                    MaxLength = 1
                };
                panel.Children.Add(txtGrade);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                Button btnSave = new Button
                {
                    Content = "Сохранить",
                    Style = (Style)FindResource("MainButtonStyle"),
                    Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    Padding = new Thickness(20, 8, 20, 8),
                    Margin = new Thickness(0, 0, 10, 0),
                    Width = 100
                };
                btnSave.Click += (s, args) =>
                {
                    if (int.TryParse(txtGrade.Text, out int grade) && grade >= 2 && grade <= 5)
                    {
                        SetGrade(rowIndex, colIndex, grade);
                        dialog.Close();
                    }
                    else if (string.IsNullOrEmpty(txtGrade.Text))
                    {
                        SetGrade(rowIndex, colIndex, 0);
                        dialog.Close();
                    }
                    else
                    {
                        MessageBox.Show("Введите оценку от 2 до 5", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                txtGrade.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // ОБРАБОТЧИКИ ДЛЯ ПОСЕЩАЕМОСТИ
        // ============================================================

        private void AttendanceCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var textBlock = sender as TextBlock;
                var cell = textBlock?.Tag as GradeCell;
                if (cell == null) return;

                OpenAttendanceEditDialog(cell.RowIndex, cell.ColIndex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка AttendanceCell_MouseLeftButtonDown: {ex.Message}");
            }
        }

        private void OpenAttendanceEditDialog(int rowIndex, int colIndex)
        {
            try
            {
                if (rowIndex < 0 || rowIndex >= currentJournal.Count ||
                    colIndex < 0 || colIndex >= currentJournal[rowIndex].Grades.Count)
                    return;

                var student = currentJournal[rowIndex];
                var date = dates[colIndex];
                var studentId = student.StudentId;

                var existingAttendance = allAttendance.FirstOrDefault(a =>
                    a.StudentId == studentId &&
                    a.Date == date);

                var dialog = new Window
                {
                    Title = "📋 Отметка посещаемости",
                    Width = 350,
                    Height = 300,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    ResizeMode = ResizeMode.NoResize
                };

                var panel = new StackPanel { Margin = new Thickness(20) };

                panel.Children.Add(new TextBlock
                {
                    Text = $"Ученик: {student.StudentName}",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    Margin = new Thickness(0, 0, 0, 10)
                });

                panel.Children.Add(new TextBlock
                {
                    Text = $"Дата: {date:dd.MM.yyyy}",
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    Margin = new Thickness(0, 0, 0, 15)
                });

                panel.Children.Add(new TextBlock
                {
                    Text = "Статус посещаемости:",
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    Margin = new Thickness(0, 0, 0, 5)
                });

                var cmbStatus = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10)
                };
                cmbStatus.Items.Add(new ComboBoxItem { Content = "✅ Присутствовал", Tag = AttendanceStatus.Present });
                cmbStatus.Items.Add(new ComboBoxItem { Content = "❌ Отсутствовал", Tag = AttendanceStatus.Absent });
                cmbStatus.Items.Add(new ComboBoxItem { Content = "⏰ Опоздал", Tag = AttendanceStatus.Late });
                cmbStatus.Items.Add(new ComboBoxItem { Content = "📝 Уважительная причина", Tag = AttendanceStatus.Excused });

                if (existingAttendance != null)
                {
                    foreach (ComboBoxItem item in cmbStatus.Items)
                    {
                        if ((AttendanceStatus)item.Tag == existingAttendance.Status)
                        {
                            cmbStatus.SelectedItem = item;
                            break;
                        }
                    }
                }
                else
                {
                    cmbStatus.SelectedIndex = 0;
                }
                panel.Children.Add(cmbStatus);

                panel.Children.Add(new TextBlock
                {
                    Text = "Примечание:",
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    Margin = new Thickness(0, 0, 0, 5)
                });

                var txtNote = new TextBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10),
                    Text = existingAttendance?.Note ?? ""
                };
                panel.Children.Add(txtNote);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                Button btnSave = new Button
                {
                    Content = "Сохранить",
                    Style = (Style)FindResource("MainButtonStyle"),
                    Background = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    Padding = new Thickness(20, 8, 20, 8),
                    Margin = new Thickness(0, 0, 10, 0),
                    Width = 100
                };
                btnSave.Click += (s, args) =>
                {
                    try
                    {
                        var status = (AttendanceStatus)((ComboBoxItem)cmbStatus.SelectedItem).Tag;

                        var newAttendance = new Attendance
                        {
                            StudentId = studentId,
                            Date = date,
                            Status = status,
                            Note = txtNote.Text
                        };

                        if (existingAttendance != null)
                            newAttendance.Id = existingAttendance.Id;

                        bool success = dbHelper.SaveAttendance(newAttendance);
                        if (success)
                        {
                            string oldValue = existingAttendance?.Status.ToString() ?? "";
                            string newValue = status.ToString();
                            string action = existingAttendance != null ? "edit_attendance" : "add_attendance";
                            SaveLog(studentId, student.StudentName, action, "Посещаемость", oldValue, newValue,
                                existingAttendance != null ? $"Изменена посещаемость {oldValue} → {newValue}" : $"Добавлена посещаемость {newValue}");

                            dialog.Close();
                            LoadJournal();
                            MessageBox.Show("Посещаемость сохранена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // ОБРАБОТЧИКИ МЫШИ
        // ============================================================

        private void GradeCell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var border = sender as Border;
                var cell = border?.Tag as GradeCell;
                if (cell == null) return;

                if (lastClickedItem == cell && (DateTime.Now - lastClickTime).TotalMilliseconds < 500)
                {
                    OpenGradeInputDialog(cell.RowIndex, cell.ColIndex);
                    lastClickedItem = null;
                }
                else
                {
                    lastClickedItem = cell;
                    lastClickTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GradeCell_MouseLeftButtonDown: {ex.Message}");
            }
        }

        private void GradeCell_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var border = sender as Border;
                var cell = border?.Tag as GradeCell;
                if (cell == null) return;

                var contextMenu = new ContextMenu();

                var menuItemSetGrade = new MenuItem
                {
                    Header = "✏ Поставить оценку",
                    Icon = new TextBlock { Text = "📝", FontSize = 16 }
                };
                menuItemSetGrade.Click += (s, args) =>
                {
                    OpenGradeInputDialog(cell.RowIndex, cell.ColIndex);
                };
                contextMenu.Items.Add(menuItemSetGrade);

                contextMenu.Items.Add(new Separator());

                for (int i = 2; i <= 5; i++)
                {
                    int gradeValue = i;
                    var menuItem = new MenuItem
                    {
                        Header = gradeValue.ToString(),
                        Icon = new Border
                        {
                            Background = GetGradeColor(gradeValue),
                            Width = 20,
                            Height = 20,
                            CornerRadius = new CornerRadius(4),
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    };
                    menuItem.Click += (s, args) =>
                    {
                        SetGrade(cell.RowIndex, cell.ColIndex, gradeValue);
                    };
                    contextMenu.Items.Add(menuItem);
                }

                if (cell.Grade > 0)
                {
                    contextMenu.Items.Add(new Separator());

                    var menuItemDelete = new MenuItem
                    {
                        Header = "🗑 Удалить",
                        Icon = new TextBlock { Text = "❌", FontSize = 16 }
                    };
                    menuItemDelete.Click += (s, args) =>
                    {
                        if (MessageBox.Show($"Удалить оценку {cell.Grade} по предмету {cell.Subject}?", "Подтверждение",
                            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            SetGrade(cell.RowIndex, cell.ColIndex, 0);
                        }
                    };
                    contextMenu.Items.Add(menuItemDelete);
                }

                border.ContextMenu = contextMenu;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GradeCell_MouseRightButtonUp: {ex.Message}");
            }
        }

        private void btnDeleteGrade_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                var cell = button?.Tag as GradeCell;
                if (cell == null) return;

                if (MessageBox.Show($"Удалить оценку {cell.Grade} по предмету {cell.Subject}?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    SetGrade(cell.RowIndex, cell.ColIndex, 0);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void lvJournal_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var listView = sender as ListView;
                if (listView?.SelectedItem is JournalStudent selectedStudent)
                {
                    selectedRowIndex = currentJournal.IndexOf(selectedStudent);
                    selectedColIndex = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка MouseClick: {ex.Message}");
            }
        }

        // ============================================================
        // КНОПКИ ДЕЙСТВИЙ
        // ============================================================

        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadJournal();
        }

        private void btnAddGrade_Click(object sender, RoutedEventArgs e)
        {
            if (students == null || !students.Any())
            {
                MessageBox.Show("В классе нет учеников", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OpenAddGradeDialog();
        }

        private void OpenAddGradeDialog()
        {
            try
            {
                var dialog = new Window
                {
                    Title = "➕ Добавление оценки",
                    Width = 400,
                    Height = 420,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
                };

                var panel = new StackPanel { Margin = new Thickness(20, 20, 20, 20) };

                panel.Children.Add(new TextBlock
                {
                    Text = "Добавление оценки",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    Margin = new Thickness(0, 0, 0, 20)
                });

                panel.Children.Add(new TextBlock { Text = "Ученик:*", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var cmbStudent = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10, 10, 10, 10)
                };
                foreach (var s in students.OrderBy(s => s.FullName))
                {
                    cmbStudent.Items.Add(s.FullName);
                }
                cmbStudent.SelectedIndex = 0;
                panel.Children.Add(cmbStudent);

                panel.Children.Add(new TextBlock { Text = "Предмет:*", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var cmbSubject = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10, 10, 10, 10),
                    IsEditable = true
                };
                var subjects = dbHelper.GetAllSubjects() ?? new List<Subject>();
                foreach (var s in subjects)
                    cmbSubject.Items.Add(s.Name);
                if (cmbSubject.Items.Count > 0) cmbSubject.SelectedIndex = 0;
                panel.Children.Add(cmbSubject);

                panel.Children.Add(new TextBlock { Text = "Оценка (2-5):*", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var cmbGrade = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10, 10, 10, 10)
                };
                for (int i = 2; i <= 5; i++)
                    cmbGrade.Items.Add(i);
                cmbGrade.SelectedIndex = 0;
                panel.Children.Add(cmbGrade);

                panel.Children.Add(new TextBlock { Text = "Дата:", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var dpDate = new DatePicker
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    SelectedDate = DateTime.Now
                };
                panel.Children.Add(dpDate);

                panel.Children.Add(new TextBlock { Text = "Комментарий:", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var txtComment = new TextBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10, 10, 10, 10)
                };
                panel.Children.Add(txtComment);

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
                        if (cmbStudent.SelectedItem == null)
                        {
                            MessageBox.Show("Выберите ученика", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        if (string.IsNullOrEmpty(cmbSubject.Text))
                        {
                            MessageBox.Show("Введите предмет", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var studentName = cmbStudent.SelectedItem.ToString();
                        var student = students.FirstOrDefault(st => st.FullName == studentName);
                        if (student == null)
                        {
                            MessageBox.Show("Ученик не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        var grade = new Grade
                        {
                            StudentId = student.Id,
                            Subject = cmbSubject.Text,
                            Value = (int)cmbGrade.SelectedItem,
                            Date = dpDate.SelectedDate ?? DateTime.Now,
                            Comment = txtComment.Text
                        };

                        if (dbHelper.SaveGrade(grade))
                        {
                            SaveLog(student.Id, student.FullName, "add_grade", cmbSubject.Text, "", grade.Value.ToString(),
                                $"Добавлена оценка {grade.Value}");
                            dialog.Close();
                            LoadJournal();
                            MessageBox.Show("Оценка добавлена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnAddAttendance_Click(object sender, RoutedEventArgs e)
        {
            if (students == null || !students.Any())
            {
                MessageBox.Show("В классе нет учеников", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OpenAddAttendanceDialog();
        }

        private void OpenAddAttendanceDialog()
        {
            try
            {
                var dialog = new Window
                {
                    Title = "📋 Отметка посещаемости",
                    Width = 500,
                    Height = 550,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
                };

                var panel = new StackPanel { Margin = new Thickness(20, 20, 20, 20) };

                panel.Children.Add(new TextBlock
                {
                    Text = "Отметка посещаемости",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    Margin = new Thickness(0, 0, 0, 20)
                });

                panel.Children.Add(new TextBlock { Text = "Дата:", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var dpDate = new DatePicker
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    SelectedDate = DateTime.Now
                };
                panel.Children.Add(dpDate);

                panel.Children.Add(new TextBlock { Text = "Ученики:", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });

                var scrollViewer = new ScrollViewer { MaxHeight = 280, Margin = new Thickness(0, 0, 0, 15) };
                var studentPanel = new StackPanel();

                var checkBoxes = new List<CheckBox>();
                var statusComboBoxes = new List<ComboBox>();

                foreach (var student in students.OrderBy(s => s.FullName))
                {
                    var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };

                    var checkBox = new CheckBox
                    {
                        Content = student.FullName,
                        Width = 200,
                        IsChecked = true,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    rowPanel.Children.Add(checkBox);
                    checkBoxes.Add(checkBox);

                    var statusCombo = new ComboBox
                    {
                        Width = 140,
                        Margin = new Thickness(10, 0, 0, 0),
                        Padding = new Thickness(8, 4, 8, 4)
                    };
                    statusCombo.Items.Add(new ComboBoxItem { Content = "✅ Присутствовал", Tag = AttendanceStatus.Present });
                    statusCombo.Items.Add(new ComboBoxItem { Content = "❌ Отсутствовал", Tag = AttendanceStatus.Absent });
                    statusCombo.Items.Add(new ComboBoxItem { Content = "⏰ Опоздал", Tag = AttendanceStatus.Late });
                    statusCombo.Items.Add(new ComboBoxItem { Content = "📝 Уважительная", Tag = AttendanceStatus.Excused });
                    statusCombo.SelectedIndex = 0;
                    rowPanel.Children.Add(statusCombo);
                    statusComboBoxes.Add(statusCombo);

                    studentPanel.Children.Add(rowPanel);
                }

                scrollViewer.Content = studentPanel;
                panel.Children.Add(scrollViewer);

                var quickPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };

                Button btnAllPresent = new Button
                {
                    Content = "✅ Все присутствуют",
                    Style = (Style)FindResource("SmallButtonStyle"),
                    Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    Margin = new Thickness(0, 0, 8, 0),
                    Padding = new Thickness(12, 5, 12, 5)
                };
                btnAllPresent.Click += (s, args) =>
                {
                    for (int i = 0; i < checkBoxes.Count; i++)
                    {
                        checkBoxes[i].IsChecked = true;
                        statusComboBoxes[i].SelectedIndex = 0;
                    }
                };
                quickPanel.Children.Add(btnAllPresent);

                Button btnAllAbsent = new Button
                {
                    Content = "❌ Все отсутствуют",
                    Style = (Style)FindResource("SmallButtonStyle"),
                    Background = new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    Margin = new Thickness(0, 0, 8, 0),
                    Padding = new Thickness(12, 5, 12, 5)
                };
                btnAllAbsent.Click += (s, args) =>
                {
                    for (int i = 0; i < checkBoxes.Count; i++)
                    {
                        checkBoxes[i].IsChecked = true;
                        statusComboBoxes[i].SelectedIndex = 1;
                    }
                };
                quickPanel.Children.Add(btnAllAbsent);

                panel.Children.Add(quickPanel);

                panel.Children.Add(new TextBlock { Text = "Примечание:", Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)), Margin = new Thickness(0, 0, 0, 5) });
                var txtNote = new TextBox
                {
                    Margin = new Thickness(0, 0, 0, 15),
                    Padding = new Thickness(10, 10, 10, 10),
                    Height = 60,
                    TextWrapping = TextWrapping.Wrap
                };
                panel.Children.Add(txtNote);

                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

                Button btnSave = new Button
                {
                    Content = "Сохранить",
                    Style = (Style)FindResource("MainButtonStyle"),
                    Background = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    Margin = new Thickness(0, 0, 10, 0),
                    Width = 100
                };
                btnSave.Click += (s, args) =>
                {
                    try
                    {
                        int saved = 0;
                        var date = dpDate.SelectedDate ?? DateTime.Now;

                        for (int i = 0; i < checkBoxes.Count; i++)
                        {
                            if (checkBoxes[i].IsChecked == true)
                            {
                                var student = students.FirstOrDefault(st => st.FullName == checkBoxes[i].Content.ToString());
                                if (student != null)
                                {
                                    var status = (AttendanceStatus)((ComboBoxItem)statusComboBoxes[i].SelectedItem).Tag;
                                    var attendance = new Attendance
                                    {
                                        StudentId = student.Id,
                                        Date = date,
                                        Status = status,
                                        Note = txtNote.Text
                                    };

                                    if (dbHelper.SaveAttendance(attendance))
                                    {
                                        SaveLog(student.Id, student.FullName, "add_attendance", "Посещаемость", "", status.ToString(),
                                            $"Добавлена посещаемость: {status}");
                                        saved++;
                                    }
                                }
                            }
                        }

                        dialog.Close();
                        LoadJournal();
                        MessageBox.Show($"Отмечено {saved} учеников", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnViewLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logWindow = new LogViewerWindow(dbHelper, className);
                logWindow.Owner = this;
                logWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия логов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnExportJournal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (students == null || !students.Any())
                {
                    MessageBox.Show("Нет данных для экспорта", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Title = "Сохранить журнал",
                    Filter = "CSV файлы (*.csv)|*.csv|Все файлы (*.*)|*.*",
                    DefaultExt = ".csv",
                    FileName = $"Журнал_{className}_{DateTime.Now:yyyy-MM-dd}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();

                    sb.Append("№;ФИО;");
                    foreach (var date in dates)
                    {
                        sb.Append($"{date:dd.MM};");
                    }
                    sb.AppendLine("Средний балл;Посещаемость;Изменения");

                    var journal = lvJournal.ItemsSource as List<JournalStudent>;
                    if (journal != null)
                    {
                        int index = 1;
                        foreach (var item in journal)
                        {
                            sb.Append($"{index};{item.StudentName};");
                            index++;

                            foreach (var grade in item.Grades)
                            {
                                sb.Append(grade.Grade > 0 ? $"{grade.Grade};" : ";");
                            }

                            var grades = item.Grades.Where(g => g.Grade > 0).ToList();
                            double avg = grades.Any() ? grades.Average(g => g.Grade) : 0;
                            sb.Append($"{avg:F2};");

                            var attendance = allAttendance?.Where(a => a.StudentId == item.StudentId).ToList() ?? new List<Attendance>();
                            int present = attendance.Count(a => a.Status == AttendanceStatus.Present);
                            int total = attendance.Count;
                            int percent = total > 0 ? (present * 100 / total) : 0;
                            sb.Append($"{percent}% ({present}/{total});");

                            int changes = item.Grades.Count(g => g.IsChanged);
                            sb.AppendLine(changes > 0 ? $"⚠️ {changes}" : "");
                        }
                    }

                    System.IO.File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Журнал экспортирован", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
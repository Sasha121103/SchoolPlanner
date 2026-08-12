using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SchoolPlanner.Database;
using SchoolPlanner.Pages;

namespace SchoolPlanner
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer timer;

        public MainWindow()
        {
            InitializeComponent();
            App.MainWindow = this;
            MainFrame.Navigate(new LoginPage());

            StartClock();
        }

        public void UpdateUserInfo()
        {
            if (App.CurrentUser != null && txtUserInfo != null)
            {
                txtUserInfo.Text = $"{App.CurrentUser.FullName} ({GetRoleName(App.CurrentUser.Role)})";
            }
        }

        private string GetRoleName(UserRole role)
        {
            return role == UserRole.Admin ? "УВР" : "Учитель";
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentUser = null;
            MainFrame.Navigate(new LoginPage());
        }

        #region Управление окном

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void btnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                // Меняем иконку на квадрат
                (sender as Button).Content = "☐";
            }
            else
            {
                WindowState = WindowState.Maximized;
                // Меняем иконку на два квадрата (восстановить)
                (sender as Button).Content = "⧉";
            }
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        #endregion

        #region Часы, дата и день недели

        private void StartClock()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();
            UpdateClock();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateClock();
        }

        private void UpdateClock()
        {
            try
            {
                var now = DateTime.Now;

                if (txtClock != null)
                {
                    txtClock.Text = now.ToString("HH:mm:ss");
                }

                if (txtDate != null)
                {
                    txtDate.Text = now.ToString("dd MMMM yyyy");
                }

                if (txtDayOfWeek != null)
                {
                    // Получаем день недели на русском языке
                    string dayOfWeek = GetRussianDayOfWeek(now.DayOfWeek);
                    txtDayOfWeek.Text = dayOfWeek;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Clock error: {ex.Message}");
            }
        }

        /// <summary>
        /// Возвращает название дня недели на русском языке
        /// </summary>
        private string GetRussianDayOfWeek(DayOfWeek dayOfWeek)
        {
            switch (dayOfWeek)
            {
                case DayOfWeek.Monday: return "Понедельник";
                case DayOfWeek.Tuesday: return "Вторник";
                case DayOfWeek.Wednesday: return "Среда";
                case DayOfWeek.Thursday: return "Четверг";
                case DayOfWeek.Friday: return "Пятница";
                case DayOfWeek.Saturday: return "Суббота";
                case DayOfWeek.Sunday: return "Воскресенье";
                default: return dayOfWeek.ToString();
            }
        }

        #endregion
    }
}
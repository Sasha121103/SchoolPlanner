using System.Windows;
using System.Windows.Controls;
using SchoolPlanner.Database;
using SchoolPlanner.Page;

namespace SchoolPlanner.Pages
{
    public partial class LoginPage
    {
        private DbHelper dbHelper;

        public LoginPage()
        {
            InitializeComponent();
            dbHelper = new DbHelper();

            // Очищаем статус при загрузке
            if (txtStatus != null)
            {
                txtStatus.Text = "";
                txtStatus.Visibility = Visibility.Collapsed;
            }

            // Обработчик нажатия Enter
            txtUsername.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    txtPassword.Focus();
                }
            };

            txtPassword.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    btnLogin_Click(s, e);
                }
            };
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("⚠️ Введите логин и пароль");
                return;
            }

            try
            {
                var user = dbHelper.AuthenticateUser(username, password);

                if (user != null)
                {
                    App.CurrentUser = user;

                    // Обновляем главное окно
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.UpdateUserInfo();
                    }

                    // Переходим в главное меню
                    NavigationService.Navigate(new MainMenuPage());
                }
                else
                {
                    ShowError("❌ Неверный логин или пароль");
                    txtPassword.Password = "";
                    txtPassword.Focus();
                }
            }
            catch (System.Exception ex)
            {
                ShowError($"❌ Ошибка подключения: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Ошибка входа: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            if (txtStatus != null)
            {
                txtStatus.Text = message;
                txtStatus.Visibility = Visibility.Visible;
            }
        }
    }
}
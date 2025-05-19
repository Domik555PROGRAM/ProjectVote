using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Project_Vote
{
    public partial class PasswordPromptWindow : Window
    {
        public string Password { get; private set; }
        private bool _isPasswordVisible = false;
        private bool _isChangeMode;

        public PasswordPromptWindow(string testTitle, bool isChangeMode = false)
        {
            InitializeComponent();
            _isChangeMode = isChangeMode;

            // Устанавливаем название теста в заголовке
            TestTitleTextBlock.Text = $"Тест: {testTitle}";
            this.Title = isChangeMode ? "Изменение пароля" : "Ввод пароля";

            // Фокус на поле ввода пароля
            PasswordBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {

            Password = _isPasswordVisible ? PasswordTextBox.Text : PasswordBox.Password;

            // Если пароль пустой, показываем предупреждение
            if (string.IsNullOrEmpty(Password))
            {
                MessageBox.Show("Пожалуйста, введите пароль.", "Пароль не введен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);

                if (_isPasswordVisible)
                    PasswordTextBox.Focus();
                else
                    PasswordBox.Focus();

                return;
            }

            // Закрываем окно с результатом true (пароль введен)
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Закрываем окно с результатом false (отмена)
            DialogResult = false;
            Close();
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Если нажат Enter, выполняем действие кнопки ОК
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
            }
            // Если нажат Escape, выполняем действие кнопки Отмена
            else if (e.Key == Key.Escape)
            {
                CancelButton_Click(sender, e);
            }
        }

        // Метод для переключения видимости пароля
        private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                // Показываем пароль в текстовом поле
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordTextBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;
                TogglePasswordButton.Content = "🔒";
                PasswordTextBox.Focus();
                PasswordTextBox.SelectionStart = PasswordTextBox.Text.Length;
            }
            else
            {
                // Скрываем пароль и показываем поле PasswordBox
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordBox.Visibility = Visibility.Visible;
                TogglePasswordButton.Content = "👁";
                PasswordBox.Focus();
            }
        }

        // Метод для синхронизации текста между PasswordBox и TextBox
        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isPasswordVisible && PasswordTextBox.Visibility == Visibility.Visible)
            {
                PasswordBox.Password = PasswordTextBox.Text;
            }
        }
    }
}
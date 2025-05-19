using MySql.Data.MySqlClient;
using Project_Vote.Models;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Project_Vote
{
    /// <summary>
    /// Логика взаимодействия для Vhod.xaml
    /// </summary>
    public partial class Vhod : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=voteuser;Uid=root";
        private bool isPasswordVisible = false;

        // Метод для хеширования пароля с использованием SHA-256
        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                // Преобразуем пароль в массив байтов
                byte[] bytes = Encoding.UTF8.GetBytes(password);

                // Вычисляем хеш SHA-256
                byte[] hash = sha256.ComputeHash(bytes);

                // Преобразуем массив байтов в строку в шестнадцатеричном формате
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        public Vhod()
        {
            InitializeComponent();
        }

        private void ClearErrors()
        {
            EmailErrorText.Visibility = Visibility.Collapsed;
            PasswordErrorText.Visibility = Visibility.Collapsed;
        }

        private void ShowError(TextBlock errorText, string message)
        {
            errorText.Text = message;
            errorText.Visibility = Visibility.Visible;
        }

        // Обработчик для переключения видимости пароля
        private void TogglePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            isPasswordVisible = !isPasswordVisible;

            if (isPasswordVisible)
            {
                // Показываем пароль
                PasswordVisibleBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                PasswordVisibleBox.Visibility = Visibility.Visible;

                // Меняем иконку
                TextBlock eyeIcon = TogglePasswordButton.Content as TextBlock;
                if (eyeIcon != null)
                {
                    eyeIcon.Text = "👁️‍🗨️";
                    eyeIcon.Foreground = new SolidColorBrush(Color.FromArgb(204, 255, 255, 255)); // #CCFFFFFF
                }
            }
            else
            {
                // Скрываем пароль
                PasswordBox.Password = PasswordVisibleBox.Text;
                PasswordVisibleBox.Visibility = Visibility.Collapsed;
                PasswordBox.Visibility = Visibility.Visible;

                // Возвращаем иконку
                TextBlock eyeIcon = TogglePasswordButton.Content as TextBlock;
                if (eyeIcon != null)
                {
                    eyeIcon.Text = "👁️";
                    eyeIcon.Foreground = new SolidColorBrush(Color.FromArgb(204, 255, 255, 255)); // #CCFFFFFF
                }
            }

            // Фокусируем элемент ввода пароля
            if (isPasswordVisible)
                PasswordVisibleBox.Focus();
            else
                PasswordBox.Focus();
        }

        // Синхронизация между видимым и скрытым полем пароля
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!isPasswordVisible)
            {
                PasswordVisibleBox.Text = PasswordBox.Password;
            }
        }

        private void PasswordVisibleBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isPasswordVisible)
            {
                PasswordBox.Password = PasswordVisibleBox.Text;
            }
        }

        private bool CheckTableExists(MySqlConnection connection)
        {
            try
            {
                string checkTableQuery = "SHOW TABLES LIKE 'users'";
                using (MySqlCommand cmd = new MySqlCommand(checkTableQuery, connection))
                {
                    object result = cmd.ExecuteScalar();
                    return result != null;
                }
            }
            catch (Exception ex)
            {
                ShowError(EmailErrorText, $"Ошибка проверки базы данных: {ex.Message}");
                return false;
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            ClearErrors();
            bool hasErrors = false;

            if (string.IsNullOrWhiteSpace(EmailTextBox.Text))
            {
                ShowError(EmailErrorText, "Пожалуйста, введите email");
                EmailTextBox.Focus();
                hasErrors = true;
                return;
            }

            if (!EmailTextBox.Text.Contains("@") || !EmailTextBox.Text.Contains("."))
            {
                ShowError(EmailErrorText, "Пожалуйста, введите корректный email");
                EmailTextBox.Focus();
                hasErrors = true;
                return;
            }

            // Получаем пароль из активного поля
            string password = isPasswordVisible ? PasswordVisibleBox.Text : PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError(PasswordErrorText, "Пожалуйста, введите пароль");
                if (isPasswordVisible)
                    PasswordVisibleBox.Focus();
                else
                    PasswordBox.Focus();
                hasErrors = true;
                return;
            }

            if (!hasErrors)
            {
                try
                {
                    using (MySqlConnection conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();

                        // Проверяем существование таблицы users
                        if (!CheckTableExists(conn))
                        {
                            ShowError(EmailErrorText, "Таблица пользователей не существует. Сначала зарегистрируйтесь.");
                            return;
                        }

                        // Хешируем пароль перед проверкой
                        string hashedPassword = HashPassword(password);

                        string query = "SELECT * FROM users WHERE email = @email AND password = @password";
                        using (MySqlCommand cmd = new MySqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@email", EmailTextBox.Text);
                            cmd.Parameters.AddWithValue("@password", hashedPassword);

                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    // Сохраняем данные пользователя
                                    CurrentUser.UserId = reader.GetInt32("id");
                                    CurrentUser.Email = reader.GetString("email");
                                    CurrentUser.Name = reader.GetString("name");
                                    if (!reader.IsDBNull(reader.GetOrdinal("photo")))
                                    {
                                        byte[] photoData = (byte[])reader["photo"];
                                        CurrentUser.Photo = photoData;
                                    }

                                    DialogResult = true;
                                    Close();
                                }
                                else
                                {
                                    ShowError(PasswordErrorText, "Неверный email или пароль");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShowError(EmailErrorText, $"Ошибка при входе: {ex.Message}");
                }
            }
        }

        private void RegisterLink_Click(object sender, RoutedEventArgs e)
        {
            Registration registrationWindow = new Registration();
            if (registrationWindow.ShowDialog() == true)
            {
                // Если регистрация успешна, закрываем окно входа с положительным результатом
                DialogResult = true;
                Close();
            }
        }
    }
}

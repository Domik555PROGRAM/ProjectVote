using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using MySql.Data.MySqlClient;
using System.IO;
using Project_Vote.Models;
using System.Security.Cryptography;

namespace Project_Vote
{
    
    public partial class Registration : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=voteuser;Uid=root";
        private byte[] userPhotoData;
        private bool isPasswordVisible = false;

        public Registration()
        {
            InitializeComponent();
            // Убираем автоматическое подключение к БД при открытии окна
            
            // Проверим наличие изображений для кнопки видимости пароля
            try
            {
                // Проверяем, существуют ли файлы изображений
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string resourcesDir = System.IO.Path.Combine(baseDirectory, "Resources");
                
                if (!Directory.Exists(resourcesDir))
                {
                    Directory.CreateDirectory(resourcesDir);
                }
                
                string eyeOnPath = System.IO.Path.Combine(resourcesDir, "eye_on.png");
                string eyeOffPath = System.IO.Path.Combine(resourcesDir, "eye_off.png");
                
                // Если файлов нет, используем символы вместо изображений
                if (!File.Exists(eyeOnPath) || !File.Exists(eyeOffPath))
                {
                    TextBlock eyeIcon = new TextBlock
                    {
                        Text = "👁️",
                        FontSize = 16,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    TogglePasswordButton.Content = eyeIcon;
                }
            }
            catch
            {
                // В случае ошибки просто используем текст
                TogglePasswordButton.Content = "👁️";
            }
        }

        private void ClearErrors()
        {
            EmailErrorText.Visibility = Visibility.Collapsed;
            NameErrorText.Visibility = Visibility.Collapsed;
            PasswordErrorText.Visibility = Visibility.Collapsed;
            ConfirmPasswordErrorText.Visibility = Visibility.Collapsed;
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
                
                // Меняем иконку на "глаз перечеркнутый"
                if (PasswordVisibilityIcon != null)
                {
                    try
                    {
                        PasswordVisibilityIcon.Source = new BitmapImage(new Uri("/Resources/eye_on.png", UriKind.Relative));
                        PasswordVisibilityIcon.Opacity = 0.7;
                    }
                    catch
                    {
                        TextBlock eyeIcon = TogglePasswordButton.Content as TextBlock;
                        if (eyeIcon != null)
                        {
                            eyeIcon.Text = "👁️‍🗨️";
                            eyeIcon.Foreground = new SolidColorBrush(Color.FromArgb(204, 255, 255, 255)); // #CCFFFFFF
                        }
                    }
                }
            }
            else
            {
                // Скрываем пароль
                PasswordBox.Password = PasswordVisibleBox.Text;
                PasswordVisibleBox.Visibility = Visibility.Collapsed;
                PasswordBox.Visibility = Visibility.Visible;
                
                // Меняем иконку на "глаз"
                if (PasswordVisibilityIcon != null)
                {
                    try
                    {
                        PasswordVisibilityIcon.Source = new BitmapImage(new Uri("/Resources/eye_off.png", UriKind.Relative));
                        PasswordVisibilityIcon.Opacity = 0.7;
                    }
                    catch
                    {
                        TextBlock eyeIcon = TogglePasswordButton.Content as TextBlock;
                        if (eyeIcon != null)
                        {
                            eyeIcon.Text = "👁️";
                            eyeIcon.Foreground = new SolidColorBrush(Color.FromArgb(204, 255, 255, 255)); // #CCFFFFFF
                        }
                    }
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
                ValidatePassword(PasswordBox.Password);
            }
        }
        
        private void PasswordVisibleBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isPasswordVisible)
            {
                PasswordBox.Password = PasswordVisibleBox.Text;
                ValidatePassword(PasswordVisibleBox.Text);
            }
        }
        
        // Проверка требований к паролю
        private void ValidatePassword(string password)
        {
            // Проверка на наличие латинских букв разного регистра
            bool hasLatinLetters = Regex.IsMatch(password, "[a-z]") && Regex.IsMatch(password, "[A-Z]");
            LatinLettersCheck.Visibility = hasLatinLetters ? Visibility.Visible : Visibility.Collapsed;
            
            // Проверка на наличие цифр или спецсимволов
            bool hasDigitsOrSymbols = Regex.IsMatch(password, "[0-9]") || Regex.IsMatch(password, "[!@#$%^&*(),.?\":{}|<>]");
            DigitsSymbolsCheck.Visibility = hasDigitsOrSymbols ? Visibility.Visible : Visibility.Collapsed;
            
            // Проверка длины пароля
            bool hasMinLength = password.Length >= 8;
            LengthCheck.Visibility = hasMinLength ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool CheckEmailExists(string email, MySqlConnection connection)
        {
            string checkQuery = "SELECT COUNT(*) FROM users WHERE email = @email";
            using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, connection))
            {
                checkCmd.Parameters.AddWithValue("@email", email);
                int userCount = Convert.ToInt32(checkCmd.ExecuteScalar());
                return userCount > 0;
            }
        }

        private bool CheckNameExists(string name, MySqlConnection connection)
        {
            string checkQuery = "SELECT COUNT(*) FROM users WHERE name = @name";
            using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, connection))
            {
                checkCmd.Parameters.AddWithValue("@name", name);
                int userCount = Convert.ToInt32(checkCmd.ExecuteScalar());
                return userCount > 0;
            }
        }

        private bool CreateTableIfNotExists(MySqlConnection connection)
        {
            try
            {
                // Проверяем существование таблицы users
                string checkTableQuery = "SHOW TABLES LIKE 'users'";
                using (MySqlCommand cmd = new MySqlCommand(checkTableQuery, connection))
                {
                    object result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        // Если таблица не существует, создаем её
                        string createTableQuery = @"
                            CREATE TABLE users (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                email VARCHAR(255) NOT NULL UNIQUE,
                                name VARCHAR(16) NOT NULL UNIQUE,
                                password VARCHAR(255) NOT NULL,
                                photo LONGBLOB
                            )";
                        using (MySqlCommand createCmd = new MySqlCommand(createTableQuery, connection))
                        {
                            createCmd.ExecuteNonQuery();
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                ShowError(EmailErrorText, $"Ошибка базы данных: {ex.Message}");
                return false;
            }
        }

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

        private bool SaveUserToDatabase(string email, string name, string password, byte[] photo)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Создаем таблицу если она не существует
                    if (!CreateTableIfNotExists(conn))
                    {
                        return false;
                    }

                    // Проверяем, существует ли пользователь с таким email
                    if (CheckEmailExists(email, conn))
                    {
                        ShowError(EmailErrorText, "Пользователь с таким email уже существует!");
                        return false;
                    }

                    // Проверяем, существует ли пользователь с таким именем
                    if (CheckNameExists(name, conn))
                    {
                        ShowError(NameErrorText, "Пользователь с таким именем уже существует!");
                        return false;
                    }

                    // Хешируем пароль
                    string hashedPassword = HashPassword(password);

                    // Добавляем нового пользователя
                    string insertQuery = "INSERT INTO users (email, name, password, photo) VALUES (@email, @name, @password, @photo)";
                    using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@email", email);
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@password", hashedPassword); // Используем хешированный пароль
                        cmd.Parameters.AddWithValue("@photo", photo);

                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError(EmailErrorText, $"Ошибка при сохранении данных: {ex.Message}");
                return false;
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            ClearErrors();
            bool hasErrors = false;

            // Проверяем Email
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

            // Проверяем Имя
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                ShowError(NameErrorText, "Пожалуйста, введите имя");
                NameTextBox.Focus();
                hasErrors = true;
                return;
            }

            if (NameTextBox.Text.Length < 2)
            {
                ShowError(NameErrorText, "Имя должно содержать минимум 2 символа");
                NameTextBox.Focus();
                hasErrors = true;
                return;
            }

            if (NameTextBox.Text.Length > 16)
            {
                ShowError(NameErrorText, "Имя не должно превышать 16 символов");
                NameTextBox.Focus();
                hasErrors = true;
                return;
            }

            // Проверяем Пароль
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

            if (password.Length < 8)
            {
                ShowError(PasswordErrorText, "Пароль должен содержать минимум 8 символов");
                if (isPasswordVisible)
                    PasswordVisibleBox.Focus();
                else
                    PasswordBox.Focus();
                hasErrors = true;
                return;
            }
            
            // Дополнительная проверка требований к паролю
            bool hasLatinLetters = Regex.IsMatch(password, "[a-z]") && Regex.IsMatch(password, "[A-Z]");
            bool hasDigitsOrSymbols = Regex.IsMatch(password, "[0-9]") || Regex.IsMatch(password, "[!@#$%^&*(),.?\":{}|<>]");
            
            if (!hasLatinLetters || !hasDigitsOrSymbols)
            {
                ShowError(PasswordErrorText, "Пароль должен содержать латинские буквы разного регистра и цифры или спецсимволы");
                if (isPasswordVisible)
                    PasswordVisibleBox.Focus();
                else
                    PasswordBox.Focus();
                hasErrors = true;
                return;
            }

            // Проверяем Подтверждение пароля
            if (string.IsNullOrWhiteSpace(ConfirmPasswordBox.Password))
            {
                ShowError(ConfirmPasswordErrorText, "Пожалуйста, подтвердите пароль");
                ConfirmPasswordBox.Focus();
                hasErrors = true;
                return;
            }

            if (password != ConfirmPasswordBox.Password)
            {
                ShowError(ConfirmPasswordErrorText, "Пароли не совпадают");
                ConfirmPasswordBox.Focus();
                hasErrors = true;
                return;
            }

            if (!hasErrors)
            {
                if (SaveUserToDatabase(EmailTextBox.Text, NameTextBox.Text, password, userPhotoData))
                { 
                    // Сохраняем данные в CurrentUser
                    using (MySqlConnection conn = new MySqlConnection(connectionString))
                    {
                        conn.Open();
                        string query = "SELECT id FROM users WHERE email = @email";
                        using (MySqlCommand cmd = new MySqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@email", EmailTextBox.Text);
                            int userId = Convert.ToInt32(cmd.ExecuteScalar());
                            
                            CurrentUser.UserId = userId;
                            CurrentUser.Email = EmailTextBox.Text;
                            CurrentUser.Name = NameTextBox.Text;
                            CurrentUser.Photo = userPhotoData;
                        }
                    }

                    // Закрываем окно регистрации
                    DialogResult = true;
                    Close();
                }
            }
        }

        private void UploadPhoto_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Изображения|*.jpg;*.jpeg;*.png;*.gif;*.bmp";
            openFileDialog.Title = "Выберите фото профиля";

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Создаем новый BitmapImage из выбранного файла
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(openFileDialog.FileName);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    // Конвертируем изображение в массив байтов
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (MemoryStream ms = new MemoryStream())
                    {
                        encoder.Save(ms);
                        userPhotoData = ms.ToArray();
                    }

                    // Устанавливаем изображение в ImageBrush
                    ProfileImageBrush.ImageSource = bitmap;
                }
                catch (Exception ex)
                {
                    ShowError(NameErrorText, $"Ошибка при загрузке изображения: {ex.Message}");
                }
            }
        }
    }
}

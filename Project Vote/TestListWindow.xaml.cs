using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using Project_Vote.Models;
using System.Linq;
using System.Text;

namespace Project_Vote
{
    public class TestInfo
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public DateTime CreatedAt { get; set; }
        public int QuestionsCount { get; set; }
        public bool HasPassword { get; set; }
        public string Password { get; set; }
    }
    
    public partial class TestListWindow : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";
        private List<TestInfo> _availableTests;
        
        public TestListWindow()
        {
            InitializeComponent();
            LoadAvailableTests();
            // Добавляем отладку паролей
            DebugPasswords();
        }
        
        private void LoadAvailableTests()
        {
            try
            {
                _availableTests = new List<TestInfo>();
                
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    
                    string query = @"
                        SELECT p.id, p.title, p.description, p.created_at, p.password, u.name as author, 
                        (SELECT COUNT(*) FROM questions WHERE poll_id = p.id) as questions_count
                        FROM polls p
                        LEFT JOIN voteuser.users u ON p.user_id = u.id
                        WHERE p.poll_type = 'Тест с вопросами и вариантами ответов'
                        AND p.is_active = 1
                        ORDER BY p.created_at DESC";
                    
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string password = null;
                                bool hasPassword = false;
                                
                                if (!reader.IsDBNull(reader.GetOrdinal("password")))
                                {
                                    password = reader.GetString("password");
                                    hasPassword = !string.IsNullOrWhiteSpace(password);
                                }
                                
                                var test = new TestInfo
                                {
                                    Id = reader.GetInt32("id"),
                                    Title = reader.GetString("title"),
                                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? 
                                                 "" : reader.GetString("description"),
                                    Author = reader.IsDBNull(reader.GetOrdinal("author")) ?
                                            "Неизвестный автор" : reader.GetString("author"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    QuestionsCount = reader.GetInt32("questions_count"),
                                    HasPassword = hasPassword,
                                    Password = password
                                };
                                
                                _availableTests.Add(test);
                            }
                        }
                    }
                }
                
                TestsListView.ItemsSource = _availableTests;
                
                if (_availableTests.Count == 0)
                {
                    NoTestsText.Visibility = Visibility.Visible;
                }
                else
                {
                    NoTestsText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке списка тестов: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void StartTest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.Tag == null) 
                {
                    MessageBox.Show("Ошибка: Tag кнопки не задан", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                int testId = (int)button.Tag;
                var selectedTest = _availableTests.Find(t => t.Id == testId);
                
                if (selectedTest == null)
                {
                    MessageBox.Show($"Ошибка: Тест с ID {testId} не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Отладочная информация о тесте и его пароле
                MessageBox.Show($"Выбран тест: {selectedTest.Title}\nID: {selectedTest.Id}\nПароль в списке: {selectedTest.Password}\nЗащищен паролем: {selectedTest.HasPassword}", 
                    "Информация о тесте", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Проверяем наличие пароля напрямую из базы
                bool needsPassword = false;
                string dbPassword = null;
                
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT password FROM polls WHERE id = @testId";
                    
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@testId", testId);
                        var result = cmd.ExecuteScalar();
                        
                        if (result != null && result != DBNull.Value)
                        {
                            dbPassword = result.ToString().Trim();
                            needsPassword = !string.IsNullOrWhiteSpace(dbPassword);
                        }
                    }
                }
                
                MessageBox.Show($"Проверка пароля из БД:\nID теста: {testId}\nПароль в БД: {dbPassword}\nТребуется пароль: {needsPassword}", 
                    "Проверка пароля", MessageBoxButton.OK, MessageBoxImage.Information);
                
                if (needsPassword)
                {
                    var passwordWindow = new PasswordPromptWindow(selectedTest.Title, false);
                    if (passwordWindow.ShowDialog() == true)
                    {
                        string enteredPassword = passwordWindow.Password.Trim();
                        
                        if (enteredPassword != dbPassword)
                        {
                            MessageBox.Show($"Неверный пароль!\nВведено: '{enteredPassword}'\nОжидается: '{dbPassword}'", 
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        else
                        {
                            MessageBox.Show("Пароль верный!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    else
                    {
                        // Пользователь нажал отмену
                        return;
                    }
                }
                
                var testWindow = new TestPassingWindow(testId, selectedTest.Title);
                testWindow.Owner = this;
                testWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при запуске теста: {ex.Message}\n{ex.StackTrace}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void EditTest_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;

            int testId = (int)button.Tag;
            var selectedTest = _availableTests.Find(t => t.Id == testId);
            
            if (selectedTest == null) return;

            var passwordWindow = new PasswordPromptWindow(selectedTest.Title, true);
            if (passwordWindow.ShowDialog() == true)
            {
                string newPassword = passwordWindow.Password;
                if (UpdateTestPassword(testId, newPassword))
                {
                    MessageBox.Show("Пароль успешно изменен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadAvailableTests(); // Перезагружаем список тестов
                }
                else
                {
                    MessageBox.Show("Не удалось изменить пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private bool CheckTestPassword(int testId, string password)
        {
            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT password FROM polls WHERE id = @testId";
                    
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@testId", testId);
                        var result = cmd.ExecuteScalar();
                        
                        if (result == null || result == DBNull.Value)
                        {
                            MessageBox.Show("Пароль в базе отсутствует (null)", "Отладка", MessageBoxButton.OK, MessageBoxImage.Information);
                            return false;
                        }
                        
                        string storedPassword = result.ToString().Trim();
                        string inputPassword = password.Trim();
                        
                        // Детальная отладка
                        StringBuilder debug = new StringBuilder();
                        debug.AppendLine("Детальная проверка паролей:");
                        debug.AppendLine($"Введенный пароль: '{inputPassword}'");
                        debug.AppendLine($"Пароль в базе: '{storedPassword}'");
                        debug.AppendLine($"Длина введенного: {inputPassword.Length}");
                        debug.AppendLine($"Длина в базе: {storedPassword.Length}");
                        
                        // Преобразуем строки в массивы кодов символов
                        var inputCodes = inputPassword.ToCharArray().Select(c => (int)c).ToArray();
                        var storedCodes = storedPassword.ToCharArray().Select(c => (int)c).ToArray();
                        
                        debug.AppendLine($"Коды символов введенного: {string.Join(",", inputCodes)}");
                        debug.AppendLine($"Коды символов в базе: {string.Join(",", storedCodes)}");
                        
                        MessageBox.Show(debug.ToString(), "Отладка сравнения", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        bool isMatch = string.Equals(storedPassword, inputPassword, StringComparison.Ordinal);
                        MessageBox.Show($"Результат сравнения: {isMatch}", "Результат", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        return isMatch;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке пароля: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        
        private bool UpdateTestPassword(int testId, string newPassword)
        {
            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "UPDATE polls SET password = @password WHERE id = @testId";
                    
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@testId", testId);
                        cmd.Parameters.AddWithValue("@password", newPassword.Trim());
                        
                        int rowsAffected = cmd.ExecuteNonQuery();

                        // Проверяем обновленный пароль
                        if (rowsAffected > 0)
                        {
                            string checkQuery = "SELECT password FROM polls WHERE id = @testId";
                            using (var checkCmd = new MySqlCommand(checkQuery, conn))
                            {
                                checkCmd.Parameters.AddWithValue("@testId", testId);
                                var savedPassword = checkCmd.ExecuteScalar();
                                MessageBox.Show($"Проверка обновленного пароля:\nНовый пароль в базе: {savedPassword}", 
                                    "Проверка обновления", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении пароля: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // Отладка загрузки паролей
        private void DebugPasswords()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    
                    string query = "SELECT id, title, password FROM polls WHERE poll_type LIKE '%Тест%'";
                    
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            string debugInfo = "Пароли в базе данных:\n\n";
                            bool hasAnyPasswords = false;
                            
                            while (reader.Read())
                            {
                                int id = reader.GetInt32("id");
                                string title = reader.GetString("title");
                                bool hasPassword = !reader.IsDBNull(reader.GetOrdinal("password"));
                                string password = hasPassword ? reader.GetString("password") : "NULL";
                                
                                debugInfo += $"ID: {id}, Название: {title}\nПароль: {password}\n\n";
                                if (hasPassword) hasAnyPasswords = true;
                            }
                            
                            if (!hasAnyPasswords)
                            {
                                debugInfo += "НИ ОДИН ТЕСТ НЕ ИМЕЕТ ПАРОЛЯ!";
                            }
                            
                            MessageBox.Show(debugInfo, "Отладка паролей в БД", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отладке паролей: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 
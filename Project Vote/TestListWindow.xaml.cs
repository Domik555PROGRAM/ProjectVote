using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

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
        public string PollType { get; set; }
        public DateTime EndDate { get; set; }
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
                        SELECT p.id, p.title, p.description, p.created_at, p.password, p.poll_type, u.name as author, 
                        (SELECT COUNT(*) FROM questions WHERE poll_id = p.id) as questions_count,
                        p.end_date
                        FROM polls p
                        LEFT JOIN voteuser.users u ON p.user_id = u.id
                        WHERE (p.poll_type = 'Тест с вопросами и вариантами ответов' OR p.poll_type = 'Голосование')
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

                                // Проверяем тип опроса
                                string pollType = reader.GetString("poll_type");

                                // Получаем дату окончания
                                DateTime endDate = DateTime.MaxValue;
                                if (!reader.IsDBNull(reader.GetOrdinal("end_date")))
                                {
                                    endDate = reader.GetDateTime("end_date");
                                }

                                // Проверяем, активен ли опрос по дате
                                if (!IsPollActive(endDate))
                                {
                                    // Пропускаем неактивные опросы
                                    continue;
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
                                    Password = password,
                                    PollType = pollType, // Сохраняем тип опроса
                                    EndDate = endDate    // Сохраняем дату окончания
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
                if (button?.Tag == null) return;

                int testId = (int)button.Tag;
                var selectedTest = _availableTests.Find(t => t.Id == testId);

                if (selectedTest == null)
                {
                    MessageBox.Show($"Ошибка: Тест с ID {testId} не найден", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверяем тип опроса - тест или голосование
                string pollType = GetPollType(testId);

                // Проверяем дату окончания теста/опроса
                DateTime endDate = DateTime.MaxValue;

                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT end_date FROM polls WHERE id = @testId";

                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@testId", testId);
                        var result = cmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            endDate = Convert.ToDateTime(result);
                        }
                    }
                }

                // Проверяем, активен ли тест/опрос по дате окончания
                if (!IsPollActive(endDate))
                {
                    MessageBox.Show(
                        $"Срок действия {(pollType == "Голосование" ? "голосования" : "теста")} \"{selectedTest.Title}\" истек {endDate.ToShortDateString()}.\n\n" +
                        $"Данный {(pollType == "Голосование" ? "опрос" : "тест")} больше недоступен для прохождения.",
                        $"Срок {(pollType == "Голосование" ? "голосования" : "теста")} истек",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Получаем информацию о наличии пароля у теста/опроса
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

                // Если тест/опрос имеет пароль, запрашиваем его
                if (needsPassword)
                {
                    var passwordWindow = new PasswordPromptWindow(selectedTest.Title, false);
                    if (passwordWindow.ShowDialog() == true)
                    {
                        string enteredPassword = passwordWindow.Password.Trim();

                        if (enteredPassword != dbPassword)
                        {
                            MessageBox.Show("Неверный пароль! Доступ запрещен.",
                                "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    else
                    {
                        // Пользователь отменил ввод пароля
                        return;
                    }
                }

                // Если это голосование
                if (pollType == "Голосование")
                {
                    // Запускаем окно голосования
                    var votingWindow = new TestGolosovania(testId);
                    votingWindow.Owner = this;
                    votingWindow.ShowDialog();
                    return;
                }

                // Если пароля нет или пароль верный, запускаем тест (для типа "Тест с вопросами")
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

                // Проверяем наличие пароля у теста
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

                // Если тест защищен паролем, требуем его ввод
                if (needsPassword)
                {
                    var passwordWindow = new PasswordPromptWindow(selectedTest.Title, true);
                    if (passwordWindow.ShowDialog() == true)
                    {
                        string enteredPassword = passwordWindow.Password.Trim();

                        if (enteredPassword != dbPassword)
                        {
                            MessageBox.Show("Неверный пароль! Доступ к редактированию запрещен.",
                                "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                    else
                    {
                        // Пользователь отменил ввод пароля
                        return;
                    }
                }

                // Если пароля нет или пароль верный, открываем редактор теста
                var editWindow = new Golos();
                editWindow.LoadPollForEditing(testId);
                editWindow.Owner = this;
                editWindow.ShowDialog();

                // После редактирования обновляем список тестов
                LoadAvailableTests();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при редактировании теста: {ex.Message}\n{ex.StackTrace}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

        // Метод для проверки активности теста по дате окончания
        private bool IsPollActive(DateTime endDate)
        {
            // Если дата окончания далеко в будущем (год >= 9000), считаем тест без ограничения по времени
            if (endDate.Year >= 9000)
                return true;

            // Иначе проверяем, не прошла ли дата окончания
            return DateTime.Now.Date <= endDate.Date;
        }

        // Метод для получения типа опроса
        private string GetPollType(int pollId)
        {
            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT poll_type FROM polls WHERE id = @pollId";

                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@pollId", pollId);
                        var result = cmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            return result.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении типа опроса: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return "Неизвестный тип";
        }
    }
}
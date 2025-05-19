using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Project_Vote
{
    public class SearchItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Description { get; set; }
        public bool HasPassword { get; set; }
        public string Type { get; set; }
        public string ActionButtonText { get; set; }
    }

    public partial class SearchResultWindow : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";
        private List<SearchItem> searchResults;
        private string _currentSearchQuery;
        private string _currentCategory = "Все";

        public SearchResultWindow(string searchQuery)
        {
            InitializeComponent();
            _currentSearchQuery = searchQuery;
            Title = $"Результаты поиска: {searchQuery}";
            SearchBox.Text = searchQuery;

            searchResults = new List<SearchItem>();
            SearchItems();
        }

        public void UpdateSearch(string searchQuery)
        {
            if (searchQuery != _currentSearchQuery)
            {
                _currentSearchQuery = searchQuery;
                Title = $"Результаты поиска: {searchQuery}";

                // Выполняем поиск с обновленным запросом
                searchResults = new List<SearchItem>();
                SearchItems();
            }
        }

        private void PerformSearch()
        {
            string searchQuery = SearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                MessageBox.Show("Пожалуйста, введите запрос для поиска",
                                "Пустой запрос", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _currentSearchQuery = searchQuery;
            Title = $"Результаты поиска: {searchQuery}";

            // Выполняем поиск с текущими параметрами
            searchResults = new List<SearchItem>();

            // Остальной код перенесен в отдельный метод SearchItems
            SearchItems();
        }

        private void SearchItems()
        {
            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Определяем условие для типа элемента в зависимости от выбранной категории
                    string typeCondition = "";
                    if (_currentCategory == "Тесты")
                    {
                        typeCondition = "AND p.poll_type = 'Тест с вопросами и вариантами ответов'";
                    }
                    else if (_currentCategory == "Голосования")
                    {
                        typeCondition = "AND p.poll_type = 'Голосование'";
                    }

                    string query = @"
                        SELECT p.id, p.title, p.created_at, p.description, 
                               p.password, u.name as author, p.poll_type
                        FROM polls p
                        LEFT JOIN voteuser.users u ON p.user_id = u.id
                        WHERE p.title LIKE @searchQuery 
                        " + typeCondition + @"
                        AND p.is_active = 1
                        ORDER BY p.created_at DESC";

                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@searchQuery", $"%{_currentSearchQuery}%");

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string pollType = reader.GetString("poll_type");
                                string displayType = pollType == "Тест с вопросами и вариантами ответов" ? "Тест" : pollType;
                                string actionText = pollType == "Тест с вопросами и вариантами ответов" ? "Пройти тест" : "Голосовать";

                                searchResults.Add(new SearchItem
                                {
                                    Id = reader.GetInt32("id"),
                                    Title = reader.GetString("title"),
                                    Author = reader.IsDBNull(reader.GetOrdinal("author")) ?
                                             "Неизвестный автор" : reader.GetString("author"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ?
                                                  "" : reader.GetString("description"),
                                    HasPassword = !reader.IsDBNull(reader.GetOrdinal("password")) &&
                                                 !string.IsNullOrEmpty(reader.GetString("password")),
                                    Type = displayType,
                                    ActionButtonText = actionText
                                });
                            }
                        }
                    }
                }

                if (searchResults.Count > 0)
                {
                    ItemsListView.ItemsSource = searchResults;
                    NoResultsText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ItemsListView.ItemsSource = null;
                    NoResultsText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при поиске: {ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartItem_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null || button.Tag == null)
                return;

            int itemId = Convert.ToInt32(button.Tag);
            var selectedItem = searchResults.Find(t => t.Id == itemId);

            if (selectedItem == null)
                return;

            // Проверяем дату окончания 
            try
            {
                DateTime endDate = DateTime.MaxValue;

                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT end_date FROM polls WHERE id = @itemId";

                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@itemId", itemId);
                        var result = cmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            endDate = Convert.ToDateTime(result);
                        }
                    }
                }

                // Проверяем, активен ли по дате окончания
                if (!IsPollActive(endDate))
                {
                    string itemType = selectedItem.Type == "Тест" ? "теста" : "голосования";
                    MessageBox.Show(
                        $"Срок действия {itemType} \"{selectedItem.Title}\" истек {endDate.ToShortDateString()}.\n\n" +
                        $"Данное {(selectedItem.Type == "Тест" ? "тест" : "голосование")} больше недоступно для прохождения.",
                        $"Срок {itemType} истек",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                string itemType = selectedItem.Type == "Тест" ? "теста" : "голосования";
                MessageBox.Show($"Ошибка при проверке даты окончания {itemType}: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверяем пароль, если защищен
            if (selectedItem.HasPassword)
            {
                string password = GetItemPassword(itemId);

                // Показываем диалог для ввода пароля
                var passwordWindow = new PasswordPromptWindow(selectedItem.Title, false);
                if (passwordWindow.ShowDialog() == true)
                {
                    string enteredPassword = passwordWindow.Password.Trim();

                    if (enteredPassword != password)
                    {
                        string itemType = selectedItem.Type == "Тест" ? "тесту" : "голосованию";
                        MessageBox.Show($"Неверный пароль! Доступ к {itemType} запрещен.",
                            "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    return; // Пользователь отменил ввод пароля
                }
            }

            // Открываем соответствующее окно в зависимости от типа
            if (selectedItem.Type == "Тест")
            {
                // Открываем окно прохождения теста
                var testWindow = new TestPassingWindow(itemId, selectedItem.Title);
                testWindow.Owner = this;
                testWindow.ShowDialog();
            }
            else if (selectedItem.Type == "Голосование")
            {
                // Открываем окно голосования
                var votingWindow = new TestGolosovania(itemId, selectedItem.Title);
                votingWindow.Owner = this;
                votingWindow.ShowDialog();
            }
        }

        private string GetItemPassword(int itemId)
        {
            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT password FROM polls WHERE id = @itemId";

                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@itemId", itemId);
                        var result = cmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            return result.ToString().Trim();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке пароля: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return string.Empty;
        }

        private bool IsPollActive(DateTime endDate)
        {
            // Если дата окончания далеко в будущем (год >= 9000), считаем без ограничения по времени
            if (endDate.Year >= 9000)
                return true;

            // Иначе проверяем, не прошла ли дата окончания
            return DateTime.Now.Date <= endDate.Date;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformSearch();
            }
        }

        private void SearchAgain_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _currentCategory = selectedItem.Content.ToString();

                // Если окно уже инициализировано и есть текущий запрос, выполняем поиск снова
                if (!string.IsNullOrEmpty(_currentSearchQuery))
                {
                    searchResults = new List<SearchItem>();
                    SearchItems();
                }
            }
        }
    }
}
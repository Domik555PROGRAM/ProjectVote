using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MySql.Data.MySqlClient;

namespace Project_Vote
{
    /// <summary>
    /// Логика взаимодействия для SearchResultWindow.xaml
    /// </summary>
    public class TestItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Description { get; set; }
        public bool HasPassword { get; set; }
    }

    public partial class SearchResultWindow : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";
        private List<TestItem> searchResults;

        public SearchResultWindow(string searchQuery)
        {
            InitializeComponent();
            SearchTests(searchQuery);
        }

        private void SearchTests(string searchQuery)
        {
            searchResults = new List<TestItem>();
            
            try
            {
                using (var conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT p.id, p.title, p.created_at, p.description, 
                               p.password, u.name as author
                        FROM polls p
                        LEFT JOIN voteuser.users u ON p.user_id = u.id
                        WHERE p.title LIKE @searchQuery 
                        AND p.poll_type = 'Тест с вопросами и вариантами ответов'
                        AND p.is_active = 1
                        ORDER BY p.created_at DESC";

                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@searchQuery", $"%{searchQuery}%");
                        
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                searchResults.Add(new TestItem
                                {
                                    Id = reader.GetInt32("id"),
                                    Title = reader.GetString("title"),
                                    Author = reader.IsDBNull(reader.GetOrdinal("author")) ? 
                                             "Неизвестный автор" : reader.GetString("author"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? 
                                                  "" : reader.GetString("description"),
                                    HasPassword = !reader.IsDBNull(reader.GetOrdinal("password")) && 
                                                 !string.IsNullOrEmpty(reader.GetString("password"))
                                });
                            }
                        }
                    }
                }

                if (searchResults.Count > 0)
                {
                    TestsListView.ItemsSource = searchResults;
                    NoResultsText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    NoResultsText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при поиске тестов: {ex.Message}", 
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartTest_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null || button.Tag == null)
                return;

            int testId = Convert.ToInt32(button.Tag);
            var selectedTest = searchResults.Find(t => t.Id == testId);
            
            if (selectedTest == null)
                return;

            // Проверяем пароль, если тест защищен
            if (selectedTest.HasPassword)
            {
                string password = GetTestPassword(testId);
                
                // Показываем диалог для ввода пароля
                var passwordWindow = new PasswordPromptWindow(selectedTest.Title, false);
                if (passwordWindow.ShowDialog() == true)
                {
                    string enteredPassword = passwordWindow.Password.Trim();
                    
                    if (enteredPassword != password)
                    {
                        MessageBox.Show("Неверный пароль! Доступ к тесту запрещен.", 
                            "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    return; // Пользователь отменил ввод пароля
                }
            }
            
            // Открываем окно прохождения теста
            var testWindow = new TestPassingWindow(testId, selectedTest.Title);
            testWindow.Owner = this;
            testWindow.ShowDialog();
        }

        private string GetTestPassword(int testId)
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

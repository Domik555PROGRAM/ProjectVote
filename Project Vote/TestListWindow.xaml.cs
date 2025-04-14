using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using Project_Vote.Models;

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
    }
    
    public partial class TestListWindow : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";
        private List<TestInfo> _availableTests;
        
        public TestListWindow()
        {
            InitializeComponent();
            LoadAvailableTests();
        }
        
        private void LoadAvailableTests()
        {
            try
            {
                _availableTests = new List<TestInfo>();
                
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    
                    // Получаем только тесты с вопросами и ответами
                    string query = @"
                        SELECT p.id, p.title, p.description, p.created_at, u.name as author, 
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
                                _availableTests.Add(new TestInfo
                                {
                                    Id = reader.GetInt32("id"),
                                    Title = reader.GetString("title"),
                                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? 
                                                 "" : reader.GetString("description"),
                                    Author = reader.IsDBNull(reader.GetOrdinal("author")) ?
                                            "Неизвестный автор" : reader.GetString("author"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    QuestionsCount = reader.GetInt32("questions_count")
                                });
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
            var button = sender as Button;
            if (button?.Tag == null) return;
            
            int testId = (int)button.Tag;
            var selectedTest = _availableTests.Find(t => t.Id == testId);
            
            if (selectedTest == null) return;
            
            // Открываем окно прохождения теста
            TestPassingWindow testWindow = new TestPassingWindow(testId, selectedTest.Title);
            testWindow.Owner = this;
            
            if (testWindow.ShowDialog() == true)
            {
                // Можно обновить список, если нужно
                // LoadAvailableTests();
            }
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
} 
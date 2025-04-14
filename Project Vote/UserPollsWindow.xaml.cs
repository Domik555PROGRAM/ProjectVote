using System;
using System.Collections.Generic;
using System.Windows;
using MySql.Data.MySqlClient;
using System.Windows.Controls;

namespace Project_Vote
{   public class PollSummary
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Description { get; set; }
        public string PollType { get; set; }
        public bool IsActive { get; set; }
        public string Options { get; set; } 
    }
    public partial class UserPollsWindow : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";
        
        private List<PollSummary> _userPolls;
        
        public UserPollsWindow(List<PollSummary> userPolls)
        {
            InitializeComponent();
            _userPolls = userPolls;
            PollsListView.ItemsSource = _userPolls;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        
        private void EditPoll_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;
            int pollId = (int)button.Tag;
            var pollToEdit = _userPolls.Find(p => p.Id == pollId);
            if (pollToEdit == null)
            {
                MessageBox.Show("Не удалось найти выбранный опрос.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            try
            {
                var editWindow = new Golos();
                editWindow.LoadPollForEditing(pollId);
                if (editWindow.ShowDialog() == true)
                {
                    RefreshPollsList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии окна редактирования: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void DeletePoll_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag == null) return;
            int pollId = (int)button.Tag;
            var pollToDelete = _userPolls.Find(p => p.Id == pollId);
            if (pollToDelete == null)
            {
                MessageBox.Show("Не удалось найти выбранный опрос.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var result = MessageBox.Show(
                $"Вы действительно хотите удалить опрос \"{pollToDelete.Title}\"?\n\nЭто действие нельзя будет отменить.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                if (DeletePollFromDatabase(pollId))
                {
                    _userPolls.Remove(pollToDelete);
                    PollsListView.Items.Refresh();
                    if (_userPolls.Count == 0)
                    {
                        MessageBox.Show("У вас больше нет опросов.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                        this.Close();
                    }
                }
            }
        }
        
        private bool DeletePollFromDatabase(int pollId)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    // Удаляем опрос по ID
                    string deleteQuery = "DELETE FROM polls WHERE id = @id";
                    using (MySqlCommand cmd = new MySqlCommand(deleteQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", pollId);
                        int rowsAffected = cmd.ExecuteNonQuery();
                        
                        if (rowsAffected > 0)
                        {
                            return true;
                        }
                        else
                        {
                            MessageBox.Show("Опрос не был удален. Возможно, он уже не существует в базе данных.", 
                                "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении опроса: {ex.Message}", "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        
        private void RefreshPollsList()
        {
            try
            {
                // Получаем обновленный список опросов пользователя
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    
                    string query = "SELECT id, title, created_at, description, poll_type, is_active, options FROM polls WHERE user_id = @userId ORDER BY created_at DESC";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", Project_Vote.Models.CurrentUser.UserId);
                        
                        _userPolls.Clear();
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                _userPolls.Add(new PollSummary
                                {
                                    Id = reader.GetInt32("id"),
                                    Title = reader.GetString("title"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description"),
                                    PollType = reader.IsDBNull(reader.GetOrdinal("poll_type")) ? "" : reader.GetString("poll_type"),
                                    IsActive = reader.GetBoolean("is_active"),
                                    Options = reader.IsDBNull(reader.GetOrdinal("options")) ? "" : reader.GetString("options")
                                });
                            }
                        }
                    }
                }
                PollsListView.Items.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении списка опросов: {ex.Message}", "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

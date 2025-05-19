using MySql.Data.MySqlClient;
using Project_Vote.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Project_Vote
{
    public partial class TestGolosovania : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";
        private int _pollId;
        private string _pollTitle;
        private string _pollPassword;
        private DateTime _endDate;
        private ObservableCollection<VoteCandidate> _candidates = new ObservableCollection<VoteCandidate>();
        private VoteCandidate _selectedCandidate;
        private bool _hasVoted = false;

        public class VoteCandidate : Golos.PollOption
        {
            public bool IsSelected { get; set; }
            public int VoteCount { get; set; }

            // Копирует данные из PollOption в VoteCandidate
            public static VoteCandidate FromPollOption(Golos.PollOption option)
            {
                VoteCandidate candidate = new VoteCandidate
                {
                    Text = option.Text,
                    ImageData = option.ImageData,
                    ImageDescription = option.ImageDescription,
                    IsSelected = false,
                    VoteCount = 0
                };

                // Обновляем источник изображения, если есть данные
                if (option.ImageData != null && option.ImageData.Length > 0)
                {
                    candidate.UpdateImageSource();
                }

                return candidate;
            }
        }

        // Конструктор для открытия окна голосования
        public TestGolosovania(int pollId)
        {
            InitializeComponent();
            _pollId = pollId;
            LoadPollData();
        }

        // Конструктор с двумя параметрами для вызова из окна поиска
        public TestGolosovania(int pollId, string title)
        {
            InitializeComponent();
            _pollId = pollId;
            _pollTitle = title;
            TitleTextBlock.Text = title; // Устанавливаем заголовок сразу
            LoadPollData();
        }

        // Загрузка данных голосования из базы данных
        private void LoadPollData()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Загружаем основную информацию о голосовании
                    string pollQuery = @"
                        SELECT title, description, end_date, password 
                        FROM polls 
                        WHERE id = @pollId AND poll_type = 'Голосование'";

                    using (MySqlCommand cmd = new MySqlCommand(pollQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@pollId", _pollId);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                MessageBox.Show("Голосование не найдено или не является голосованием.",
                                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                Close();
                                return;
                            }

                            // Если заголовок не был предустановлен, загружаем его из базы данных
                            if (string.IsNullOrEmpty(_pollTitle))
                            {
                                _pollTitle = reader.GetString("title");
                                TitleTextBlock.Text = _pollTitle;
                            }

                            string description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description");

                            // Получаем дату окончания голосования
                            if (!reader.IsDBNull(reader.GetOrdinal("end_date")))
                            {
                                _endDate = reader.GetDateTime("end_date");
                            }
                            else
                            {
                                _endDate = DateTime.MaxValue; // Без ограничения времени
                            }

                            // Запоминаем пароль для служебных нужд, но не будем его запрашивать у пользователя
                            if (!reader.IsDBNull(reader.GetOrdinal("password")))
                            {
                                _pollPassword = reader.GetString("password");
                            }

                            // Устанавливаем информацию в интерфейсе
                            DescriptionTextBlock.Text = description;

                            // Устанавливаем информацию о дате окончания голосования
                            if (_endDate.Year >= 9000)
                            {
                                EndDateTextBlock.Text = "Голосование без ограничения времени";
                            }
                            else
                            {
                                EndDateTextBlock.Text = $"Голосование активно до: {_endDate.ToShortDateString()}";
                            }
                        }
                    }

                    // Проверяем активность голосования
                    if (!Golos.IsPollActive(_endDate))
                    {
                        MessageBox.Show($"Срок действия голосования \"{_pollTitle}\" истек {_endDate.ToShortDateString()}.\n\n" +
                                      "Данное голосование больше недоступно для участия.",
                                      "Срок голосования истек", MessageBoxButton.OK, MessageBoxImage.Warning);
                        Close();
                        return;
                    }

                    // Проверяем, голосовал ли уже пользователь
                    if (CurrentUser.IsLoggedIn)
                    {
                        string checkVoteQuery = @"
                            SELECT COUNT(*) FROM vote_results 
                            WHERE poll_id = @pollId AND user_id = @userId";

                        using (MySqlCommand cmd = new MySqlCommand(checkVoteQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@pollId", _pollId);
                            cmd.Parameters.AddWithValue("@userId", CurrentUser.UserId);

                            long count = Convert.ToInt64(cmd.ExecuteScalar());
                            if (count > 0)
                            {
                                _hasVoted = true;
                                MessageBox.Show("Вы уже проголосовали в этом голосовании.",
                                              "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }

                    // Загружаем кандидатов для голосования из таблицы опций опроса
                    string optionsQuery = @"
                        SELECT options FROM polls WHERE id = @pollId";

                    using (MySqlCommand cmd = new MySqlCommand(optionsQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@pollId", _pollId);

                        string optionsString = (string)cmd.ExecuteScalar();
                        if (!string.IsNullOrEmpty(optionsString))
                        {
                            string[] options = optionsString.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string option in options)
                            {
                                VoteCandidate candidate = new VoteCandidate { Text = option };
                                _candidates.Add(candidate);
                            }
                        }
                    }

                    // Загружаем изображения кандидатов
                    LoadCandidateImages(conn);

                    // Привязываем данные к интерфейсу
                    CandidatesItemsControl.ItemsSource = _candidates;

                    // Пароль больше не запрашиваем, так как пользователь уже прошел аутентификацию
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных голосования: {ex.Message}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        // Загрузка изображений кандидатов
        private void LoadCandidateImages(MySqlConnection conn)
        {
            try
            {
                string query = @"
                    SELECT candidate_name, image_data, description
                    FROM candidate_images
                    WHERE poll_id = @pollId";

                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@pollId", _pollId);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string candidateName = reader.GetString("candidate_name");
                            byte[] imageData = null;
                            string description = null;

                            // Получаем изображение, если оно есть
                            if (!reader.IsDBNull(reader.GetOrdinal("image_data")))
                            {
                                imageData = (byte[])reader["image_data"];
                            }

                            // Получаем описание, если оно есть
                            if (!reader.IsDBNull(reader.GetOrdinal("description")))
                            {
                                description = reader.GetString("description");
                            }

                            // Находим соответствующего кандидата и обновляем данные
                            VoteCandidate candidate = _candidates.FirstOrDefault(c => c.Text == candidateName);
                            if (candidate != null)
                            {
                                candidate.ImageData = imageData;
                                candidate.ImageDescription = description;
                                candidate.UpdateImageSource();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке изображений кандидатов: {ex.Message}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Обработчик выбора кандидата
        private void Candidate_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_hasVoted)
            {
                MessageBox.Show("Вы уже проголосовали в этом голосовании.",
                              "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (sender is Border border && border.DataContext is VoteCandidate candidate)
            {
                // Снимаем выделение со всех кандидатов
                foreach (var c in _candidates)
                {
                    c.IsSelected = false;
                }

                // Выделяем выбранного кандидата
                candidate.IsSelected = true;
                _selectedCandidate = candidate;

                // Обновляем интерфейс
                CandidatesItemsControl.Items.Refresh();
            }
        }

        // Обработчик нажатия на кнопку "Проголосовать"
        private void VoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasVoted)
            {
                MessageBox.Show("Вы уже проголосовали в этом голосовании.",
                              "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_selectedCandidate == null)
            {
                MessageBox.Show("Пожалуйста, выберите кандидата для голосования.",
                              "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Пароль больше не запрашиваем
            // Сразу сохраняем голос в базе данных
            SaveVote();
        }
        private void CloseResult_Click(object sender, RoutedEventArgs e)
        {
            ResultPanel.Visibility = Visibility.Collapsed;
            Close();
        }

        private void ShowResultsButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно результатов голосования
            Voit_Results resultsWindow = new Voit_Results(_pollId);
            resultsWindow.Owner = this;
            resultsWindow.ShowDialog();

            // Закрываем текущее окно после просмотра результатов
            Close();
        }

        // Обработчик нажатия на кнопку "Отмена"
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Сохранение голоса в базе данных
        private void SaveVote()
        {
            if (!CurrentUser.IsLoggedIn)
            {
                MessageBox.Show("Для участия в голосовании необходимо войти в систему.",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Проверяем, существует ли таблица для результатов голосования
                    CreateVoteResultsTableIfNotExists(conn);

                    // Проверяем, не голосовал ли пользователь уже
                    string checkQuery = @"
                        SELECT COUNT(*) FROM vote_results 
                        WHERE poll_id = @pollId AND user_id = @userId";

                    using (MySqlCommand cmd = new MySqlCommand(checkQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@pollId", _pollId);
                        cmd.Parameters.AddWithValue("@userId", CurrentUser.UserId);

                        long count = Convert.ToInt64(cmd.ExecuteScalar());
                        if (count > 0)
                        {
                            MessageBox.Show("Вы уже проголосовали в этом голосовании.",
                                          "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                            _hasVoted = true;
                            return;
                        }
                    }

                    // Сохраняем голос
                    string insertQuery = @"
                        INSERT INTO vote_results (poll_id, user_id, candidate_name, vote_date) 
                        VALUES (@pollId, @userId, @candidateName, @voteDate)";

                    using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@pollId", _pollId);
                        cmd.Parameters.AddWithValue("@userId", CurrentUser.UserId);
                        cmd.Parameters.AddWithValue("@candidateName", _selectedCandidate.Text);
                        cmd.Parameters.AddWithValue("@voteDate", DateTime.Now);

                        cmd.ExecuteNonQuery();
                    }

                    _hasVoted = true;

                    // Показываем результат
                    ResultTitleTextBlock.Text = "Ваш голос принят!";
                    ResultMessageTextBlock.Text = $"Вы успешно проголосовали за кандидата \"{_selectedCandidate.Text}\".";
                    ResultPanel.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении голоса: {ex.Message}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Создание таблицы для хранения результатов голосования
        private void CreateVoteResultsTableIfNotExists(MySqlConnection conn)
        {
            try
            {
                string checkTableQuery = "SHOW TABLES LIKE 'vote_results'";
                using (MySqlCommand cmd = new MySqlCommand(checkTableQuery, conn))
                {
                    object result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        string createTableQuery = @"
                            CREATE TABLE IF NOT EXISTS `vote_results` (
                              `id` INT NOT NULL AUTO_INCREMENT,
                              `poll_id` INT NOT NULL,
                              `user_id` INT NOT NULL,
                              `candidate_name` VARCHAR(255) NOT NULL,
                              `vote_date` DATETIME NOT NULL,
                              PRIMARY KEY (`id`),
                              INDEX `idx_poll_id` (`poll_id` ASC),
                              INDEX `idx_user_id` (`user_id` ASC)
                            )";
                        using (MySqlCommand createCmd = new MySqlCommand(createTableQuery, conn))
                        {
                            createCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке/создании таблицы vote_results: {ex.Message}",
                              "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }
    }
}
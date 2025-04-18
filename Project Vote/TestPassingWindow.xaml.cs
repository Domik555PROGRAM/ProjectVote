using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using Project_Vote.Models;

namespace Project_Vote
{
    public class TestQuestion
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public List<TestOption> Options { get; set; } = new List<TestOption>();
        public List<int> SelectedOptionIds { get; set; } = new List<int>();
    }

    public class TestOption
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsSelected { get; set; }
    }

    public partial class TestPassingWindow : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";
        private int _testId;
        private string _testTitle;
        private List<TestQuestion> _questions = new List<TestQuestion>();
        private int _currentQuestionIndex = 0;
        private string _pollType;

        public TestPassingWindow(int testId, string testTitle)
        {
            InitializeComponent();
            _testId = testId;
            _testTitle = testTitle;
            TestTitleText.Text = _testTitle;

            LoadTestQuestions();
            ShowCurrentQuestion();
        }

        private void LoadTestQuestions()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Сначала проверим, есть ли данные о тесте в основной таблице polls
                    string pollQuery = "SELECT options, poll_type FROM polls WHERE id = @pollId";
                    string optionsData = null;
                    string pollType = null;

                    using (MySqlCommand cmdPoll = new MySqlCommand(pollQuery, conn))
                    {
                        cmdPoll.Parameters.AddWithValue("@pollId", _testId);
                        using (MySqlDataReader pollReader = cmdPoll.ExecuteReader())
                        {
                            if (pollReader.Read())
                            {
                                if (!pollReader.IsDBNull(pollReader.GetOrdinal("options")))
                                {
                                    optionsData = pollReader.GetString("options");
                                }
                                
                                if (!pollReader.IsDBNull(pollReader.GetOrdinal("poll_type")))
                                {
                                    pollType = pollReader.GetString("poll_type");
                                }
                            }
                        }
                    }

                    // Запомним тип опроса/теста
                    _pollType = pollType;

                    // Если у нас есть данные в поле options и это тест с вопросами, распарсим его
                    if (!string.IsNullOrEmpty(optionsData) && pollType == "Тест с вопросами и вариантами ответов")
                    {
                        ParseOptionsField(optionsData);
                    }
                    else
                    {
                        // Традиционный метод загрузки из таблицы questions
                        // Загружаем вопросы
                        string questionsQuery = "SELECT id, question_text FROM questions WHERE poll_id = @pollId ORDER BY question_order";
                        using (MySqlCommand cmd = new MySqlCommand(questionsQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@pollId", _testId);

                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    _questions.Add(new TestQuestion
                                    {
                                        Id = reader.GetInt32("id"),
                                        Text = reader.GetString("question_text")
                                    });
                                }
                            }
                        }

                        // Для каждого вопроса загружаем варианты ответов
                        foreach (var question in _questions)
                        {
                            string optionsQuery = @"
                                SELECT id, option_text, is_correct 
                                FROM question_options 
                                WHERE question_id = @questionId 
                                ORDER BY option_order";

                            using (MySqlCommand cmd = new MySqlCommand(optionsQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@questionId", question.Id);

                                using (MySqlDataReader reader = cmd.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        question.Options.Add(new TestOption
                                        {
                                            Id = reader.GetInt32("id"),
                                            Text = reader.GetString("option_text"),
                                            IsCorrect = reader.GetBoolean("is_correct"),
                                            IsSelected = false
                                        });
                                    }
                                }
                            }
                        }
                    }
                }

                // Проверим, есть ли у нас вопросы для теста
                if (_questions.Count == 0)
                {
                    MessageBox.Show("Тест не содержит вопросов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }

                // Обновляем индикатор прогресса
                ProgressText.Text = $"Вопрос {_currentQuestionIndex + 1} из {_questions.Count}";
                ProgressBar.Maximum = _questions.Count;
                ProgressBar.Value = 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке вопросов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void ParseOptionsField(string optionsData)
        {
            // Формат данных - вопросы и ответы разделены |||
            string[] parts = optionsData.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);
            
            int questionId = 1;
            TestQuestion currentQuestion = null;
            
            foreach (string part in parts)
            {
                if (part.StartsWith("Q:"))
                {
                    // Это новый вопрос
                    string questionText = part.Substring(2).Trim();
                    currentQuestion = new TestQuestion
                    {
                        Id = questionId++,
                        Text = questionText,
                        Options = new List<TestOption>()
                    };
                    _questions.Add(currentQuestion);
                }
                else if (part.StartsWith("O:") && currentQuestion != null)
                {
                    // Это вариант ответа для текущего вопроса
                    string optionText = part.Substring(2).Trim();
                    bool isCorrect = false;
                    
                    // Проверяем, содержит ли ответ информацию о правильности
                    if (optionText.Contains(":"))
                    {
                        string[] optionParts = optionText.Split(':');
                        if (optionParts.Length >= 2)
                        {
                            optionText = optionParts[0].Trim();
                            isCorrect = optionParts[1].Trim() == "1";
                        }
                    }
                    
                    currentQuestion.Options.Add(new TestOption
                    {
                        Id = currentQuestion.Options.Count + 1,
                        Text = optionText,
                        IsCorrect = isCorrect,
                        IsSelected = false
                    });
                }
            }
        }

        private void ShowCurrentQuestion()
        {
            if (_questions.Count == 0 || _currentQuestionIndex >= _questions.Count)
            {
                MessageBox.Show("Тест не содержит вопросов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }

            var currentQuestion = _questions[_currentQuestionIndex];
            QuestionText.Text = currentQuestion.Text;

            // Очищаем панель вариантов ответов
            OptionsPanel.Children.Clear();

            // Определяем, нужно ли использовать чекбоксы для множественного выбора
            bool useCheckboxes = _pollType != null && (_pollType.Contains("Множественный выбор") || 
                                                       currentQuestion.Options.Count(o => o.IsCorrect) > 1);

            // Создаем варианты ответов
            foreach (var option in currentQuestion.Options)
            {
                if (useCheckboxes)
                {
                    // Используем CheckBox для множественного выбора
                    CheckBox checkBox = new CheckBox
                    {
                        Content = option.Text,
                        Margin = new Thickness(0, 5, 0, 5),
                        Tag = option.Id,
                        IsChecked = option.IsSelected
                    };

                    checkBox.Checked += (s, e) =>
                    {
                        // Сохраняем выбор пользователя
                        var selected = s as CheckBox;
                        if (selected?.Tag != null && selected.IsChecked == true)
                        {
                            int optionId = (int)selected.Tag;
                            option.IsSelected = true;

                            // Добавляем ID в список выбранных, если его там еще нет
                            if (!currentQuestion.SelectedOptionIds.Contains(optionId))
                            {
                                currentQuestion.SelectedOptionIds.Add(optionId);
                            }
                        }
                    };

                    checkBox.Unchecked += (s, e) =>
                    {
                        // Обрабатываем снятие выбора
                        var selected = s as CheckBox;
                        if (selected?.Tag != null)
                        {
                            int optionId = (int)selected.Tag;
                            option.IsSelected = false;

                            // Удаляем ID из списка выбранных
                            currentQuestion.SelectedOptionIds.Remove(optionId);
                        }
                    };

                    OptionsPanel.Children.Add(checkBox);
                }
                else
                {
                    // Используем RadioButton для одиночного выбора
                    RadioButton radioButton = new RadioButton
                    {
                        Content = option.Text,
                        Margin = new Thickness(0, 5, 0, 5),
                        Tag = option.Id,
                        IsChecked = option.IsSelected
                    };

                    radioButton.Checked += (s, e) =>
                    {
                        // Сохраняем выбор пользователя
                        var selected = s as RadioButton;
                        if (selected?.Tag != null)
                        {
                            int optionId = (int)selected.Tag;
                            option.IsSelected = true;

                            // Сбрасываем выбор для других опций
                            foreach (var otherOption in currentQuestion.Options.Where(o => o.Id != optionId))
                            {
                                otherOption.IsSelected = false;
                            }

                            // Обновляем список выбранных ID
                            currentQuestion.SelectedOptionIds.Clear();
                            currentQuestion.SelectedOptionIds.Add(optionId);
                        }
                    };

                    OptionsPanel.Children.Add(radioButton);
                }
            }

            // Обновляем индикатор прогресса
            ProgressText.Text = $"Вопрос {_currentQuestionIndex + 1} из {_questions.Count}";
            ProgressBar.Value = _currentQuestionIndex + 1;

            // Управляем видимостью кнопок
            PrevButton.IsEnabled = _currentQuestionIndex > 0;
            NextButton.Content = _currentQuestionIndex < _questions.Count - 1 ? "Следующий" : "Завершить";
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentQuestionIndex > 0)
            {
                _currentQuestionIndex--;
                ShowCurrentQuestion();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверяем, выбран ли вариант ответа
            if (_questions[_currentQuestionIndex].SelectedOptionIds.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите вариант ответа.",
                    "Не выбран ответ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentQuestionIndex < _questions.Count - 1)
            {
                // Переходим к следующему вопросу
                _currentQuestionIndex++;
                ShowCurrentQuestion();
            }
            else
            {
                // Завершаем тест и показываем результаты
                FinishTest();
            }
        }

        private void FinishTest()
        {
            // Подсчитываем результаты
            int totalQuestions = _questions.Count;
            int correctAnswers = 0;

            foreach (var question in _questions)
            {
                bool questionCorrect = true;

                // Проверяем, все ли выбранные варианты правильные и все ли правильные варианты выбраны
                foreach (var option in question.Options)
                {
                    if (option.IsSelected && !option.IsCorrect)
                    {
                        questionCorrect = false;
                        break;
                    }

                    if (!option.IsSelected && option.IsCorrect)
                    {
                        questionCorrect = false;
                        break;
                    }
                }

                if (questionCorrect)
                {
                    correctAnswers++;
                }
            }

            // Вычисляем процент правильных ответов
            double percentCorrect = (double)correctAnswers / totalQuestions * 100;

            // Сохраняем результат в базу данных
            if (CurrentUser.IsLoggedIn)
            {
                SaveTestResult(correctAnswers, totalQuestions, percentCorrect);
            }

            // Показываем окно с результатами
            TestResultWindow resultWindow = new TestResultWindow(
                _testTitle,
                correctAnswers,
                totalQuestions,
                percentCorrect,
                _questions); // Передаем вопросы для подробного отчета

            resultWindow.Owner = this;
            resultWindow.ShowDialog();

            // Закрываем окно теста
            this.DialogResult = true;
            this.Close();
        }

        private void SaveTestResult(int correctAnswers, int totalQuestions, double percentCorrect)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Проверяем, существует ли таблица test_results
                    CreateTestResultsTableIfNotExists(conn);

                    // Сохраняем результат теста
                    string insertQuery = @"
                        INSERT INTO test_results 
                        (user_id, poll_id, correct_answers, total_questions, percentage, completed_at) 
                        VALUES 
                        (@userId, @pollId, @correctAnswers, @totalQuestions, @percentage, @completedAt)";

                    using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", CurrentUser.UserId);
                        cmd.Parameters.AddWithValue("@pollId", _testId);
                        cmd.Parameters.AddWithValue("@correctAnswers", correctAnswers);
                        cmd.Parameters.AddWithValue("@totalQuestions", totalQuestions);
                        cmd.Parameters.AddWithValue("@percentage", percentCorrect);
                        cmd.Parameters.AddWithValue("@completedAt", DateTime.Now);

                        cmd.ExecuteNonQuery();
                    }

                    // Сохраняем детальные ответы пользователя
                    SaveUserAnswers(conn);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении результатов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateTestResultsTableIfNotExists(MySqlConnection conn)
        {
            string query = @"
                CREATE TABLE IF NOT EXISTS test_results (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    user_id INT NOT NULL,
                    poll_id INT NOT NULL,
                    correct_answers INT NOT NULL,
                    total_questions INT NOT NULL,
                    percentage DOUBLE NOT NULL,
                    completed_at DATETIME NOT NULL,
                    INDEX (user_id),
                    INDEX (poll_id)
                )";

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.ExecuteNonQuery();
            }

            // Создаем таблицу для детальных ответов
            string detailsQuery = @"
                CREATE TABLE IF NOT EXISTS test_user_answers (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    user_id INT NOT NULL,
                    poll_id INT NOT NULL,
                    question_id INT NOT NULL,
                    option_id INT NOT NULL,
                    is_correct BOOLEAN NOT NULL,
                    completed_at DATETIME NOT NULL,
                    INDEX (user_id),
                    INDEX (poll_id),
                    INDEX (question_id),
                    INDEX (option_id)
                )";

            using (MySqlCommand cmd = new MySqlCommand(detailsQuery, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private void SaveUserAnswers(MySqlConnection conn)
        {
            string query = @"
                INSERT INTO test_user_answers 
                (user_id, poll_id, question_id, option_id, is_correct, completed_at) 
                VALUES 
                (@userId, @pollId, @questionId, @optionId, @isCorrect, @completedAt)";

            foreach (var question in _questions)
            {
                foreach (var optionId in question.SelectedOptionIds)
                {
                    var option = question.Options.FirstOrDefault(o => o.Id == optionId);
                    if (option != null)
                    {
                        using (MySqlCommand cmd = new MySqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@userId", CurrentUser.UserId);
                            cmd.Parameters.AddWithValue("@pollId", _testId);
                            cmd.Parameters.AddWithValue("@questionId", question.Id);
                            cmd.Parameters.AddWithValue("@optionId", optionId);
                            cmd.Parameters.AddWithValue("@isCorrect", option.IsCorrect);
                            cmd.Parameters.AddWithValue("@completedAt", DateTime.Now);

                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }
    }
}
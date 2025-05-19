using MySql.Data.MySqlClient;
using Project_Vote.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Project_Vote
{
    public class TestQuestion
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public List<TestOption> Options { get; set; } = new List<TestOption>();
        public List<int> SelectedOptionIds { get; set; } = new List<int>();
        public byte[] ImageData { get; set; }
        public string ImageDescription { get; set; }
        public bool HasImage => ImageData != null && ImageData.Length > 0;
        public BitmapImage QuestionImage { get; set; }
    }

    public class TestOption
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsSelected { get; set; }
        public byte[] ImageData { get; set; }
        public string ImageDescription { get; set; }
        public bool HasImage => ImageData != null && ImageData.Length > 0;
        public BitmapImage OptionImage { get; set; }
    }

    public partial class TestPassingWindow : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";
        private int _testId;
        private string _testTitle;
        private List<TestQuestion> _questions = new List<TestQuestion>();
        private int _currentQuestionIndex = 0;
        private string _pollType;
        private Image _questionImage;
        private Border _questionImageBorder;
        private TextBlock _questionImageDescription;

        public TestPassingWindow(int testId, string testTitle)
        {
            InitializeComponent();
            _questionImage = this.QuestionImage;
            _questionImageBorder = this.QuestionImageBorder;
            _questionImageDescription = this.QuestionImageDescription;
            _testId = testId;
            _testTitle = testTitle;
            TestTitleText.Text = _testTitle;
            LoadTestQuestions();
            if (_questions.Count == 0)
            {
                MessageBox.Show("Не удалось загрузить вопросы теста. Возможно, произошла ошибка в базе данных.",
                    "Ошибка загрузки", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
                return;
            }
            ShowCurrentQuestion();
        }

        private void LoadTestQuestions()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string pollQuery = "SELECT poll_type, options, end_date FROM polls WHERE id = @pollId";
                    string optionsData = null;
                    DateTime endDate = DateTime.MaxValue;
                    using (MySqlCommand cmdPoll = new MySqlCommand(pollQuery, conn))
                    {
                        cmdPoll.Parameters.AddWithValue("@pollId", _testId);
                        using (MySqlDataReader pollReader = cmdPoll.ExecuteReader())
                        {
                            if (pollReader.Read())
                            {
                                if (!pollReader.IsDBNull(pollReader.GetOrdinal("poll_type")))
                                {
                                    _pollType = pollReader.GetString("poll_type");
                                    if (!_pollType.Contains("Множественный выбор"))
                                    {
                                        _pollType = "Тест с вопросами и вариантами ответов";
                                    }
                                }

                                if (!pollReader.IsDBNull(pollReader.GetOrdinal("options")))
                                {
                                    optionsData = pollReader.GetString("options");
                                }
                                if (!pollReader.IsDBNull(pollReader.GetOrdinal("end_date")))
                                {
                                    endDate = pollReader.GetDateTime("end_date");
                                }
                            }
                        }
                    }
                    if (!IsDateActive(endDate))
                    {
                        MessageBox.Show(
                            $"Срок действия теста \"{_testTitle}\" истек {endDate.ToShortDateString()}.\n\n" +
                            "Данный тест/опрос больше недоступен для прохождения.",
                            "Срок теста истек",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        this.Close();
                        return;
                    }
                    List<TestQuestion> questionsFromDatabase = new List<TestQuestion>();
                    LoadQuestionsFromDatabase(conn, questionsFromDatabase);
                    if (questionsFromDatabase.Count == 0 &&
                        !string.IsNullOrEmpty(optionsData) &&
                        _pollType == "Тест с вопросами и вариантами ответов")
                    {
                        ParseOptionsField(optionsData);
                    }
                    else
                    {
                        _questions = questionsFromDatabase;
                    }
                }
                if (_questions.Count == 0)
                {
                    MessageBox.Show("Тест не содержит вопросов.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                    return;
                }
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

        private void LoadQuestionsFromDatabase(MySqlConnection conn, List<TestQuestion> questionsFromDatabase)
        {
            try
            {
                string questionsQuery = @"
                    SELECT id, question_text, question_image, image_description 
                    FROM questions 
                    WHERE poll_id = @pollId 
                    ORDER BY question_order";

                using (MySqlCommand cmd = new MySqlCommand(questionsQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@pollId", _testId);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        Dictionary<int, TestQuestion> questionsDict = new Dictionary<int, TestQuestion>();

                        while (reader.Read())
                        {
                            int questionId = reader.GetInt32("id");
                            string questionText = reader.GetString("question_text");
                            TestQuestion question = new TestQuestion
                            {
                                Id = questionId,
                                Text = questionText,
                                Options = new List<TestOption>()
                            };
                            if (!reader.IsDBNull(reader.GetOrdinal("question_image")))
                            {
                                byte[] imageData = (byte[])reader["question_image"];
                                question.ImageData = imageData;
                                using (MemoryStream ms = new MemoryStream(imageData))
                                {
                                    BitmapImage bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.StreamSource = ms;
                                    bitmap.EndInit();
                                    bitmap.Freeze();
                                    question.QuestionImage = bitmap;
                                }
                            }
                            if (!reader.IsDBNull(reader.GetOrdinal("image_description")))
                            {
                                question.ImageDescription = reader.GetString("image_description");
                            }

                            questionsDict[questionId] = question;
                            questionsFromDatabase.Add(question);
                        }
                        if (questionsDict.Count > 0)
                        {
                            string questionIds = string.Join(",", questionsDict.Keys);
                        }
                    }
                }
                if (questionsFromDatabase.Count > 0)
                {
                    string questionIds = string.Join(",", questionsFromDatabase.Select(q => q.Id));
                    if (!string.IsNullOrEmpty(questionIds))
                    {
                        string optionsQuery = $@"
                            SELECT question_id, id, option_text, is_correct, option_image, image_description
                            FROM question_options
                            WHERE question_id IN ({questionIds})
                            ORDER BY question_id, option_order";

                        using (MySqlCommand cmd = new MySqlCommand(optionsQuery, conn))
                        {
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int questionId = reader.GetInt32("question_id");
                                    int optionId = reader.GetInt32("id");
                                    string optionText = reader.GetString("option_text");
                                    bool isCorrect = reader.GetBoolean("is_correct");
                                    var question = questionsFromDatabase.FirstOrDefault(q => q.Id == questionId);
                                    if (question != null)
                                    {
                                        var option = new TestOption
                                        {
                                            Id = optionId,
                                            Text = optionText,
                                            IsCorrect = isCorrect
                                        };
                                        if (!reader.IsDBNull(reader.GetOrdinal("option_image")))
                                        {
                                            byte[] imageData = (byte[])reader["option_image"];
                                            option.ImageData = imageData;
                                            using (MemoryStream ms = new MemoryStream(imageData))
                                            {
                                                BitmapImage bitmap = new BitmapImage();
                                                bitmap.BeginInit();
                                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                                bitmap.StreamSource = ms;
                                                bitmap.EndInit();
                                                bitmap.Freeze();
                                                option.OptionImage = bitmap;
                                            }
                                        }
                                        if (!reader.IsDBNull(reader.GetOrdinal("image_description")))
                                        {
                                            option.ImageDescription = reader.GetString("image_description");
                                        }

                                        question.Options.Add(option);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке вопросов из базы данных: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ParseOptionsField(string optionsData)
        {
            try
            {
                string[] parts = optionsData.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);

                int questionId = 1;
                TestQuestion currentQuestion = null;

                foreach (string part in parts)
                {
                    if (part.StartsWith("Q:"))
                    {
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
                        string optionText = part.Substring(2).Trim();
                        bool isCorrect = false;
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
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при разборе вопросов из формата options: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (currentQuestion.HasImage && currentQuestion.QuestionImage != null)
            {
                QuestionImage.Source = currentQuestion.QuestionImage;
                QuestionImageDescription.Text = currentQuestion.ImageDescription ?? "";
                QuestionImageBorder.Visibility = Visibility.Visible;
            }
            else
            {
                QuestionImage.Source = null;
                QuestionImageDescription.Text = "";
                QuestionImageBorder.Visibility = Visibility.Collapsed;
            }
            OptionsPanel.Children.Clear();
            bool useCheckboxes = false;
            if (_pollType != null)
            {
                if (_pollType.Contains("Множественный выбор"))
                {
                    useCheckboxes = true;
                }
                else if (_pollType.Contains("Тест с вопросами и вариантами ответов") &&
                         currentQuestion.Options.Count(o => o.IsCorrect) > 1)
                {
                    useCheckboxes = true;
                }
            }
            string radioGroupName = $"Question_{currentQuestion.Id}_Group";

            // Создаем варианты ответов
            foreach (var option in currentQuestion.Options)
            {
                StackPanel optionPanel = new StackPanel
                {
                    Margin = new Thickness(0, 5, 0, 15)
                };

                if (useCheckboxes)
                {
                    TextBlock textBlock = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 22,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    ProcessOptionText(textBlock, option.Text);
                    CheckBox checkBox = new CheckBox
                    {
                        Content = textBlock,
                        Margin = new Thickness(0, 5, 0, 5),
                        Tag = option.Id,
                        IsChecked = option.IsSelected,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        FontSize = 16,
                        Padding = new Thickness(8, 0, 0, 0),
                        Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#333"))
                    };
                    checkBox.Checked += (s, e) =>
                    {
                        var selected = s as CheckBox;
                        if (selected?.Tag != null && selected.IsChecked == true)
                        {
                            int optionId = (int)selected.Tag;
                            option.IsSelected = true;
                            if (!currentQuestion.SelectedOptionIds.Contains(optionId))
                            {
                                currentQuestion.SelectedOptionIds.Add(optionId);
                            }
                        }
                    };
                    checkBox.Unchecked += (s, e) =>
                    {
                        var selected = s as CheckBox;
                        if (selected?.Tag != null)
                        {
                            int optionId = (int)selected.Tag;
                            option.IsSelected = false;
                            currentQuestion.SelectedOptionIds.Remove(optionId);
                        }
                    };

                    optionPanel.Children.Add(checkBox);
                }
                else
                {
                    TextBlock textBlock = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 22,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    ProcessOptionText(textBlock, option.Text);
                    RadioButton radioButton = new RadioButton
                    {
                        Content = textBlock,
                        Margin = new Thickness(0, 5, 0, 5),
                        Tag = option.Id,
                        IsChecked = option.IsSelected,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        FontSize = 16,
                        Padding = new Thickness(8, 0, 0, 0),
                        Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#333"))
                    };
                    radioButton.Checked += (s, e) =>
                    {
                        var selected = s as RadioButton;
                        if (selected?.Tag != null)
                        {
                            int optionId = (int)selected.Tag;
                            option.IsSelected = true;
                            foreach (var otherOption in currentQuestion.Options.Where(o => o.Id != optionId))
                            {
                                otherOption.IsSelected = false;
                            }
                            currentQuestion.SelectedOptionIds.Clear();
                            currentQuestion.SelectedOptionIds.Add(optionId);
                        }
                    };
                    radioButton.GroupName = radioGroupName;

                    optionPanel.Children.Add(radioButton);
                }
                if (option.HasImage && option.OptionImage != null)
                {
                    Border imageBorder = new Border
                    {
                        BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#eee")),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(30, 10, 5, 10),
                        MaxWidth = 600,
                        MaxHeight = 400
                    };

                    StackPanel imageStack = new StackPanel();
                    Image optionImage = new Image
                    {
                        Source = option.OptionImage,
                        Stretch = System.Windows.Media.Stretch.Uniform,
                        MaxHeight = 350,
                        Margin = new Thickness(5)
                    };
                    imageStack.Children.Add(optionImage);
                    if (!string.IsNullOrEmpty(option.ImageDescription))
                    {
                        TextBlock imageDescription = new TextBlock
                        {
                            Text = option.ImageDescription,
                            TextWrapping = TextWrapping.Wrap,
                            Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666")),
                            FontSize = 12,
                            Margin = new Thickness(5),
                            Padding = new Thickness(5),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#f8f8f8"))
                        };
                        imageStack.Children.Add(imageDescription);
                    }
                    imageBorder.Child = imageStack;
                    optionPanel.Children.Add(imageBorder);
                }
                OptionsPanel.Children.Add(optionPanel);
            }
            ProgressText.Text = $"Вопрос {_currentQuestionIndex + 1} из {_questions.Count}";
            ProgressBar.Maximum = _questions.Count;
            ProgressBar.Value = _currentQuestionIndex + 1;
            PrevButton.IsEnabled = _currentQuestionIndex > 0;
            NextButton.Content = _currentQuestionIndex < _questions.Count - 1 ? "Следующий" : "Завершить";
        }
        /// <param name="textBlock">TextBlock для отображения</param>
        /// <param name="text">Исходный текст</param>
        private void ProcessOptionText(TextBlock textBlock, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            Regex imgRegex = new Regex(@"\[img:(.*?)\]", RegexOptions.IgnoreCase);

            int lastIndex = 0;
            foreach (Match match in imgRegex.Matches(text))
            {
                string textBeforeImg = text.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrEmpty(textBeforeImg))
                {
                    textBlock.Inlines.Add(new Run(textBeforeImg));
                }
                string imageUrl = match.Groups[1].Value.Trim();
                try
                {
                    Image inlineImage = new Image();
                    inlineImage.MaxHeight = 20;
                    inlineImage.VerticalAlignment = VerticalAlignment.Center;
                    inlineImage.Margin = new Thickness(2, 0, 2, 0);
                    if (imageUrl.StartsWith("http"))
                    {
                        inlineImage.Source = new BitmapImage(new Uri(imageUrl));
                    }
                    else if (File.Exists(imageUrl))
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(imageUrl, UriKind.RelativeOrAbsolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        inlineImage.Source = bitmap;
                    }
                    InlineUIContainer container = new InlineUIContainer(inlineImage);
                    textBlock.Inlines.Add(container);
                }
                catch (Exception ex)
                {
                    textBlock.Inlines.Add(new Run($"[ошибка изображения: {ex.Message}]")
                    {
                        Foreground = Brushes.Red,
                        FontStyle = FontStyles.Italic
                    });
                }

                lastIndex = match.Index + match.Length;
            }
            if (lastIndex < text.Length)
            {
                string remainingText = text.Substring(lastIndex);
                textBlock.Inlines.Add(new Run(remainingText));
            }
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
            if (_questions[_currentQuestionIndex].SelectedOptionIds.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите вариант ответа.",
                    "Не выбран ответ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentQuestionIndex < _questions.Count - 1)
            {
                _currentQuestionIndex++;
                ShowCurrentQuestion();
            }
            else
            {
                FinishTest();
            }
        }

        private void FinishTest()
        {
            int totalQuestions = _questions.Count;
            int correctAnswers = 0;

            foreach (var question in _questions)
            {
                bool questionCorrect = true;
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
            double percentCorrect = (double)correctAnswers / totalQuestions * 100;
            if (CurrentUser.IsLoggedIn)
            {
                SaveTestResult(correctAnswers, totalQuestions, percentCorrect);
            }
            TestResultWindow resultWindow = new TestResultWindow(
                _testTitle,
                correctAnswers,
                totalQuestions,
                percentCorrect,
                _questions);

            resultWindow.Owner = this;
            resultWindow.ShowDialog();
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
                    TimeSpan duration = DateTime.Now - DateTime.Now.AddMinutes(-30);
                    int durationSeconds = (int)duration.TotalSeconds;
                    string insertQuery = @"
                        INSERT INTO test_results 
                        (user_id, poll_id, created_at, correct_answers, total_questions, percentage_correct, duration_seconds) 
                        VALUES 
                        (@userId, @pollId, @createdAt, @correctAnswers, @totalQuestions, @percentageCorrect, @durationSeconds)";

                    using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", CurrentUser.UserId);
                        cmd.Parameters.AddWithValue("@pollId", _testId);
                        cmd.Parameters.AddWithValue("@createdAt", DateTime.Now);
                        cmd.Parameters.AddWithValue("@correctAnswers", correctAnswers);
                        cmd.Parameters.AddWithValue("@totalQuestions", totalQuestions);
                        cmd.Parameters.AddWithValue("@percentageCorrect", percentCorrect);
                        cmd.Parameters.AddWithValue("@durationSeconds", durationSeconds);

                        cmd.ExecuteNonQuery();
                    }
                    SaveUserAnswers(conn);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении результатов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveUserAnswers(MySqlConnection conn)
        {
            int testResultId = 0;
            string getLastIdQuery = "SELECT LAST_INSERT_ID()";
            using (MySqlCommand cmd = new MySqlCommand(getLastIdQuery, conn))
            {
                testResultId = Convert.ToInt32(cmd.ExecuteScalar());
            }

            string query = @"
                INSERT INTO test_user_answers 
                (user_id, poll_id, question_id, option_id, is_correct, answer_time) 
                VALUES 
                (@userId, @pollId, @questionId, @optionId, @isCorrect, @answerTime)";

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
                            cmd.Parameters.AddWithValue("@answerTime", DateTime.Now);

                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        private bool IsDateActive(DateTime endDate)
        {
            if (endDate.Year >= 9000)
                return true;
            return DateTime.Now.Date <= endDate.Date;
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MySql.Data.MySqlClient;
using Project_Vote.Models;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using System.Windows.Media;

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
        
        // Элементы управления для изображения вопроса
        private Image _questionImage;
        private Border _questionImageBorder;
        private TextBlock _questionImageDescription;

        public TestPassingWindow(int testId, string testTitle)
        {
            InitializeComponent();
            
            // Инициализация элементов управления для изображения
            _questionImage = this.QuestionImage;
            _questionImageBorder = this.QuestionImageBorder;
            _questionImageDescription = this.QuestionImageDescription;
            
            _testId = testId;
            _testTitle = testTitle;
            TestTitleText.Text = _testTitle;

            LoadTestQuestions();
            
            // Проверка на случай, если вопросы не загрузились
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

                    // Загружаем информацию о типе теста
                    string pollQuery = "SELECT poll_type, options FROM polls WHERE id = @pollId";
                    string optionsData = null;
                    
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
                                    // При загрузке типа теста устанавливаем значение по умолчанию
                                    // для возможности множественного выбора
                                    if (!_pollType.Contains("Множественный выбор"))
                                    {
                                        _pollType = "Тест с вопросами и вариантами ответов";
                                    }
                                }
                                
                                if (!pollReader.IsDBNull(pollReader.GetOrdinal("options")))
                                {
                                    optionsData = pollReader.GetString("options");
                                }
                            }
                        }
                    }

                    // Список для временного хранения вопросов из базы данных
                    List<TestQuestion> questionsFromDatabase = new List<TestQuestion>();
                    
                    // Первым приоритетом загружаем вопросы из таблицы questions
                    LoadQuestionsFromDatabase(conn, questionsFromDatabase);
                    
                    // Если в базе данных нет вопросов и у нас есть данные options,
                    // то парсим вопросы из поля options
                    if (questionsFromDatabase.Count == 0 && 
                        !string.IsNullOrEmpty(optionsData) && 
                        _pollType == "Тест с вопросами и вариантами ответов")
                    {
                        ParseOptionsField(optionsData);
                    }
                    else
                    {
                        // Используем вопросы из базы данных
                        _questions = questionsFromDatabase;
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

        private void LoadQuestionsFromDatabase(MySqlConnection conn, List<TestQuestion> questionsFromDatabase)
        {
            try
            {
                // Загрузим все вопросы с изображениями
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
                            
                            // Создаем новый вопрос
                            TestQuestion question = new TestQuestion
                            {
                                Id = questionId,
                                Text = questionText,
                                Options = new List<TestOption>()
                            };
                            
                            // Загружаем изображение вопроса, если оно есть
                            if (!reader.IsDBNull(reader.GetOrdinal("question_image")))
                            {
                                byte[] imageData = (byte[])reader["question_image"];
                                question.ImageData = imageData;
                                
                                // Создаем BitmapImage для отображения
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
                            
                            // Загружаем описание изображения
                            if (!reader.IsDBNull(reader.GetOrdinal("image_description")))
                            {
                                question.ImageDescription = reader.GetString("image_description");
                            }
                            
                            questionsDict[questionId] = question;
                            questionsFromDatabase.Add(question);
                        }
                        
                        // Если мы нашли вопросы, загружаем варианты ответов
                        if (questionsDict.Count > 0)
                        {
                            string questionIds = string.Join(",", questionsDict.Keys);
                            
                            // Закрываем первый reader перед открытием второго
                        }
                    }
                }

                // Теперь загрузим варианты ответов для всех вопросов
                if (questionsFromDatabase.Count > 0)
                {
                    // Создаем строку с ID всех вопросов для SQL запроса
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
                                    
                                    // Находим вопрос, к которому относится этот вариант
                                    var question = questionsFromDatabase.FirstOrDefault(q => q.Id == questionId);
                                    if (question != null)
                                    {
                                        // Создаем новый вариант
                                        var option = new TestOption
                                        {
                                            Id = optionId,
                                            Text = optionText,
                                            IsCorrect = isCorrect
                                        };
                                        
                                        // Загружаем изображение варианта, если оно есть
                                        if (!reader.IsDBNull(reader.GetOrdinal("option_image")))
                                        {
                                            byte[] imageData = (byte[])reader["option_image"];
                                            option.ImageData = imageData;
                                            
                                            // Создаем BitmapImage для отображения
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
                                        
                                        // Загружаем описание изображения
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

            // Отображаем изображение вопроса, если оно есть
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

            // Очищаем панель вариантов ответов
            OptionsPanel.Children.Clear();

            // Определяем, нужно ли использовать чекбоксы для множественного выбора
            bool useCheckboxes = false;
            
            // Проверяем тип опроса/теста
            if (_pollType != null)
            {
                // Если в типе опроса указано, что это множественный выбор
                if (_pollType.Contains("Множественный выбор"))
                {
                    useCheckboxes = true;
                }
                // Если это стандартный тест, но с несколькими правильными ответами
                else if (_pollType.Contains("Тест с вопросами и вариантами ответов") && 
                         currentQuestion.Options.Count(o => o.IsCorrect) > 1)
                {
                    useCheckboxes = true;
                }
            }

            // Создаем уникальное имя группы для RadioButton
            string radioGroupName = $"Question_{currentQuestion.Id}_Group";

            // Создаем варианты ответов
            foreach (var option in currentQuestion.Options)
            {
                // Создаем панель для варианта ответа (содержит текст и возможно изображение)
                StackPanel optionPanel = new StackPanel
                {
                    Margin = new Thickness(0, 5, 0, 15)
                };

                if (useCheckboxes)
                {
                    // Используем CheckBox для множественного выбора
                    TextBlock textBlock = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 22,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    
                    // Обрабатываем текст с внутренними изображениями
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

                    optionPanel.Children.Add(checkBox);
                }
                else
                {
                    // Используем RadioButton для одиночного выбора
                    TextBlock textBlock = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        LineHeight = 22,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    
                    // Обрабатываем текст с внутренними изображениями
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

                    // Добавляем RadioButton в группу для связывания кнопок
                    radioButton.GroupName = radioGroupName;

                    optionPanel.Children.Add(radioButton);
                }

                // Добавляем изображение к варианту ответа, если оно есть
                if (option.HasImage && option.OptionImage != null)
                {
                    // Создаем контейнер для изображения
                    Border imageBorder = new Border
                    {
                        BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#eee")),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(30, 10, 5, 10),
                        MaxWidth = 400
                    };

                    StackPanel imageStack = new StackPanel();
                    
                    // Добавляем изображение
                    Image optionImage = new Image
                    {
                        Source = option.OptionImage,
                        Stretch = System.Windows.Media.Stretch.Uniform,
                        MaxHeight = 200,
                        Margin = new Thickness(5)
                    };
                    imageStack.Children.Add(optionImage);
                    
                    // Добавляем описание изображения, если оно есть
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

                // Добавляем панель с вариантом ответа на главную панель
                OptionsPanel.Children.Add(optionPanel);
            }

            // Обновляем индикатор прогресса
            ProgressText.Text = $"Вопрос {_currentQuestionIndex + 1} из {_questions.Count}";
            ProgressBar.Maximum = _questions.Count;
            ProgressBar.Value = _currentQuestionIndex + 1;

            // Управляем видимостью кнопок
            PrevButton.IsEnabled = _currentQuestionIndex > 0;
            NextButton.Content = _currentQuestionIndex < _questions.Count - 1 ? "Следующий" : "Завершить";
        }

        /// <summary>
        /// Обрабатывает текст варианта ответа и добавляет встроенные изображения
        /// </summary>
        /// <param name="textBlock">TextBlock для отображения</param>
        /// <param name="text">Исходный текст</param>
        private void ProcessOptionText(TextBlock textBlock, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // Паттерн для поиска тегов изображений [img:URL]
            Regex imgRegex = new Regex(@"\[img:(.*?)\]", RegexOptions.IgnoreCase);
            
            int lastIndex = 0;
            foreach (Match match in imgRegex.Matches(text))
            {
                // Добавляем текст до изображения
                string textBeforeImg = text.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrEmpty(textBeforeImg))
                {
                    textBlock.Inlines.Add(new Run(textBeforeImg));
                }

                // Получаем URL изображения
                string imageUrl = match.Groups[1].Value.Trim();
                
                try
                {
                    // Создаем изображение
                    Image inlineImage = new Image();
                    inlineImage.MaxHeight = 20; // ограничиваем высоту встроенного изображения
                    inlineImage.VerticalAlignment = VerticalAlignment.Center;
                    inlineImage.Margin = new Thickness(2, 0, 2, 0);
                    
                    // Устанавливаем источник изображения
                    if (imageUrl.StartsWith("http"))
                    {
                        // Если это URL, используем его напрямую
                        inlineImage.Source = new BitmapImage(new Uri(imageUrl));
                    }
                    else if (File.Exists(imageUrl))
                    {
                        // Если это локальный файл
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(imageUrl, UriKind.RelativeOrAbsolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        inlineImage.Source = bitmap;
                    }
                    
                    // Добавляем изображение в текстовый блок
                    InlineUIContainer container = new InlineUIContainer(inlineImage);
                    textBlock.Inlines.Add(container);
                }
                catch (Exception ex)
                {
                    // Если не удалось загрузить изображение, добавляем сообщение об ошибке
                    textBlock.Inlines.Add(new Run($"[ошибка изображения: {ex.Message}]") 
                    { 
                        Foreground = Brushes.Red,
                        FontStyle = FontStyles.Italic
                    });
                }
                
                lastIndex = match.Index + match.Length;
            }
            
            // Добавляем оставшийся текст после последнего изображения
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
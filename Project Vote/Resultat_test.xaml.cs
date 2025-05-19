using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Project_Vote
{
    /// <summary>
    /// Логика взаимодействия для Resultat_test.xaml
    /// </summary>
    public partial class Resultat_test : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";
        private List<TestResultItem> allResults = new List<TestResultItem>();
        private List<ComboBoxItem> availableTests = new List<ComboBoxItem>();
        private List<ComboBoxItem> availableUsers = new List<ComboBoxItem>();
        private int? currentTestId = null;
        private int? currentUserId = null;
        private int? selectedResultId = null;

        // Модель для строки результата в DataGrid
        public class TestResultItem
        {
            public int Id { get; set; }
            public int TestId { get; set; }
            public string TestTitle { get; set; }
            public int? UserId { get; set; }
            public string UserName { get; set; }
            public DateTime DateTime { get; set; }
            public DateTime Date => DateTime.Date;
            public DateTime Time => DateTime;
            public int CorrectAnswers { get; set; }
            public int TotalQuestions { get; set; }
            public string Score => $"{CorrectAnswers}/{TotalQuestions}";
            public decimal ScorePercent { get; set; }
            public int DurationSeconds { get; set; }
            public bool Passed { get; set; }
        }

        // Модель для деталей ответа на вопрос
        public class AnswerDetailItem
        {
            public int QuestionNumber { get; set; }
            public string QuestionText { get; set; }
            public string SelectedAnswer { get; set; }
            public bool IsCorrect { get; set; }
            public Brush StatusColor => IsCorrect ?
                new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
        }

        public Resultat_test()
        {
            InitializeComponent();
            LoadResults();
            LoadFilters();
        }

        private void LoadResults(int? testId = null, int? userId = null)
        {
            allResults.Clear();
            selectedResultId = null;

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    // Проверяем существование таблицы test_results
                    if (!TableExists(connection, "test_results"))
                    {
                        MessageBox.Show("Таблица с результатами тестов не существует или пуста.",
                            "Нет данных", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    string query = @"
                        SELECT tr.*, p.title AS test_title, u.name AS user_name
                        FROM test_results tr
                        LEFT JOIN polls p ON tr.poll_id = p.id
                        LEFT JOIN voteuser.users u ON tr.user_id = u.id
                        WHERE 1=1";

                    List<MySqlParameter> parameters = new List<MySqlParameter>();

                    // Добавляем фильтры, если они указаны
                    if (testId.HasValue)
                    {
                        query += " AND tr.poll_id = @testId";
                        parameters.Add(new MySqlParameter("@testId", testId.Value));
                    }

                    if (userId.HasValue)
                    {
                        query += " AND tr.user_id = @userId";
                        parameters.Add(new MySqlParameter("@userId", userId.Value));
                    }

                    query += " ORDER BY tr.end_time DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        foreach (var param in parameters)
                        {
                            cmd.Parameters.Add(param);
                        }

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                TestResultItem item = new TestResultItem
                                {
                                    Id = reader.GetInt32("id"),
                                    TestId = reader.GetInt32("poll_id"),
                                    TestTitle = reader.IsDBNull(reader.GetOrdinal("test_title")) ?
                                        "Неизвестный тест" : reader.GetString("test_title"),
                                    UserId = reader.IsDBNull(reader.GetOrdinal("user_id")) ?
                                        null : (int?)reader.GetInt32("user_id"),
                                    UserName = reader.IsDBNull(reader.GetOrdinal("user_name")) ?
                                        (reader.IsDBNull(reader.GetOrdinal("user_name")) ?
                                            "Анонимный пользователь" : reader.GetString("user_name")) :
                                        reader.GetString("user_name"),
                                    DateTime = reader.GetDateTime("end_time"),
                                    CorrectAnswers = reader.GetInt32("correct_answers"),
                                    TotalQuestions = reader.GetInt32("total_questions"),
                                    ScorePercent = reader.GetDecimal("percentage_correct"),
                                    DurationSeconds = reader.GetInt32("duration_seconds"),
                                    Passed = reader.IsDBNull(reader.GetOrdinal("passed_status")) ?
                                        false : reader.GetBoolean("passed_status")
                                };
                                allResults.Add(item);
                            }
                        }
                    }
                }

                // Обновляем отображение
                ResultsDataGrid.ItemsSource = null;
                ResultsDataGrid.ItemsSource = allResults;

                // Очищаем детали
                ClearDetails();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке результатов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadFilters()
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    // Загружаем список тестов
                    string testsQuery = "SELECT id, title FROM polls WHERE poll_type = 'Тест с вопросами и вариантами ответов' ORDER BY title";
                    using (MySqlCommand cmd = new MySqlCommand(testsQuery, connection))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            availableTests.Clear();

                            // Добавляем пустой элемент "Все тесты"
                            availableTests.Add(new ComboBoxItem
                            {
                                Content = "Все тесты",
                                Tag = null
                            });

                            while (reader.Read())
                            {
                                int id = reader.GetInt32("id");
                                string title = reader.GetString("title");

                                availableTests.Add(new ComboBoxItem
                                {
                                    Content = title,
                                    Tag = id
                                });
                            }
                        }
                    }

                    // Загружаем список пользователей
                    string usersQuery = "SELECT id, name FROM voteuser.users ORDER BY name";
                    using (MySqlCommand cmd = new MySqlCommand(usersQuery, connection))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            availableUsers.Clear();

                            // Добавляем пустой элемент "Все пользователи"
                            availableUsers.Add(new ComboBoxItem
                            {
                                Content = "Все пользователи",
                                Tag = null
                            });

                            while (reader.Read())
                            {
                                int id = reader.GetInt32("id");
                                string name = reader.GetString("name");

                                availableUsers.Add(new ComboBoxItem
                                {
                                    Content = name,
                                    Tag = id
                                });
                            }
                        }
                    }
                }

                // Заполняем комбобоксы
                TestsComboBox.ItemsSource = availableTests;
                TestsComboBox.DisplayMemberPath = "Content";
                TestsComboBox.SelectedIndex = 0;

                UsersComboBox.ItemsSource = availableUsers;
                UsersComboBox.DisplayMemberPath = "Content";
                UsersComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке фильтров: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadResultDetails(int resultId)
        {
            selectedResultId = resultId;
            var selectedResult = allResults.FirstOrDefault(r => r.Id == resultId);

            if (selectedResult == null)
            {
                ClearDetails();
                return;
            }

            try
            {
                // Загружаем информацию о результате
                DetailTestName.Text = selectedResult.TestTitle;
                DetailUserName.Text = selectedResult.UserName;
                DetailDateTime.Text = selectedResult.DateTime.ToString("dd.MM.yyyy HH:mm:ss");

                // Форматируем длительность
                TimeSpan duration = TimeSpan.FromSeconds(selectedResult.DurationSeconds);
                string durationStr = duration.TotalHours >= 1
                    ? $"{(int)duration.TotalHours}ч {duration.Minutes}м {duration.Seconds}с"
                    : duration.Minutes > 0
                        ? $"{duration.Minutes}м {duration.Seconds}с"
                        : $"{duration.Seconds}с";
                DetailDuration.Text = durationStr;

                DetailCorrectCount.Text = selectedResult.CorrectAnswers.ToString();
                DetailTotalCount.Text = selectedResult.TotalQuestions.ToString();
                DetailScore.Text = $"{selectedResult.ScorePercent}% ({selectedResult.Score})";
                DetailStatus.Text = selectedResult.Passed ? "Пройден" : "Не пройден";

                // Обновляем визуализацию результатов
                DrawResultChart(selectedResult.ScorePercent);
                PercentText.Text = $"{selectedResult.ScorePercent}%";

                // Включаем кнопку "Пройти тест"
                TakeTestButton.Tag = selectedResult.TestId;
                TakeTestButton.IsEnabled = true;

                // Загружаем детали ответов пользователя
                LoadAnswerDetails(resultId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке деталей результата: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadAnswerDetails(int resultId)
        {
            List<AnswerDetailItem> answers = new List<AnswerDetailItem>();

            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    string query = @"
                        SELECT 
                            tra.question_id,
                            tra.selected_option_id,
                            tra.is_correct,
                            q.question_text,
                            q.question_order,
                            qo.option_text
                        FROM test_result_answers tra
                        JOIN questions q ON tra.question_id = q.id
                        LEFT JOIN question_options qo ON tra.selected_option_id = qo.id
                        WHERE tra.test_result_id = @resultId
                        ORDER BY q.question_order, q.id";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@resultId", resultId);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            int questionNumber = 1;
                            while (reader.Read())
                            {
                                string questionText = reader.GetString("question_text");
                                bool isCorrect = reader.GetBoolean("is_correct");
                                string optionText = reader.IsDBNull(reader.GetOrdinal("option_text"))
                                    ? "Не выбран ответ"
                                    : reader.GetString("option_text");

                                answers.Add(new AnswerDetailItem
                                {
                                    QuestionNumber = questionNumber++,
                                    QuestionText = questionText,
                                    SelectedAnswer = optionText,
                                    IsCorrect = isCorrect
                                });
                            }
                        }
                    }
                }

                // Обновляем таблицу ответов
                AnswersDataGrid.ItemsSource = null;
                AnswersDataGrid.ItemsSource = answers;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке деталей ответов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearDetails()
        {
            // Очищаем выделенную запись
            selectedResultId = null;

            // Очищаем информацию о результате
            DetailTestName.Text = "-";
            DetailUserName.Text = "-";
            DetailDateTime.Text = "-";
            DetailDuration.Text = "-";
            DetailCorrectCount.Text = "-";
            DetailTotalCount.Text = "-";
            DetailScore.Text = "-";
            DetailStatus.Text = "-";

            // Очищаем визуализацию
            PercentText.Text = "0%";
            DrawResultChart(0);

            // Отключаем кнопку "Пройти тест"
            TakeTestButton.Tag = null;
            TakeTestButton.IsEnabled = false;

            // Очищаем таблицу ответов
            AnswersDataGrid.ItemsSource = null;
        }

        private void DrawResultChart(decimal percent)
        {
            try
            {
                // Определяем цвет в зависимости от результата
                Color resultColor;
                if (percent >= 80)
                    resultColor = Colors.Green;
                else if (percent >= 60)
                    resultColor = Colors.LimeGreen;
                else if (percent >= 40)
                    resultColor = Colors.Orange;
                else
                    resultColor = Colors.Red;

                ResultArc.Fill = new SolidColorBrush(resultColor);

                // Создаем геометрию для круговой диаграммы
                double radius = 90;
                double centerX = 90;
                double centerY = 90;
                double angle = (double)(percent / 100m * 360);

                // Конвертируем угол в радианы для рисования дуги
                double radians = angle * Math.PI / 180;

                // Определяем, является ли дуга большой (более 180 градусов)
                bool isLargeArc = angle > 180;

                // Вычисляем конечную точку дуги
                double endX = centerX + radius * Math.Sin(radians);
                double endY = centerY - radius * Math.Cos(radians);

                // Создаем PathGeometry для дуги
                PathGeometry pathGeometry = new PathGeometry();
                PathFigure figure = new PathFigure();
                figure.StartPoint = new Point(centerX, centerY);

                // Добавляем линию от центра к началу дуги
                LineSegment lineToStart = new LineSegment(new Point(centerX, centerY - radius), true);
                figure.Segments.Add(lineToStart);

                // Добавляем дугу
                ArcSegment arc = new ArcSegment
                {
                    Size = new Size(radius, radius),
                    Point = new Point(endX, endY),
                    SweepDirection = SweepDirection.Clockwise,
                    IsLargeArc = isLargeArc,
                    RotationAngle = 0
                };
                figure.Segments.Add(arc);

                // Замыкаем фигуру
                figure.IsClosed = true;

                // Добавляем фигуру в геометрию
                pathGeometry.Figures.Add(figure);

                // Устанавливаем геометрию в Path
                ResultArc.Data = pathGeometry;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отрисовке диаграммы: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Проверка существования таблицы в базе данных
        private bool TableExists(MySqlConnection connection, string tableName)
        {
            string query = "SHOW TABLES LIKE @tableName";
            using (MySqlCommand cmd = new MySqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@tableName", tableName);
                object result = cmd.ExecuteScalar();
                return result != null;
            }
        }

        #region Event Handlers

        private void TestsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TestsComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                currentTestId = selectedItem.Tag as int?;
                LoadResults(currentTestId, currentUserId);
            }
        }

        private void UsersComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsersComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                currentUserId = selectedItem.Tag as int?;
                LoadResults(currentTestId, currentUserId);
            }
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            TestsComboBox.SelectedIndex = 0;
            UsersComboBox.SelectedIndex = 0;
            currentTestId = null;
            currentUserId = null;
            LoadResults();
        }

        private void ResultsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResultsDataGrid.SelectedItem is TestResultItem selectedResult)
            {
                LoadResultDetails(selectedResult.Id);
            }
            else
            {
                ClearDetails();
            }
        }

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag != null)
            {
                int resultId = Convert.ToInt32(button.Tag);

                // Находим и выделяем соответствующую строку в DataGrid
                for (int i = 0; i < ResultsDataGrid.Items.Count; i++)
                {
                    if (ResultsDataGrid.Items[i] is TestResultItem item && item.Id == resultId)
                    {
                        ResultsDataGrid.SelectedIndex = i;
                        ResultsDataGrid.ScrollIntoView(ResultsDataGrid.SelectedItem);
                        break;
                    }
                }

                LoadResultDetails(resultId);
            }
        }

        private void CloseDetails_Click(object sender, RoutedEventArgs e)
        {
            ClearDetails();
            ResultsDataGrid.SelectedItem = null;
        }

        private void TakeTest_Click(object sender, RoutedEventArgs e)
        {
            if (TakeTestButton.Tag != null)
            {
                int testId = Convert.ToInt32(TakeTestButton.Tag);
                var selectedTest = allResults.FirstOrDefault(r => r.TestId == testId);

                if (selectedTest != null)
                {
                    // Проверяем, требуется ли пароль для теста
                    string password = GetTestPassword(testId);

                    if (!string.IsNullOrEmpty(password))
                    {
                        // Показываем диалог ввода пароля
                        var passwordWindow = new PasswordPromptWindow(selectedTest.TestTitle, false);
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

                    // Открываем окно для прохождения теста
                    var testWindow = new TestPassingWindow(testId, selectedTest.TestTitle);
                    testWindow.Owner = this;

                    if (testWindow.ShowDialog() == true)
                    {
                        // После прохождения теста обновляем список результатов
                        LoadResults(currentTestId, currentUserId);
                    }
                }
            }
        }

        // Получение пароля теста из базы данных
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

        #endregion
    }
}

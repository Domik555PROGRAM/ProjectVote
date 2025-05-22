using MySql.Data.MySqlClient;
using Project_Vote.Models;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Project_Vote
{
    public partial class TEST_Results : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";
        private List<TestItem> userTests = new List<TestItem>();
        private List<TestResultItem> testResults = new List<TestResultItem>();
        private int? selectedTestId = null;
        private int? selectedResultId = null;

        public class TestItem
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public DateTime CreatedAt { get; set; }
            public int TotalPasses { get; set; }
            public decimal AverageScore { get; set; }
        }

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
        }

        public class AnswerDetailItem
        {
            public int QuestionNumber { get; set; }
            public string QuestionText { get; set; }
            public string SelectedAnswer { get; set; }
            public bool IsCorrect { get; set; }
            public Brush StatusColor => IsCorrect ?
                new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
        }

        public TEST_Results()
        {
            InitializeComponent();
            LoadUserTests();
        }

        private void LoadUserTests()
        {
            userTests.Clear();

            try
            {
                if (!CurrentUser.IsLoggedIn)
                {
                    MessageBox.Show("Необходимо войти в систему для просмотра результатов тестов.",
                        "Требуется авторизация", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close();
                    return;
                }

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    string query = @"
                        SELECT p.id, p.title, p.created_at,
                               (SELECT COUNT(*) FROM test_results WHERE poll_id = p.id) AS total_passes,
                               (SELECT AVG(percentage_correct) FROM test_results WHERE poll_id = p.id) AS average_score
                        FROM polls p
                        WHERE p.user_id = @userId 
                          AND p.poll_type = 'Тест с вопросами и вариантами ответов'
                        ORDER BY p.created_at DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@userId", CurrentUser.UserId);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                TestItem test = new TestItem
                                {
                                    Id = reader.GetInt32("id"),
                                    Title = reader.GetString("title"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    TotalPasses = reader.IsDBNull(reader.GetOrdinal("total_passes")) ?
                                        0 : reader.GetInt32("total_passes"),
                                    AverageScore = reader.IsDBNull(reader.GetOrdinal("average_score")) ?
                                        0 : reader.GetDecimal("average_score")
                                };

                                userTests.Add(test);
                            }
                        }
                    }
                }

                // Устанавливаем список тестов в ListView
                TestsListView.ItemsSource = userTests;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке тестов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TestsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TestsListView.SelectedItem is TestItem selectedTest)
            {
                // Проверяем, не тот же самый тест выбран повторно
                if (selectedTestId.HasValue && selectedTestId.Value == selectedTest.Id)
                {
                    // Если тот же тест, просто обновляем статистику без перезагрузки данных
                    return;
                }

                selectedTestId = selectedTest.Id;
                SelectedTestTitle.Text = selectedTest.Title;
                TestStatistics.Text = $"Всего прохождений: {selectedTest.TotalPasses}, Средний балл: {selectedTest.AverageScore:0.##}%";

                // Сбрасываем выбранный результат и скрываем панель деталей
                ClearResultDetails();

                // Загружаем результаты для выбранного теста
                LoadTestResults(selectedTest.Id);
            }
            else
            {
                SelectedTestTitle.Text = "Выберите тест из списка";
                TestStatistics.Text = "";
                UsersDataGrid.ItemsSource = null;
                ClearResultDetails();
            }
        }

        private void LoadTestResults(int testId)
        {
            // Очищаем предыдущие результаты
            testResults.Clear();

            // Сбрасываем ItemsSource для DataGrid для обеспечения полного обновления
            UsersDataGrid.ItemsSource = null;

            try
            {
                // Сначала проверяем, принадлежит ли тест текущему пользователю
                bool isUserTest = false;
                foreach (var test in userTests)
                {
                    if (test.Id == testId)
                    {
                        isUserTest = true;
                        break;
                    }
                }

                if (!isUserTest)
                {
                    MessageBox.Show("Вы можете просматривать результаты только своих тестов.",
                        "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    // Сначала проверим структуру таблицы test_results
                    Dictionary<string, bool> tableColumns = new Dictionary<string, bool>();
                    string checkTableQuery = "DESCRIBE test_results";

                    try
                    {
                        using (MySqlCommand cmd = new MySqlCommand(checkTableQuery, connection))
                        {
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string columnName = reader.GetString("Field");
                                    tableColumns[columnName.ToLower()] = true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при проверке структуры таблицы: {ex.Message}\n\n" +
                            "Таблица test_results может отсутствовать или иметь неверную структуру.",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Формируем гибкий запрос в зависимости от доступных столбцов
                    string dateColumn = "created_at"; // по умолчанию
                    if (tableColumns.ContainsKey("end_time"))
                        dateColumn = "end_time";
                    else if (tableColumns.ContainsKey("start_time"))
                        dateColumn = "start_time";

                    string query = $@"
                        SELECT tr.id, tr.poll_id, p.title AS test_title, 
                               tr.user_id, u.name AS user_name,
                               tr.{dateColumn} AS result_date, 
                               tr.correct_answers, tr.total_questions";

                    // Добавляем поле percentage_correct, если оно существует
                    if (tableColumns.ContainsKey("percentage_correct"))
                        query += ", tr.percentage_correct";
                    else
                        query += ", (tr.correct_answers / tr.total_questions * 100) AS percentage_correct";

                    // Добавляем поле duration_seconds, если оно существует
                    if (tableColumns.ContainsKey("duration_seconds"))
                        query += ", tr.duration_seconds";
                    else
                        query += ", 0 AS duration_seconds";

                    query += $@" 
                        FROM test_results tr
                        JOIN polls p ON tr.poll_id = p.id
                        LEFT JOIN voteuser.users u ON tr.user_id = u.id
                        WHERE tr.poll_id = @testId
                          AND p.user_id = @currentUserId
                        ORDER BY tr.{dateColumn} DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@testId", testId);
                        cmd.Parameters.AddWithValue("@currentUserId", CurrentUser.UserId);

                        try
                        {
                            using (MySqlDataReader reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    TestResultItem result = new TestResultItem
                                    {
                                        Id = reader.GetInt32("id"),
                                        TestId = reader.GetInt32("poll_id"),
                                        TestTitle = reader.GetString("test_title"),
                                        UserId = reader.IsDBNull(reader.GetOrdinal("user_id")) ?
                                            null : (int?)reader.GetInt32("user_id"),
                                        UserName = reader.IsDBNull(reader.GetOrdinal("user_name")) ?
                                            "Гость" : reader.GetString("user_name"),
                                        DateTime = reader.GetDateTime("result_date"),
                                        CorrectAnswers = reader.GetInt32("correct_answers"),
                                        TotalQuestions = reader.GetInt32("total_questions"),
                                        ScorePercent = Convert.ToDecimal(reader["percentage_correct"]),
                                        DurationSeconds = reader.GetInt32("duration_seconds")
                                    };

                                    testResults.Add(result);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка при чтении данных: {ex.Message}\n\n" +
                                "SQL запрос: " + query,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }
                }

                // Обновляем таблицу результатов
                UsersDataGrid.ItemsSource = testResults;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке результатов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UsersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Если в списке результатов что-то выбрано
            if (UsersDataGrid.SelectedItem is TestResultItem selectedResult)
            {
                selectedResultId = selectedResult.Id;
                LoadResultDetails(selectedResult);
            }
            else
            {
                // Если выбор отменен (например, при выборе нового теста)
                ClearResultDetails();
            }
        }

        private void LoadResultDetails(TestResultItem result)
        {
            try
            {
                DetailUserName.Text = result.UserName;
                DetailDateTime.Text = $"Пройден: {result.DateTime:dd.MM.yyyy HH:mm:ss}";
                DetailScore.Text = $"Результат: {result.Score} ({result.ScorePercent:0.##}%)";

                TimeSpan duration = TimeSpan.FromSeconds(result.DurationSeconds);
                string durationStr = duration.TotalHours >= 1
                    ? $"{(int)duration.TotalHours}ч {duration.Minutes}м {duration.Seconds}с"
                    : duration.Minutes > 0
                        ? $"{duration.Minutes}м {duration.Seconds}с"
                        : $"{duration.Seconds}с";

                PercentText.Text = $"{result.ScorePercent:0}%";
                DrawResultPie(result.ScorePercent);

                LoadAnswerDetails(result.Id);

                ResultDetailsPanel.Visibility = Visibility.Visible;
                NoSelectionText.Visibility = Visibility.Collapsed;
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

                    // Запрос на получение деталей ответов для конкретного результата
                    string query = @"
                        SELECT 
                            tua.question_id,
                            tua.option_id,
                            tua.is_correct,
                            q.question_text,
                            qo.option_text
                        FROM test_user_answers tua
                        JOIN questions q ON tua.question_id = q.id
                        LEFT JOIN question_options qo ON tua.option_id = qo.id
                        WHERE tua.user_id = (SELECT user_id FROM test_results WHERE id = @resultId)
                          AND tua.poll_id = (SELECT poll_id FROM test_results WHERE id = @resultId)
                        ORDER BY q.question_order, q.id";

                    using (MySqlCommand cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@resultId", resultId);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            int questionNumber = 0;
                            int lastQuestionId = -1;

                            while (reader.Read())
                            {
                                int questionId = reader.GetInt32("question_id");

                                // Если это новый вопрос, увеличиваем номер
                                if (questionId != lastQuestionId)
                                {
                                    questionNumber++;
                                    lastQuestionId = questionId;
                                }

                                string questionText = reader.GetString("question_text");
                                bool isCorrect = reader.GetBoolean("is_correct");
                                string optionText = reader.IsDBNull(reader.GetOrdinal("option_text")) ?
                                    "Не выбран ответ" : reader.GetString("option_text");

                                answers.Add(new AnswerDetailItem
                                {
                                    QuestionNumber = questionNumber,
                                    QuestionText = questionText,
                                    SelectedAnswer = optionText,
                                    IsCorrect = isCorrect
                                });
                            }
                        }
                    }
                }

                // Обновляем список ответов
                AnswersListView.ItemsSource = answers;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке деталей ответов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearResultDetails()
        {
            // Сбрасываем выбранный ID результата
            selectedResultId = null;

            // Очищаем ItemsSource для списка ответов
            AnswersListView.ItemsSource = null;

            // Сбрасываем выделение в таблице пользователей (если оно есть)
            if (UsersDataGrid != null && UsersDataGrid.SelectedItem != null)
            {
                UsersDataGrid.SelectedItem = null;
                UsersDataGrid.UnselectAll();
            }

            // Скрываем панель с деталями и показываем сообщение
            ResultDetailsPanel.Visibility = Visibility.Collapsed;
            NoSelectionText.Visibility = Visibility.Visible;
        }

        private void DrawResultPie(decimal percent)
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
                double radius = 50; // Радиус круга
                double centerX = 50; // Центральная точка X
                double centerY = 50; // Центральная точка Y
                double angle = (double)(percent / 100m * 360); // Угол в градусах

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
    }
}

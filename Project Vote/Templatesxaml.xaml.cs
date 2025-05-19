using MySql.Data.MySqlClient;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Project_Vote
{
    /// <summary>
    /// Логика взаимодействия для Templatesxaml.xaml
    /// </summary>
    public partial class Templatesxaml : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";

        public Templatesxaml()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            // Обработка клика по гиперссылке
            MessageBox.Show("Переход к дизайнам опросов");
        }

        private void Template_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //  текст и тег выбранного шаблона
            if (sender is TextBlock textBlock)
            {
                string templateName = textBlock.Text;
                string templateTag = textBlock.Tag as string ?? "";

                // тип шаблона по тегу
                bool isTest = templateTag.StartsWith("test_");
                bool isVote = templateTag.StartsWith("vote_");
                string templateType = isTest ? "тест" : (isVote ? "голосование" : "шаблон");

                // Проверяем, есть ли в базе данных тест/голосование с таким названием
                int? pollId = CheckPollExistsInDatabase(templateName, isTest ? "Тест с вопросами и вариантами ответов" : "Голосование");

                if (pollId.HasValue)
                {
                    // Тест/голосование существует, открываем его
                    if (isTest)
                    {
                        // Открываем тест
                        OpenTest(pollId.Value, templateName);
                    }
                    else if (isVote)
                    {
                        // Открываем голосование
                        OpenVoting(pollId.Value, templateName);
                    }
                }
                else
                {
                    // Тест/голосование не существует, спрашиваем, хочет ли пользователь создать новый
                    MessageBoxResult result = MessageBox.Show(
                        $"{templateType} с названием \"{templateName}\" не найден в базе данных.");
                    if (result == MessageBoxResult.Yes)
                    {
                        // Создаем новый тест/голосование на основе шаблона
                        if (isTest)
                        {
                            OpenCreateTestWindow(templateName);
                        }
                        else if (isVote)
                        {
                            OpenCreateVotingWindow(templateName);
                        }
                    }
                }
            }
        }

        private int? CheckPollExistsInDatabase(string title, string pollType)
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "SELECT id FROM polls WHERE title = @title AND poll_type = @pollType";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@title", title);
                        cmd.Parameters.AddWithValue("@pollType", pollType);

                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToInt32(result);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке существования теста/голосования: {ex.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return null;
        }

        private void OpenTest(int testId, string testName)
        {
            try
            {
                TestGolosovania testWindow = new TestGolosovania(testId, testName);
                testWindow.Owner = this;
                testWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии теста: {ex.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenVoting(int votingId, string votingName)
        {
            try
            {
                TestGolosovania votingWindow = new TestGolosovania(votingId, votingName);
                votingWindow.Owner = this;
                votingWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии голосования: {ex.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenCreateTestWindow(string testName)
        {
            try
            {
                Golos testWindow = new Golos();
                // Передаем название теста и устанавливаем соответствующий тип
                testWindow.SetTemplateValues(testName, "Тест с вопросами и вариантами ответов");
                testWindow.Owner = this;
                testWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании нового теста: {ex.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenCreateVotingWindow(string votingName)
        {
            try
            {
                Golos votingWindow = new Golos();
                // Передаем название голосования и устанавливаем соответствующий тип
                votingWindow.SetTemplateValues(votingName, "Голосование");
                votingWindow.Owner = this;
                votingWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании нового голосования: {ex.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

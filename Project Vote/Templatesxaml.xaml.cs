using MySql.Data.MySqlClient;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Project_Vote
{
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
            MessageBox.Show("Переход к дизайнам опросов");
        }

        private void Template_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                string templateName = textBlock.Text;
                string templateTag = textBlock.Tag as string ?? "";
                bool isTest = templateTag.StartsWith("test_");
                bool isVote = templateTag.StartsWith("vote_");
                string templateType = isTest ? "тест" : (isVote ? "голосование" : "шаблон");
                if (isTest)
                {
                    OpenCreateTestWindow(templateName);
                }
                else if (isVote)
                {
                    OpenCreateVotingWindow(templateName);
                }
                else
                {
                    MessageBox.Show($"Неизвестный тип шаблона: {templateTag}", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void OpenCreateTestWindow(string testName)
        {
            try
            {
                int? templatePollId = CheckPollExistsInDatabase(testName, "Тест с вопросами и вариантами ответов");
                Golos testWindow = new Golos();
                if (templatePollId.HasValue)
                {
                    testWindow.LoadTemplateData(templatePollId.Value); 
                }
                else
                {
                    testWindow.SetTemplateValues(testName, "Тест с вопросами и вариантами ответов");
                }
                testWindow.Owner = this;
                testWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании нового теста на основе шаблона: {ex.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenCreateVotingWindow(string votingName)
        {
            try
            {
                int? templatePollId = CheckPollExistsInDatabase(votingName, "Голосование");
                Golos votingWindow = new Golos();
                if (templatePollId.HasValue)
                {
                    votingWindow.LoadTemplateData(templatePollId.Value);
                }
                else
                {
                    votingWindow.SetTemplateValues(votingName, "Голосование");
                }
                votingWindow.Owner = this;
                votingWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании нового голосования на основе шаблона: {ex.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

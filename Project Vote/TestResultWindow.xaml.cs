using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Project_Vote
{
    public partial class TestResultWindow : Window
    {
        private List<TestQuestion> _questions;

        public TestResultWindow(string testTitle, int correctAnswers, int totalQuestions, double percentage, List<TestQuestion> questions)
        {
            InitializeComponent();

            _questions = questions;

            TestTitleText.Text = testTitle;
            CorrectAnswersText.Text = $"{correctAnswers} из {totalQuestions}";
            PercentageText.Text = $"{percentage:0.##}%";

            // Определяем оценку в зависимости от процента
            string grade;
            SolidColorBrush gradeBrush;

            if (percentage >= 90)
            {
                grade = "Отлично!";
                gradeBrush = new SolidColorBrush(Colors.Green);
            }
            else if (percentage >= 75)
            {
                grade = "Хорошо";
                gradeBrush = new SolidColorBrush(Colors.LimeGreen);
            }
            else if (percentage >= 60)
            {
                grade = "Удовлетворительно";
                gradeBrush = new SolidColorBrush(Colors.Orange);
            }
            else
            {
                grade = "Нужно еще поработать";
                gradeBrush = new SolidColorBrush(Colors.Red);
            }

            GradeText.Text = grade;
            GradeText.Foreground = gradeBrush;

            // Заполняем подробные результаты
            ShowDetailedResults();
        }

        private void ShowDetailedResults()
        {
            for (int i = 0; i < _questions.Count; i++)
            {
                var question = _questions[i];

                // Создаем заголовок вопроса
                TextBlock questionHeader = new TextBlock
                {
                    Text = $"Вопрос {i + 1}: {question.Text}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    Margin = new Thickness(0, 10, 0, 5),
                    TextWrapping = TextWrapping.Wrap
                };

                DetailedResultsPanel.Children.Add(questionHeader);

                // Добавляем варианты ответов
                foreach (var option in question.Options)
                {
                    StackPanel optionPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(10, 3, 0, 3)
                    };

                    // Иконка правильности
                    TextBlock icon = new TextBlock
                    {
                        FontFamily = new FontFamily("Segoe UI Symbol"),
                        FontSize = 14,
                        Width = 20,
                        Margin = new Thickness(0, 0, 5, 0)
                    };

                    // Текст варианта
                    TextBlock optionText = new TextBlock
                    {
                        Text = option.Text,
                        TextWrapping = TextWrapping.Wrap,
                        Width = 450
                    };

                    // Настраиваем отображение в зависимости от правильности и выбора
                    if (option.IsSelected)
                    {
                        if (option.IsCorrect)
                        {
                            // Правильный ответ и выбран
                            icon.Text = "✓";
                            icon.Foreground = new SolidColorBrush(Colors.Green);
                            optionText.FontWeight = FontWeights.Bold;
                        }
                        else
                        {
                            // Неправильный ответ и выбран
                            icon.Text = "✗";
                            icon.Foreground = new SolidColorBrush(Colors.Red);
                            optionText.FontWeight = FontWeights.Bold;
                            optionText.TextDecorations = TextDecorations.Strikethrough;
                        }
                    }
                    else
                    {
                        if (option.IsCorrect)
                        {
                            // Правильный ответ, но не выбран
                            icon.Text = "!";
                            icon.Foreground = new SolidColorBrush(Colors.Orange);
                            optionText.Foreground = new SolidColorBrush(Colors.Green);
                        }
                        else
                        {
                            // Неправильный ответ и не выбран
                            icon.Text = "";
                            optionText.Foreground = new SolidColorBrush(Colors.Gray);
                        }
                    }

                    optionPanel.Children.Add(icon);
                    optionPanel.Children.Add(optionText);

                    DetailedResultsPanel.Children.Add(optionPanel);
                }

                // Добавляем разделитель между вопросами
                if (i < _questions.Count - 1)
                {
                    DetailedResultsPanel.Children.Add(new Separator
                    {
                        Margin = new Thickness(0, 10, 0, 10),
                        Background = new SolidColorBrush(Color.FromRgb(200, 200, 200))
                    });
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
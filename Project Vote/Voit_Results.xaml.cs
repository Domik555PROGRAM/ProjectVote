using Microsoft.Win32;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Project_Vote
{
    public partial class Voit_Results : Window
    {
        private string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";
        private int _pollId;
        private string _pollTitle;
        private DateTime _pollDate;
        private int _totalVotes;
        private ObservableCollection<VoteResult> _results = new ObservableCollection<VoteResult>();
        private List<Color> chartColors = new List<Color>
        {
            Color.FromRgb(41, 128, 185),   // Синий
            Color.FromRgb(231, 76, 60),    // Красный
            Color.FromRgb(46, 204, 113),   // Зеленый
            Color.FromRgb(241, 196, 15),   // Желтый
            Color.FromRgb(155, 89, 182),   // Фиолетовый
            Color.FromRgb(52, 152, 219),   // Голубой
            Color.FromRgb(230, 126, 34),   // Оранжевый
            Color.FromRgb(149, 165, 166),  // Серый
            Color.FromRgb(26, 188, 156),   // Бирюзовый
            Color.FromRgb(192, 57, 43)     // Темно-красный
        };

        public class VoteResult
        {
            public string CandidateName { get; set; }
            public int VotesCount { get; set; }
            public double Percentage { get; set; }
            public SolidColorBrush Color { get; set; }
        }
        public Voit_Results(int pollId)
        {
            InitializeComponent();
            _pollId = pollId;
            LoadVotingResults();
        }
        public Voit_Results()
        {
            InitializeComponent();
        }

        private void LoadVotingResults()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    LoadPollInfo(conn);
                    LoadVotesData(conn);
                    UpdateUI();
                    DrawPieChart();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке результатов голосования: {ex.Message}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPollInfo(MySqlConnection conn)
        {
            string query = @"
                SELECT title, description, created_at 
                FROM polls
                WHERE id = @pollId";

            using (MySqlCommand cmd = new MySqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@pollId", _pollId);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        _pollTitle = reader.GetString("title");
                        _pollDate = reader.GetDateTime("created_at");
                    }
                    else
                    {
                        MessageBox.Show("Голосование не найдено",
                                      "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        Close();
                    }
                }
            }
        }

        private void LoadVotesData(MySqlConnection conn)
        {
            _results.Clear();
            string totalQuery = @"
                SELECT COUNT(*) 
                FROM vote_results
                WHERE poll_id = @pollId";

            using (MySqlCommand cmd = new MySqlCommand(totalQuery, conn))
            {
                cmd.Parameters.AddWithValue("@pollId", _pollId);
                _totalVotes = Convert.ToInt32(cmd.ExecuteScalar());
            }

            if (_totalVotes == 0)
            {
                return;
            }
            string resultsQuery = @"
                SELECT candidate_name, COUNT(*) as votes
                FROM vote_results
                WHERE poll_id = @pollId
                GROUP BY candidate_name
                ORDER BY votes DESC";

            using (MySqlCommand cmd = new MySqlCommand(resultsQuery, conn))
            {
                cmd.Parameters.AddWithValue("@pollId", _pollId);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    int colorIndex = 0;
                    while (reader.Read())
                    {
                        string candidateName = reader.GetString("candidate_name");
                        int votes = Convert.ToInt32(reader["votes"]);
                        double percentage = (double)votes / _totalVotes * 100;
                        Color color = chartColors[colorIndex % chartColors.Count];
                        colorIndex++;

                        _results.Add(new VoteResult
                        {
                            CandidateName = candidateName,
                            VotesCount = votes,
                            Percentage = percentage,
                            Color = new SolidColorBrush(color)
                        });
                    }
                }
            }
        }

        private void UpdateUI()
        {
            this.Title = $"Результаты голосования - {_pollTitle}";
            TitleText.Text = $"Результаты голосования";
            PollNameText.Text = _pollTitle;
            DateTimeText.Text = _pollDate.ToString("dd.MM.yyyy HH:mm:ss");

            if (_totalVotes == 0)
            {
                TotalVotesText.Text = "0";
                ExportButton.IsEnabled = false;
            }
            else
            {
                TotalVotesText.Text = $"{_totalVotes} {GetPersonsText(_totalVotes)}";
                ResultsDataGrid.ItemsSource = _results;
                ChartLegend.ItemsSource = _results;
                ExportButton.IsEnabled = true;
            }
        }

        private string GetPersonsText(int count)
        {
            int lastDigit = count % 10;
            int lastTwoDigits = count % 100;

            if (lastTwoDigits >= 11 && lastTwoDigits <= 19)
                return "человек";

            if (lastDigit == 1)
                return "человек";

            if (lastDigit >= 2 && lastDigit <= 4)
                return "человека";

            return "человек";
        }

        private void DrawPieChart()
        {
            if (_results.Count == 0 || _totalVotes == 0)
                return;
            ChartCanvas.Children.Clear();
            double canvasWidth = ChartCanvas.ActualWidth;
            double canvasHeight = ChartCanvas.ActualHeight;
            double radius = Math.Min(canvasWidth, canvasHeight) / 2 * 0.8;
            Point center = new Point(canvasWidth / 2, canvasHeight / 2);
            double startAngle = 0;
            foreach (var result in _results)
            {
                double sweepAngle = result.Percentage / 100 * 360;
                PathFigure figure = new PathFigure();
                figure.StartPoint = center;
                double endAngle = startAngle + sweepAngle;
                bool isLargeArc = sweepAngle > 180;

                Point arcStart = new Point(
                    center.X + radius * Math.Cos(startAngle * Math.PI / 180),
                    center.Y + radius * Math.Sin(startAngle * Math.PI / 180));

                Point arcEnd = new Point(
                    center.X + radius * Math.Cos(endAngle * Math.PI / 180),
                    center.Y + radius * Math.Sin(endAngle * Math.PI / 180));

                figure.Segments.Add(new LineSegment(arcStart, true));
                figure.Segments.Add(new ArcSegment(
                    arcEnd,
                    new Size(radius, radius),
                    0,
                    isLargeArc,
                    SweepDirection.Clockwise,
                    true));
                figure.Segments.Add(new LineSegment(center, true));
                PathGeometry geometry = new PathGeometry();
                geometry.Figures.Add(figure);

                System.Windows.Shapes.Path path = new System.Windows.Shapes.Path();
                path.Data = geometry;
                path.Fill = result.Color;
                path.Stroke = Brushes.White;
                path.StrokeThickness = 1;
                ToolTip tooltip = new ToolTip();
                tooltip.Content = $"{result.CandidateName}: {result.VotesCount} ({result.Percentage:N2}%)";
                path.ToolTip = tooltip;
                ChartCanvas.Children.Add(path);
                startAngle = endAngle;
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_results.Count == 0)
            {
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "CSV файл (*.csv)|*.csv|Текстовый файл (*.txt)|*.txt";
            saveFileDialog.Title = "Экспорт результатов голосования";
            saveFileDialog.FileName = $"Результаты_{_pollTitle}_{DateTime.Now:yyyyMMdd_HHmmss}";

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName, false, Encoding.UTF8))
                    {
                        writer.WriteLine($"Результаты голосования \"{_pollTitle}\"");
                        writer.WriteLine($"Дата проведения: {_pollDate:dd.MM.yyyy HH:mm:ss}");
                        writer.WriteLine($"Всего проголосовало: {_totalVotes} {GetPersonsText(_totalVotes)}");
                        writer.WriteLine();
                        writer.WriteLine("Кандидат;Количество голосов;Процент");
                        foreach (var result in _results)
                        {
                            writer.WriteLine($"{result.CandidateName};{result.VotesCount};{result.Percentage:N2}%");
                        }
                    }

                    MessageBox.Show("Экспорт успешно выполнен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при экспорте данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_pollId > 0)
            {
                LoadVotingResults();
            }
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DrawPieChart();
        }
    }
}

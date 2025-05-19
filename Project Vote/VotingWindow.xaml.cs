using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Project_Vote
{
    /// <summary>
    /// Логика взаимодействия для VotingWindow.xaml
    /// </summary>
    public partial class VotingWindow : Window
    {
        // Внутренний класс для хранения данных о кандидате
        public class Candidate
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public BitmapImage Image { get; set; }
            public bool HasImage => Image != null;
        }

        private ObservableCollection<Candidate> _candidates;

        // Конструктор по умолчанию
        public VotingWindow()
        {
            InitializeComponent();
            _candidates = new ObservableCollection<Candidate>();
            CandidatesItemsControl.ItemsSource = _candidates;
        }

        // Конструктор для предпросмотра голосования
        public VotingWindow(string title, string description, List<Golos.PollOption> options)
        {
            InitializeComponent();

            TitleTextBlock.Text = title;
            DescriptionTextBlock.Text = description;

            _candidates = new ObservableCollection<Candidate>();

            // Преобразуем опции из Golos в кандидатов
            foreach (var option in options)
            {
                // Проверяем и обновляем изображение, если необходимо
                if (option.HasImage && option.OptionImageSource == null && option.ImageData != null)
                {
                    option.UpdateImageSource();
                }

                var candidate = new Candidate
                {
                    Name = option.Text,
                    Description = option.ImageDescription,
                    Image = option.OptionImageSource
                };

                _candidates.Add(candidate);
            }

            CandidatesItemsControl.ItemsSource = _candidates;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

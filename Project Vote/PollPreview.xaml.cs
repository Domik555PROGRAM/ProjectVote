using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PollOptionGolos = Project_Vote.Golos.PollOption;
using QuestionGolos = Project_Vote.Golos.Question;

namespace Project_Vote
{
    // Конвертер для отображения/скрытия элементов с изображениями
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class PollPreview : Window
    {
        private string _pollType;
        private object _pollData;
        public PollPreview(string title, FlowDocument descriptionDocument, string pollType, object pollData)
        {
            InitializeComponent();
            TitleTextBlock.Text = title ?? "Без заголовка";
            if (descriptionDocument != null)
            {
                try
                {
                    var stream = new System.IO.MemoryStream();
                    TextRange source = new TextRange(descriptionDocument.ContentStart, descriptionDocument.ContentEnd);
                    source.Save(stream, DataFormats.XamlPackage);
                    TextRange dest = new TextRange(DescriptionRichTextBox.Document.ContentStart, DescriptionRichTextBox.Document.ContentEnd);
                    dest.Load(stream, DataFormats.XamlPackage);
                    stream.Close();
                }
                catch (Exception ex)
                {
                    DescriptionRichTextBox.Document.Blocks.Clear();
                    DescriptionRichTextBox.Document.Blocks.Add(new Paragraph(new Run($"Ошибка загрузки описания: {ex.Message}")));
                }
            }
            else
            {
                DescriptionRichTextBox.Document.Blocks.Clear();
                DescriptionRichTextBox.Document.Blocks.Add(new Paragraph(new Run("Нет описания.")));
            }
            _pollType = pollType;
            _pollData = pollData;

            // Подготовка изображений для предпросмотра
            PrepareImagesForPreview();

            Loaded += PollPreview_Loaded;
        }

        // Метод для подготовки изображений в предпросмотре
        private void PrepareImagesForPreview()
        {
            if (_pollType == "Тест с вопросами и вариантами ответов" && _pollData is ObservableCollection<QuestionGolos> questions)
            {
                foreach (var question in questions)
                {
                    // Загружаем изображение вопроса, если оно есть
                    if (question.HasImage && question.ImageData != null)
                    {
                        try
                        {
                            using (MemoryStream ms = new MemoryStream(question.ImageData))
                            {
                                BitmapImage bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.StreamSource = ms;
                                bitmap.EndInit();
                                bitmap.Freeze();

                                // Динамически добавляем свойство для привязки
                                AddDynamicPropertyIfNeeded(question, "QuestionImageSource");
                                question.GetType().GetProperty("QuestionImageSource")?.SetValue(question, bitmap);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке изображения вопроса: {ex.Message}");
                        }
                    }

                    // Загружаем изображения вариантов ответов
                    foreach (var option in question.Options)
                    {
                        if (option.HasImage && option.ImageData != null)
                        {
                            try
                            {
                                using (MemoryStream ms = new MemoryStream(option.ImageData))
                                {
                                    BitmapImage bitmap = new BitmapImage();
                                    bitmap.BeginInit();
                                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                    bitmap.StreamSource = ms;
                                    bitmap.EndInit();
                                    bitmap.Freeze();

                                    // Динамически добавляем свойство для привязки
                                    AddDynamicPropertyIfNeeded(option, "OptionImageSource");
                                    option.GetType().GetProperty("OptionImageSource")?.SetValue(option, bitmap);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке изображения варианта: {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

        // Метод для динамического добавления свойства
        private void AddDynamicPropertyIfNeeded(object obj, string propertyName)
        {
            var property = obj.GetType().GetProperty(propertyName);
            if (property == null)
            {
                // Если свойства нет, нельзя динамически добавить его
                // Это только проверка, для реального добавления нужно использовать
                // dynamic или расширить класс с этим свойством
                System.Diagnostics.Debug.WriteLine($"Свойство {propertyName} не найдено и не может быть добавлено динамически");
            }
        }
        private void PollPreview_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                switch (_pollType)
                {
                    case "Одиночный выбор (радиокнопки)":
                    case "Множественный выбор (флажки)":
                        PollItemsControl.ItemTemplate = (DataTemplate)PollItemsControl.FindResource("SimpleOptionTemplate");
                        PollItemsControl.ItemsSource = _pollData as ObservableCollection<PollOptionGolos>;
                        SetupSelectionControls(_pollType == "Множественный выбор (флажки)");
                        break;
                    case "Тест с вопросами и вариантами ответов":
                        PollItemsControl.ItemTemplate = (DataTemplate)PollItemsControl.FindResource("QuestionTemplate");
                        PollItemsControl.ItemsSource = _pollData as ObservableCollection<QuestionGolos>;
                        SetupTestSelectionControls();
                        break;
                    default:
                        PollItemsControl.ItemsSource = null;
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке шаблона предпросмотра: {ex.Message}", "Ошибка предпросмотра", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupSelectionControls(bool useCheckBox)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (PollItemsControl.ItemsSource == null) return;

                    foreach (var item in PollItemsControl.Items)
                    {
                        DependencyObject container = PollItemsControl.ItemContainerGenerator.ContainerFromItem(item);
                        if (container == null) continue;
                        ContentPresenter presenter = FindVisualChild<ContentPresenter>(container);
                        if (presenter == null && container is Border borderContainer)
                        {
                            presenter = FindVisualChild<ContentPresenter>(borderContainer);
                        }
                        if (presenter == null) continue;
                        presenter.ApplyTemplate();
                        ContentControl selectionControl = FindVisualChild<ContentControl>(presenter, "SelectionControl");
                        if (selectionControl != null)
                        {
                            if (useCheckBox)
                            {
                                CheckBox cb = new CheckBox();
                                if (PollItemsControl.TryFindResource("OptionCheckBoxStyle") is Style cbStyle)
                                {
                                    cb.Style = cbStyle;
                                }
                                selectionControl.Content = cb;
                            }
                            else
                            {
                                RadioButton rb = new RadioButton();
                                if (PollItemsControl.TryFindResource("OptionRadioButtonStyle") is Style rbStyle)
                                {
                                    rb.Style = rbStyle;
                                    rb.GroupName = "PreviewOptionsGroup";
                                }
                                selectionControl.Content = rb;
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Не удалось найти SelectionControl для элемента: {item}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка в SetupSelectionControls: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        private void SetupTestSelectionControls()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (PollItemsControl.ItemsSource == null) return;

                    foreach (QuestionGolos question in PollItemsControl.Items.OfType<QuestionGolos>())
                    {
                        DependencyObject questionContainer = PollItemsControl.ItemContainerGenerator.ContainerFromItem(question);
                        if (questionContainer == null) continue;
                        Border questionBorder = FindVisualChild<Border>(questionContainer);
                        if (questionBorder == null) continue;
                        ItemsControl optionsItemsControl = FindVisualChild<ItemsControl>(questionBorder, "QuestionOptionsItemsControl");
                        if (optionsItemsControl == null) continue;
                        optionsItemsControl.ApplyTemplate();
                        optionsItemsControl.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (optionsItemsControl.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                            {
                                System.Diagnostics.Debug.WriteLine($"Контейнеры для вариантов вопроса '{question.QuestionText}' еще не сгенерированы.");
                            }
                            int correctAnswersCount = question.Options?.Count(opt => opt.IsCorrect) ?? 0;
                            bool useCheckBoxForQuestion = correctAnswersCount > 1;
                            foreach (PollOptionGolos option in optionsItemsControl.Items.OfType<PollOptionGolos>())
                            {
                                DependencyObject optionContainer = optionsItemsControl.ItemContainerGenerator.ContainerFromItem(option);
                                if (optionContainer == null) continue;
                                ContentControl selectionControl = FindVisualChild<ContentControl>(optionContainer, "QuestionOptionSelectionControl");
                                if (selectionControl != null)
                                {
                                    if (useCheckBoxForQuestion)
                                    {
                                        CheckBox cb = new CheckBox();
                                        if (PollItemsControl.TryFindResource("OptionCheckBoxStyle") is Style cbStyle)
                                        {
                                            cb.Style = cbStyle;
                                        }
                                        selectionControl.Content = cb;
                                    }
                                    else
                                    {
                                        RadioButton rb = new RadioButton();
                                        if (PollItemsControl.TryFindResource("OptionRadioButtonStyle") is Style rbStyle)
                                        {
                                            rb.Style = rbStyle;
                                        }
                                        rb.GroupName = $"Question_{question.GetHashCode()}";
                                        selectionControl.Content = rb;
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Не удалось найти QuestionOptionSelectionControl для варианта: {option.Text} в вопросе: {question.QuestionText}");
                                }
                            }
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка в SetupTestSelectionControls: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
        public static T FindVisualChild<T>(DependencyObject parent, string childName = null) where T : DependencyObject
        {
            if (parent == null) return null;
            T foundChild = null;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                T childType = child as T;
                if (childType != null)
                {
                    if (!string.IsNullOrEmpty(childName))
                    {
                        var frameworkElement = child as FrameworkElement;
                        if (frameworkElement != null && frameworkElement.Name == childName)
                        {
                            foundChild = childType;
                            break;
                        }
                    }
                    else
                    {
                        foundChild = childType;
                        break;
                    }
                }
                foundChild = FindVisualChild<T>(child, childName);
                if (foundChild != null) break;
            }
            return foundChild;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}

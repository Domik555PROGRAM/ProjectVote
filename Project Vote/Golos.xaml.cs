using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MySql.Data.MySqlClient;
using System.Runtime.InteropServices;
using Project_Vote.Models;

namespace Project_Vote
{
    /// <summary>
    /// Логика взаимодействия для Golos.xaml
    /// </summary>
    public partial class Golos : Window
    {
        private Dictionary<Type, Window> _openWindows = new Dictionary<Type, Window>();
        private string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";

        [DllImport("user32.dll")]
        private static extern int ToUnicodeEx(
            uint wVirtKey,
            uint wScanCode,
            byte[] lpKeyState,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
            int cchBuff,
            uint wFlags,
            IntPtr dwhkl);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        private static IntPtr _currentKeyboardLayout = GetKeyboardLayout(0);

        private void OpenTemplatesWindow_Click(object sender, RoutedEventArgs e)
        {
            if (_openWindows.TryGetValue(typeof(Templatesxaml), out Window existingWindow))
            {
                existingWindow.Close();
                _openWindows.Remove(typeof(Templatesxaml));
            }

            Templatesxaml newWindow = new Templatesxaml();
            newWindow.Owner = this;

            newWindow.Closed += (s, args) => _openWindows.Remove(typeof(Templatesxaml));

            _openWindows[typeof(Templatesxaml)] = newWindow;

            newWindow.Show();
        }
        // Коллекция вариантов ответа
        private ObservableCollection<PollOption> _pollOptions;

        // Класс для хранения данных опроса
        private Poll _currentPoll;

        // Переменные для отслеживания состояния форматирования
        private bool _isBoldActive = false;
        private bool _isItalicActive = false;
        private bool _isUnderlineActive = false;


        public class Question
        {
            public string QuestionText { get; set; }
            public ObservableCollection<PollOption> Options { get; set; } = new ObservableCollection<PollOption>();
        }
        private ObservableCollection<Question> _questions = new ObservableCollection<Question>();
        private int _editingPollId = -1;
        private bool _isEditingMode = false;
        
        public Golos()
        {
            _currentPoll = new Poll();
            _pollOptions = new ObservableCollection<PollOption>
            {
                new PollOption { Text = "Вариант 1" },
                new PollOption { Text = "Вариант 2" },
                new PollOption { Text = "Вариант 3" }
            };
            InitializeComponent();
            DataContext = _currentPoll;
            InitializeRichTextBox();
            QuestionsItemsControl.ItemsSource = _questions;
        }

        private void InitializeRichTextBox()
        {
            Paragraph paragraph = new Paragraph();
            DescriptionRichTextBox.Document = new FlowDocument(paragraph);
            DescriptionRichTextBox.TextChanged += DescriptionRichTextBox_TextChanged;
            DescriptionRichTextBox.PreviewKeyDown += DescriptionRichTextBox_PreviewKeyDown;
            DataObject.AddPastingHandler(DescriptionRichTextBox, OnPaste);
        }
        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(DataFormats.Text))
            {
                string text = (string)e.DataObject.GetData(DataFormats.Text);
                TextPointer insertionPosition = DescriptionRichTextBox.Selection.Start;
                InsertFormattedText(insertionPosition, text);
                e.CancelCommand();
            }
        }


        private void InsertFormattedText(TextPointer position, string text)
        {
            Run run = new Run(text);
            if (_isBoldActive)
                run.FontWeight = FontWeights.Bold;
            if (_isItalicActive)
                run.FontStyle = FontStyles.Italic;
            if (_isUnderlineActive)
                run.TextDecorations = TextDecorations.Underline;
            position.Paragraph.Inlines.Add(run);
            TextPointer newPosition = run.ContentEnd;
            DescriptionRichTextBox.Selection.Select(newPosition, newPosition);
        }


        private void BoldButton_Click(object sender, RoutedEventArgs e)
        {
            _isBoldActive = !_isBoldActive;
            if (!DescriptionRichTextBox.Selection.IsEmpty)
            {
                TextRange selection = new TextRange(
                    DescriptionRichTextBox.Selection.Start,
                    DescriptionRichTextBox.Selection.End);
                if (_isBoldActive)
                    selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                else
                    selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
            }
            DescriptionRichTextBox.Focus();
        }

        private void ItalicButton_Click(object sender, RoutedEventArgs e)
        {
            _isItalicActive = !_isItalicActive;
            if (!DescriptionRichTextBox.Selection.IsEmpty)
            {
                TextRange selection = new TextRange(
                    DescriptionRichTextBox.Selection.Start,
                    DescriptionRichTextBox.Selection.End);

                if (_isItalicActive)
                    selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Italic);
                else
                    selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
            }
            DescriptionRichTextBox.Focus();
        }

        private void UnderlineButton_Click(object sender, RoutedEventArgs e)
        {
            _isUnderlineActive = !_isUnderlineActive;
            if (!DescriptionRichTextBox.Selection.IsEmpty)
            {
                TextRange selection = new TextRange(
                    DescriptionRichTextBox.Selection.Start,
                    DescriptionRichTextBox.Selection.End);

                if (_isUnderlineActive)
                    selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
                else
                    selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
            }
            DescriptionRichTextBox.Focus();
        }


        private void DescriptionRichTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {

        }


        private char? GetCharacterFromKey(Key key, bool shift)
        {
            uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            StringBuilder charBuffer = new StringBuilder(5);
            byte[] keyboardState = new byte[256];

            if (shift)
                keyboardState[(int)Key.LeftShift] = 0x80;

            int result = ToUnicodeEx(
                virtualKey,
                0,
                keyboardState,
                charBuffer,
                charBuffer.Capacity,
                0,
                _currentKeyboardLayout);

            if (result == 1)
            {
                return charBuffer[0];
            }

            switch (key)
            {
                case Key.Space:
                    return ' ';
                case Key.OemComma:
                    return shift ? '<' : ',';
                case Key.OemPeriod:
                    return shift ? '>' : '.';
                case Key.OemQuestion:
                    return shift ? '?' : '/';
                case Key.OemSemicolon:
                    return shift ? ':' : ';';
                case Key.OemQuotes:
                    return shift ? '"' : '\'';
                case Key.OemOpenBrackets:
                    return shift ? '{' : '[';
                case Key.OemCloseBrackets:
                    return shift ? '}' : ']';
                case Key.OemPipe:
                    return shift ? '|' : '\\';
                case Key.OemMinus:
                    return shift ? '_' : '-';
                case Key.OemPlus:
                    return shift ? '+' : '=';
            }

            return null;
        }


        private bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl ||
                   key == Key.LeftShift || key == Key.RightShift ||
                   key == Key.LeftAlt || key == Key.RightAlt ||
                   key == Key.LWin || key == Key.RWin ||
                   key == Key.CapsLock;
        }


        private bool IsControlKey(Key key)
        {
            return key == Key.Tab || key == Key.CapsLock ||
                   key == Key.Up || key == Key.Down ||
                   key == Key.Left || key == Key.Right ||
                   key == Key.Home || key == Key.End ||
                   key == Key.PageUp || key == Key.PageDown ||
                   key == Key.Insert || key == Key.Escape ||
                   key == Key.Enter;
        }


        private void DescriptionRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_currentPoll == null)
            {
                _currentPoll = new Poll();
                DataContext = _currentPoll;
            }

            TextRange textRange = new TextRange(
                DescriptionRichTextBox.Document.ContentStart,
                DescriptionRichTextBox.Document.ContentEnd);

            _currentPoll.Description = textRange.Text?.Trim() ?? string.Empty;
        }

        private void TitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

            if (_currentPoll == null)
            {
                _currentPoll = new Poll();
                DataContext = _currentPoll;
            }

            if (TitleTextBox == null) return;

            int length = TitleTextBox.Text?.Length ?? 0;

            if (TitleCharCountText != null)
                TitleCharCountText.Text = $"{length}/200";

            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                if (TitleErrorText != null)
                    TitleErrorText.Visibility = Visibility.Visible;
            }
            else
            {
                if (TitleErrorText != null)
                    TitleErrorText.Visibility = Visibility.Collapsed;

                _currentPoll.Title = TitleTextBox.Text;
            }
        }

        private void PollTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            if (QuestionsPanel == null)
            {
                return;
            }

            if (PollTypeComboBox.SelectedIndex == 2)
            {
                QuestionsPanel.Visibility = Visibility.Visible;
            }
            else
            {
                QuestionsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void AddQuestion_Click(object sender, RoutedEventArgs e)
        {
            _questions.Add(new Question());
        }

        private void AddOption_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var question = button.DataContext as Question;
            question?.Options.Add(new PollOption());
        }

        private void RemoveOption_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var option = button.Tag as PollOption;
            var question = _questions.FirstOrDefault(q => q.Options.Contains(option));
            question?.Options.Remove(option);
        }

        private bool CreatePollTableIfNotExists(MySqlConnection connection)
        {
            try
            {
                string checkTableQuery = "SHOW TABLES LIKE 'polls'";
                using (MySqlCommand cmd = new MySqlCommand(checkTableQuery, connection))
                {
                    object result = cmd.ExecuteScalar();
                    if (result == null)
                    {

                        string createTableQuery = @"
                            CREATE TABLE polls (
                                id INT AUTO_INCREMENT PRIMARY KEY,
                                user_id INT,                 -- ID пользователя, создавшего опрос
                                title VARCHAR(255) NOT NULL,
                                description TEXT,
                                poll_type VARCHAR(50),
                                created_at DATETIME,
                                is_active BOOLEAN,
                                options TEXT
                                -- УДАЛЕНА СТРОКА: FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL
                            )";
                        using (MySqlCommand createCmd = new MySqlCommand(createTableQuery, connection))
                        {
                            createCmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        string checkColumnQuery = "SHOW COLUMNS FROM polls LIKE 'user_id'";
                        using (MySqlCommand checkColCmd = new MySqlCommand(checkColumnQuery, connection))
                        {
                            object colResult = checkColCmd.ExecuteScalar();
                            if (colResult == null)
                            {

                                string addColumnQuery = @"
                                    ALTER TABLE polls
                                    ADD COLUMN user_id INT";

                                using (MySqlCommand addColCmd = new MySqlCommand(addColumnQuery, connection))
                                {
                                    addColCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {

                MessageBox.Show($"Ошибка при проверке/создании таблицы polls или столбца user_id: {ex.Message}", "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }


        private bool CheckIfPollTitleExists(string title)
        {
            try
            {

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string checkQuery = "SELECT COUNT(*) FROM polls WHERE title = @title";
                    using (MySqlCommand cmd = new MySqlCommand(checkQuery, conn))
                    {

                        cmd.Parameters.AddWithValue("@title", title);

                        long count = (long)cmd.ExecuteScalar();

                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {

                MessageBox.Show($"Ошибка при проверке заголовка в базе данных: {ex.Message}", "Ошибка проверки", MessageBoxButton.OK, MessageBoxImage.Error);
                return true;
            }
        }

        private bool SavePollToDatabase()
        {

            if (!CurrentUser.IsLoggedIn)
            {
                MessageBox.Show("Необходимо войти в систему, чтобы сохранить опрос.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();


                    if (!CreatePollTableIfNotExists(conn))
                    {
                        return false;
                    }


                    string insertQuery = "INSERT INTO polls (user_id, title, description, poll_type, created_at, is_active, options) VALUES (@user_id, @title, @description, @poll_type, @created_at, @is_active, @options)";
                    using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                    {
                        // Добавляем параметр user_id
                        cmd.Parameters.AddWithValue("@user_id", CurrentUser.UserId);
                        cmd.Parameters.AddWithValue("@title", _currentPoll.Title);
                        cmd.Parameters.AddWithValue("@description", _currentPoll.Description);
                        cmd.Parameters.AddWithValue("@poll_type", _currentPoll.PollType);
                        cmd.Parameters.AddWithValue("@created_at", _currentPoll.CreatedAt);
                        cmd.Parameters.AddWithValue("@is_active", _currentPoll.IsActive);
                        cmd.Parameters.AddWithValue("@options", _currentPoll.Options != null ? string.Join("|||", _currentPoll.Options) : string.Empty);

                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool UpdatePollInDatabase()
        {
            if (!CurrentUser.IsLoggedIn)
            {
                MessageBox.Show("Необходимо войти в систему, чтобы обновить опрос.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Проверяем, существует ли опрос с таким ID и принадлежит ли он текущему пользователю
                    string checkQuery = "SELECT COUNT(*) FROM polls WHERE id = @id AND user_id = @user_id";
                    using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@id", _editingPollId);
                        checkCmd.Parameters.AddWithValue("@user_id", CurrentUser.UserId);

                        long count = (long)checkCmd.ExecuteScalar();
                        if (count == 0)
                        {
                            MessageBox.Show("Вы не можете редактировать этот опрос, так как он не принадлежит вам или был удален.",
                                "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }
                    }

                    // Обновляем опрос
                    string updateQuery = @"
                        UPDATE polls 
                        SET title = @title, 
                            description = @description, 
                            poll_type = @poll_type, 
                            options = @options 
                        WHERE id = @id";

                    using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", _editingPollId);
                        cmd.Parameters.AddWithValue("@title", _currentPoll.Title);
                        cmd.Parameters.AddWithValue("@description", _currentPoll.Description);
                        cmd.Parameters.AddWithValue("@poll_type", _currentPoll.PollType);
                        cmd.Parameters.AddWithValue("@options", _currentPoll.Options != null ? string.Join("|||", _currentPoll.Options) : string.Empty);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении опроса: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {

            string pollTitle = TitleTextBox.Text;
            if (string.IsNullOrWhiteSpace(pollTitle))
            {
                MessageBox.Show("Пожалуйста, введите заголовок опроса/теста перед сохранением.",
                                "Заголовок не указан", MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleTextBox.Focus();
                return;
            }

            if (CheckIfPollTitleExists(pollTitle))
            {
                MessageBox.Show($"Опрос или тест с заголовком \"{pollTitle}\" уже существует. Пожалуйста, введите другой заголовок.",
                                "Заголовок уже используется", MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleTextBox.Focus();
                TitleTextBox.SelectAll();
                return;
            }




            if (_currentPoll == null)
            {
                _currentPoll = new Poll();
            }


            _currentPoll.Title = pollTitle;


            if (DescriptionRichTextBox != null)
            {
                TextRange textRange = new TextRange(
                    DescriptionRichTextBox.Document.ContentStart,
                    DescriptionRichTextBox.Document.ContentEnd);

                _currentPoll.Description = textRange.Text?.Trim() ?? string.Empty;
            }

            if (PollTypeComboBox?.SelectedItem != null)
                _currentPoll.PollType = ((ComboBoxItem)PollTypeComboBox.SelectedItem).Content?.ToString() ?? "Одиночный выбор (радиокнопки)";


            if (_currentPoll.PollType == "Тест с вопросами и вариантами ответов")
            {
                MessageBox.Show("Сохранение тестов пока не реализовано в базе данных.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                if (_currentPoll.Options == null)
                    _currentPoll.Options = new List<string>();
                else
                    _currentPoll.Options.Clear();
            }
            else
            {
                if (_currentPoll.Options == null)
                    _currentPoll.Options = new List<string>();
                else
                    _currentPoll.Options.Clear();
                if (_pollOptions != null)
                {
                    foreach (var option in _pollOptions)
                    {
                        if (!string.IsNullOrWhiteSpace(option?.Text))
                            _currentPoll.Options.Add(option.Text);
                    }
                }
                if (!_currentPoll.Options.Any())
                {
                    MessageBox.Show("Нельзя сохранить опрос без вариантов ответов.", "Нет вариантов", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            _currentPoll.CreatedAt = DateTime.Now;
            _currentPoll.IsActive = true;

            bool success;
            if (_isEditingMode && _editingPollId > 0)
            {
                success = UpdatePollInDatabase();
            }
            else
            {
                success = SavePollToDatabase();
            }

            if (success)
            {
                if (_isEditingMode)
                    MessageBox.Show("Опрос успешно обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                else
                    MessageBox.Show("Опрос успешно сохранен в базе данных!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                
                Close();
            }
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            string title = TitleTextBox.Text;
            FlowDocument descriptionDoc = DescriptionRichTextBox.Document;
            string pollType = ((ComboBoxItem)PollTypeComboBox.SelectedItem)?.Content?.ToString() ?? "Одиночный выбор (радиокнопки)";
            object pollData = null;

            switch (pollType)
            {
                case "Одиночный выбор (радиокнопки)":
                case "Множественный выбор (флажки)":
                    pollData = _pollOptions;
                    break;
                case "Тест с вопросами и вариантами ответов":

                    pollData = _questions;
                    break;
                default:

                    break;
            }

            try
            {
                PollPreview previewWindow = new PollPreview(title, descriptionDoc, pollType, pollData);
                previewWindow.Owner = this;
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии предпросмотра: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Вы уверены, что хотите отменить создание опроса? Все несохраненные данные будут утеряны.", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Close();
            }
        }
        public void LoadPollForEditing(int pollId)
        {
            _editingPollId = pollId;
            _isEditingMode = true;

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "SELECT title, description, poll_type, options FROM polls WHERE id = @id";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", pollId);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Заполняем поля формы данными из БД
                                string title = reader.GetString("title");
                                string description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description");
                                string pollType = reader.IsDBNull(reader.GetOrdinal("poll_type")) ? "Одиночный выбор (радиокнопки)" : reader.GetString("poll_type");
                                string options = reader.IsDBNull(reader.GetOrdinal("options")) ? "" : reader.GetString("options");

                                // Заполняем заголовок
                                TitleTextBox.Text = title;

                                // Заполняем описание
                                DescriptionRichTextBox.Document.Blocks.Clear();
                                DescriptionRichTextBox.Document.Blocks.Add(new Paragraph(new Run(description)));

                                // Выбираем тип опроса
                                switch (pollType)
                                {
                                    case "Множественный выбор (флажки)":
                                        PollTypeComboBox.SelectedIndex = 1;
                                        break;
                                    case "Тест с вопросами и вариантами ответов":
                                        PollTypeComboBox.SelectedIndex = 2;
                                        break;
                                    default:
                                        PollTypeComboBox.SelectedIndex = 0;
                                        break;
                                }

                                // Заполняем варианты ответов
                                _pollOptions.Clear();
                                if (!string.IsNullOrEmpty(options))
                                {
                                    string[] optionArray = options.Split(new[] { "|||" }, StringSplitOptions.None);
                                    foreach (var option in optionArray)
                                    {
                                        if (!string.IsNullOrEmpty(option))
                                            _pollOptions.Add(new PollOption { Text = option });
                                    }
                                }

                                // Если список вариантов пуст, добавляем несколько пустых вариантов
                                if (_pollOptions.Count == 0)
                                {
                                    _pollOptions.Add(new PollOption { Text = "Вариант 1" });
                                    _pollOptions.Add(new PollOption { Text = "Вариант 2" });
                                    _pollOptions.Add(new PollOption { Text = "Вариант 3" });
                                }

                                // Обновляем заголовок окна
                                this.Title = "Редактирование опроса";
                            }
                            else
                            {
                                MessageBox.Show("Опрос не найден в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                Close();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке опроса: {ex.Message}", "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        public class PollOption
        {
            public string Text { get; set; }
            public bool IsCorrect { get; set; }
        }
        public class Poll
        {
            private List<string> _options;

            public Poll()
            {
                _options = new List<string>();
                Title = string.Empty;
                Description = string.Empty;
                Language = "Русский";
                PollType = "Одиночный выбор (радиокнопки)";
                CreatedAt = DateTime.Now;
                IsActive = true;
            }

            public string Title { get; set; }
            public string Description { get; set; }
            public string Language { get; set; }
            public string PollType { get; set; }
            public List<string> Options
            {
                get { return _options; }
                set { _options = value ?? new List<string>(); }
            }
            public DateTime CreatedAt { get; set; }
            public bool IsActive { get; set; }
        }
    }
}
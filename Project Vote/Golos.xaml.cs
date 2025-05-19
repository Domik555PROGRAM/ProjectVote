using Microsoft.Win32;
using MySql.Data.MySqlClient;
using Project_Vote.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Project_Vote
{
    // Конвертер для проверки строки на пустоту
    public class StringNotEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return !string.IsNullOrWhiteSpace(str);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

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
        private ObservableCollection<PollOption> _pollOptions;
        private Poll _currentPoll;
        private bool _isBoldActive = false;
        private bool _isItalicActive = false;
        private bool _isUnderlineActive = false;
        private bool _isPasswordVisible = false;

        public class Question
        {
            public string QuestionText { get; set; }
            public ObservableCollection<PollOption> Options { get; set; } = new ObservableCollection<PollOption>();
            public byte[] ImageData { get; set; }
            public string ImageDescription { get; set; }
            public bool HasImage => ImageData != null && ImageData.Length > 0;
            public BitmapImage QuestionImageSource { get; set; }
        }
        private ObservableCollection<Question> _questions = new ObservableCollection<Question>();
        private int _editingPollId = -1;
        private bool _isEditingMode = false;

        public class PollOption
        {
            public string Text { get; set; }
            public bool IsCorrect { get; set; }
            public byte[] ImageData { get; set; }
            public string ImageDescription { get; set; }
            public bool HasImage => ImageData != null && ImageData.Length > 0;
            public BitmapImage OptionImageSource { get; set; }

            // Метод для обновления OptionImageSource из ImageData
            public void UpdateImageSource()
            {
                if (ImageData != null && ImageData.Length > 0)
                {
                    try
                    {
                        // Создаем BitmapImage для отображения
                        using (MemoryStream ms = new MemoryStream(ImageData))
                        {
                            BitmapImage bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = ms;
                            bitmap.EndInit();
                            bitmap.Freeze();

                            OptionImageSource = bitmap;
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки при создании изображения
                        OptionImageSource = null;
                    }
                }
                else
                {
                    OptionImageSource = null;
                }
            }
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
                EndDate = DateTime.Now.AddDays(7); // По умолчанию опрос активен 7 дней
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
            public DateTime EndDate { get; set; } // Дата окончания опроса/теста
            public bool IsActive { get; set; }
        }

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

            // Устанавливаем дату окончания по умолчанию (текущая дата + 7 дней)
            EndDatePicker.SelectedDate = DateTime.Now.AddDays(7);

            DataContext = _currentPoll;
            OptionsListBox.ItemsSource = _pollOptions;

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
            if (QuestionsPanel == null || OptionsPanel == null)
            {
                return;
            }

            // Получаем выбранный тип опроса
            string selectedType = ((ComboBoxItem)PollTypeComboBox.SelectedItem)?.Content?.ToString() ?? "";

            // Скрываем все панели по умолчанию
            QuestionsPanel.Visibility = Visibility.Collapsed;
            OptionsPanel.Visibility = Visibility.Collapsed;

            // Находим TextBlock с заголовком вариантов
            var titleBlock = OptionsPanel.Children[0] as TextBlock;
            // Находим кнопку добавления вариантов
            var addButton = AddOptionButton;

            // Настраиваем интерфейс в зависимости от выбранного типа опроса
            switch (selectedType)
            {
                case "Тест с вопросами и вариантами ответов":
                    QuestionsPanel.Visibility = Visibility.Visible;
                    break;

                case "Голосование":
                    OptionsPanel.Visibility = Visibility.Visible;

                    if (titleBlock != null)
                        titleBlock.Text = "Кандидаты для голосования";

                    // Изменяем текст на кнопке добавления
                    if (addButton != null)
                        addButton.Content = "Добавить кандидата";
                    break;

                default: // Одиночный выбор или Множественный выбор
                    OptionsPanel.Visibility = Visibility.Visible;
                    // Возвращаем исходный текст
                    if (titleBlock != null)
                        titleBlock.Text = "Варианты ответа";

                    // Возвращаем исходный текст на кнопке
                    if (addButton != null)
                        addButton.Content = "Добавить вариант";
                    break;
            }
        }

        private void AddQuestion_Click(object sender, RoutedEventArgs e)
        {
            _questions.Add(new Question());
        }

        private void AddOption_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            // Проверяем, является ли кнопка частью вопроса
            var question = button?.DataContext as Question;

            if (question != null)
            {
                // Кнопка добавления вариантов ответа для вопроса
                question.Options.Add(new PollOption { Text = "Новый вариант ответа" });
            }
            else
            {
                // Кнопка добавления кандидата или варианта для обычного опроса
                if (_pollOptions == null)
                {
                    _pollOptions = new ObservableCollection<PollOption>();
                }

                // Определяем тип опроса
                string selectedType = ((ComboBoxItem)PollTypeComboBox.SelectedItem)?.Content?.ToString() ?? "";

                // Создаем новый вариант/кандидата в зависимости от типа опроса
                string defaultText = (selectedType == "Голосование") ? "Новый кандидат" : "Новый вариант ответа";
                var newOption = new PollOption { Text = defaultText };
                _pollOptions.Add(newOption);

                // Настраиваем список для отображения
                if (OptionsListBox != null && OptionsListBox.ItemsSource == null)
                {
                    OptionsListBox.ItemsSource = _pollOptions;
                }
            }
        }

        private void RemoveOption_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var option = button?.Tag as PollOption;

            if (option == null)
                return;

            // Проверяем, является ли вариант частью вопроса или основного списка
            var question = _questions.FirstOrDefault(q => q.Options.Contains(option));

            if (question != null)
            {
                // Удаляем из вопроса
                question.Options.Remove(option);
            }
            else if (_pollOptions != null && _pollOptions.Contains(option))
            {
                // Удаляем из основного списка вариантов
                _pollOptions.Remove(option);
            }
        }

        private void RemoveQuestion_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var question = button.Tag as Question;

            if (question == null)
                return;

            if (MessageBox.Show("Вы действительно хотите удалить этот вопрос?",
                               "Подтверждение удаления",
                               MessageBoxButton.YesNo,
                               MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _questions.Remove(question);
            }
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
                                end_date DATETIME,           -- Дата окончания опроса/теста
                                is_active BOOLEAN,
                                options TEXT,
                                password VARCHAR(255) NULL,  -- Пароль для доступа к опросу/тесту
                                created_by VARCHAR(100) NULL -- Имя создателя опроса
                            )";
                        using (MySqlCommand createCmd = new MySqlCommand(createTableQuery, connection))
                        {
                            createCmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        // Проверяем наличие столбца end_date
                        string checkEndDateColumnQuery = "SHOW COLUMNS FROM polls LIKE 'end_date'";
                        using (MySqlCommand checkColCmd = new MySqlCommand(checkEndDateColumnQuery, connection))
                        {
                            object colResult = checkColCmd.ExecuteScalar();
                            if (colResult == null)
                            {
                                // Добавляем столбец end_date
                                string addColumnQuery = @"
                                    ALTER TABLE polls
                                    ADD COLUMN end_date DATETIME NULL";

                                using (MySqlCommand addColCmd = new MySqlCommand(addColumnQuery, connection))
                                {
                                    addColCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // Проверяем наличие столбца password
                        string checkPasswordColumnQuery = "SHOW COLUMNS FROM polls LIKE 'password'";
                        using (MySqlCommand checkColCmd = new MySqlCommand(checkPasswordColumnQuery, connection))
                        {
                            object colResult = checkColCmd.ExecuteScalar();
                            if (colResult == null)
                            {
                                // Добавляем столбец password
                                string addColumnQuery = @"
                                    ALTER TABLE polls
                                    ADD COLUMN password VARCHAR(255) NULL";

                                using (MySqlCommand addColCmd = new MySqlCommand(addColumnQuery, connection))
                                {
                                    addColCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        string checkUserIdColumnQuery = "SHOW COLUMNS FROM polls LIKE 'user_id'";
                        using (MySqlCommand checkColCmd = new MySqlCommand(checkUserIdColumnQuery, connection))
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

                        // Проверяем наличие столбца created_by
                        string checkCreatedByColumnQuery = "SHOW COLUMNS FROM polls LIKE 'created_by'";
                        using (MySqlCommand checkColCmd = new MySqlCommand(checkCreatedByColumnQuery, connection))
                        {
                            object colResult = checkColCmd.ExecuteScalar();
                            if (colResult == null)
                            {
                                // Добавляем столбец created_by
                                string addColumnQuery = @"
                                    ALTER TABLE polls
                                    ADD COLUMN created_by VARCHAR(100) NULL";

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

                MessageBox.Show($"Ошибка при проверке/создании таблицы polls или столбцов: {ex.Message}", "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    if (_isEditingMode && _editingPollId > 0)
                    {
                        checkQuery += " AND id != @id";
                    }
                    using (MySqlCommand cmd = new MySqlCommand(checkQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@title", title);
                        if (_isEditingMode && _editingPollId > 0)
                        {
                            cmd.Parameters.AddWithValue("@id", _editingPollId);
                        }

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

                    string insertQuery = "INSERT INTO polls (user_id, title, description, poll_type, created_at, is_active, options, password) VALUES (@user_id, @title, @description, @poll_type, @created_at, @is_active, @options, @password)";
                    int pollId;

                    using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@user_id", CurrentUser.UserId);
                        cmd.Parameters.AddWithValue("@title", _currentPoll.Title);
                        cmd.Parameters.AddWithValue("@description", _currentPoll.Description);
                        cmd.Parameters.AddWithValue("@poll_type", _currentPoll.PollType);
                        cmd.Parameters.AddWithValue("@created_at", _currentPoll.CreatedAt);
                        cmd.Parameters.AddWithValue("@is_active", _currentPoll.IsActive);
                        cmd.Parameters.AddWithValue("@options", _currentPoll.Options != null ? string.Join("|||", _currentPoll.Options) : string.Empty);

                        string password = PasswordBox.Password;
                        if (!string.IsNullOrEmpty(password))
                        {
                            cmd.Parameters.AddWithValue("@password", password.Trim());
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@password", DBNull.Value);
                        }

                        cmd.ExecuteNonQuery();
                        pollId = (int)cmd.LastInsertedId;
                    }

                    // Если тип опроса - голосование, сохраняем изображения кандидатов
                    if (_currentPoll.PollType == "Голосование" && _pollOptions != null && _pollOptions.Count > 0)
                    {
                        // Проверяем и создаем таблицу для хранения изображений кандидатов
                        string checkTableQuery = "SHOW TABLES LIKE 'candidate_images'";
                        using (MySqlCommand cmd = new MySqlCommand(checkTableQuery, conn))
                        {
                            object result = cmd.ExecuteScalar();
                            if (result == null)
                            {
                                string createTableQuery = @"
                                    CREATE TABLE IF NOT EXISTS `candidate_images` (
                                      `id` INT NOT NULL AUTO_INCREMENT,
                                      `poll_id` INT NOT NULL,
                                      `candidate_name` VARCHAR(255) NOT NULL,
                                      `image_data` LONGBLOB NULL,
                                      `description` TEXT NULL,
                                      PRIMARY KEY (`id`),
                                      INDEX `idx_poll_id` (`poll_id` ASC)
                                    )";
                                using (MySqlCommand createCmd = new MySqlCommand(createTableQuery, conn))
                                {
                                    createCmd.ExecuteNonQuery();
                                }
                            }
                        }

                        // Сохраняем изображения кандидатов
                        foreach (var option in _pollOptions)
                        {
                            if (option.ImageData != null && option.ImageData.Length > 0)
                            {
                                string insertImageQuery = @"
                                    INSERT INTO candidate_images 
                                    (poll_id, candidate_name, image_data, description) 
                                    VALUES 
                                    (@poll_id, @candidate_name, @image_data, @description)";

                                using (MySqlCommand cmd = new MySqlCommand(insertImageQuery, conn))
                                {
                                    cmd.Parameters.AddWithValue("@poll_id", pollId);
                                    cmd.Parameters.AddWithValue("@candidate_name", option.Text);
                                    cmd.Parameters.AddWithValue("@image_data", option.ImageData);
                                    cmd.Parameters.AddWithValue("@description", option.ImageDescription ?? string.Empty);

                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }

                    // Проверяем сохраненный опрос
                    string checkQuery = "SELECT id, title, poll_type FROM polls WHERE id = @id";
                    using (var checkCmd = new MySqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@id", pollId);
                        using (var reader = checkCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                MessageBox.Show($"Голосование успешно сохранено:\nID: {reader["id"]}\nНазвание: {reader["title"]}\nТип: {reader["poll_type"]}",
                                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }

                    return true;
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
                            options = @options,
                            password = @password 
                        WHERE id = @id";

                    using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", _editingPollId);
                        cmd.Parameters.AddWithValue("@title", _currentPoll.Title);
                        cmd.Parameters.AddWithValue("@description", _currentPoll.Description);
                        cmd.Parameters.AddWithValue("@poll_type", _currentPoll.PollType);
                        cmd.Parameters.AddWithValue("@options", _currentPoll.Options != null ? string.Join("|||", _currentPoll.Options) : string.Empty);

                        string password = PasswordBox.Password;
                        if (!string.IsNullOrEmpty(password))
                        {
                            cmd.Parameters.AddWithValue("@password", password);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@password", DBNull.Value);
                        }

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

            // Проверка уникальности заголовка только для новых опросов, но не при редактировании
            if (!_isEditingMode && CheckIfPollTitleExists(pollTitle))
            {
                MessageBox.Show($"Опрос или тест с заголовком \"{pollTitle}\" уже существует. Пожалуйста, введите другой заголовок.",
                                "Заголовок уже используется", MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleTextBox.Focus();
                TitleTextBox.SelectAll();
                return;
            }

            // Проверка даты окончания
            if (EndDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Пожалуйста, выберите дату окончания опроса/теста.",
                               "Дата не выбрана", MessageBoxButton.OK, MessageBoxImage.Warning);
                EndDatePicker.Focus();
                return;
            }

            if (EndDatePicker.SelectedDate < DateTime.Today)
            {
                MessageBox.Show("Дата окончания опроса/теста не может быть раньше сегодняшнего дня.",
                               "Неверная дата", MessageBoxButton.OK, MessageBoxImage.Warning);
                EndDatePicker.Focus();
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
                if (_currentPoll.Options == null)
                    _currentPoll.Options = new List<string>();
                else
                    _currentPoll.Options.Clear();

                if (_questions != null && _questions.Count > 0)
                {
                    foreach (var question in _questions)
                    {
                        if (string.IsNullOrWhiteSpace(question.QuestionText))
                            continue;

                        string questionData = "Q:" + question.QuestionText;

                        if (question.Options != null && question.Options.Count > 0)
                        {
                            foreach (var option in question.Options)
                            {
                                if (string.IsNullOrWhiteSpace(option.Text))
                                    continue;

                                questionData += $"|||O:{option.Text}:{(option.IsCorrect ? "1" : "0")}";
                            }
                        }

                        _currentPoll.Options.Add(questionData);
                    }
                }

                if (!_currentPoll.Options.Any())
                {
                    MessageBox.Show("Нельзя сохранить тест без вопросов.", "Нет вопросов", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
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
            _currentPoll.EndDate = EndDatePicker.SelectedDate.Value; // Сохраняем дату окончания
            _currentPoll.IsActive = true;

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Создаем таблицу polls, если она не существует
                    if (!CreatePollTableIfNotExists(conn))
                    {
                        return;
                    }

                    int pollId;
                    string password = PasswordBox.Password;

                    // Начинаем транзакцию для обеспечения целостности данных
                    MySqlTransaction transaction = conn.BeginTransaction();

                    try
                    {
                        // Сохраняем или обновляем основную информацию об опросе
                        if (_isEditingMode && _editingPollId > 0)
                        {
                            string updateQuery = @"
                            UPDATE polls 
                            SET title = @title, 
                                description = @description, 
                                poll_type = @poll_type, 
                                options = @options,
                                created_by = @created_by,
                                password = @password,
                                end_date = @end_date
                            WHERE id = @id";

                            using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@id", _editingPollId);
                                cmd.Parameters.AddWithValue("@title", _currentPoll.Title);
                                cmd.Parameters.AddWithValue("@description", _currentPoll.Description);
                                cmd.Parameters.AddWithValue("@poll_type", _currentPoll.PollType);
                                cmd.Parameters.AddWithValue("@options", _currentPoll.Options != null ? string.Join("|||", _currentPoll.Options) : string.Empty);
                                cmd.Parameters.AddWithValue("@created_by", CurrentUser.Name);
                                cmd.Parameters.AddWithValue("@password", !string.IsNullOrEmpty(password) ? password : (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@end_date", _currentPoll.EndDate);

                                cmd.ExecuteNonQuery();
                                pollId = _editingPollId;
                            }

                            // При редактировании и если это голосование, обновляем изображения кандидатов
                            if (_currentPoll.PollType == "Голосование" && _pollOptions != null && _pollOptions.Count > 0)
                            {
                                // Удаляем старые изображения
                                string deleteImagesQuery = "DELETE FROM candidate_images WHERE poll_id = @pollId";
                                using (MySqlCommand cmd = new MySqlCommand(deleteImagesQuery, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@pollId", pollId);
                                    cmd.ExecuteNonQuery();
                                }

                                // Добавляем новые изображения
                                foreach (var option in _pollOptions)
                                {
                                    if (option.ImageData != null && option.ImageData.Length > 0)
                                    {
                                        string insertImageQuery = @"
                                        INSERT INTO candidate_images 
                                        (poll_id, candidate_name, image_data, description) 
                                        VALUES 
                                        (@poll_id, @candidate_name, @image_data, @description)";

                                        using (MySqlCommand cmd = new MySqlCommand(insertImageQuery, conn, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@poll_id", pollId);
                                            cmd.Parameters.AddWithValue("@candidate_name", option.Text);
                                            cmd.Parameters.AddWithValue("@image_data", option.ImageData);
                                            cmd.Parameters.AddWithValue("@description", option.ImageDescription ?? string.Empty);

                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            string insertQuery = @"
                            INSERT INTO polls 
                            (user_id, title, description, poll_type, created_at, is_active, options, created_by, password, end_date) 
                            VALUES 
                            (@user_id, @title, @description, @poll_type, @created_at, @is_active, @options, @created_by, @password, @end_date)";

                            using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@user_id", CurrentUser.UserId);
                                cmd.Parameters.AddWithValue("@title", _currentPoll.Title);
                                cmd.Parameters.AddWithValue("@description", _currentPoll.Description);
                                cmd.Parameters.AddWithValue("@poll_type", _currentPoll.PollType);
                                cmd.Parameters.AddWithValue("@created_at", _currentPoll.CreatedAt);
                                cmd.Parameters.AddWithValue("@is_active", _currentPoll.IsActive);
                                cmd.Parameters.AddWithValue("@options", _currentPoll.Options != null ? string.Join("|||", _currentPoll.Options) : string.Empty);
                                cmd.Parameters.AddWithValue("@created_by", CurrentUser.Name);
                                cmd.Parameters.AddWithValue("@password", !string.IsNullOrEmpty(password) ? password : (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@end_date", _currentPoll.EndDate);

                                cmd.ExecuteNonQuery();
                                pollId = (int)cmd.LastInsertedId;
                            }

                            // Если тип опроса - голосование, сохраняем изображения кандидатов
                            if (_currentPoll.PollType == "Голосование" && _pollOptions != null && _pollOptions.Count > 0)
                            {
                                // Проверяем и создаем таблицу для хранения изображений кандидатов, если необходимо
                                CreateCandidateImagesTableIfNotExists(conn);

                                // Сохраняем изображения кандидатов
                                foreach (var option in _pollOptions)
                                {
                                    if (option.ImageData != null && option.ImageData.Length > 0)
                                    {
                                        string insertImageQuery = @"
                                        INSERT INTO candidate_images 
                                        (poll_id, candidate_name, image_data, description) 
                                        VALUES 
                                        (@poll_id, @candidate_name, @image_data, @description)";

                                        using (MySqlCommand cmd = new MySqlCommand(insertImageQuery, conn, transaction))
                                        {
                                            cmd.Parameters.AddWithValue("@poll_id", pollId);
                                            cmd.Parameters.AddWithValue("@candidate_name", option.Text);
                                            cmd.Parameters.AddWithValue("@image_data", option.ImageData);
                                            cmd.Parameters.AddWithValue("@description", option.ImageDescription ?? string.Empty);

                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }
                        }

                        // Если это тест с вопросами, сохраняем вопросы и варианты ответов с изображениями
                        if (_currentPoll.PollType == "Тест с вопросами и вариантами ответов")
                        {
                            if (!SaveQuestionsAndOptions(pollId, conn, transaction))
                            {
                                MessageBox.Show("Произошла ошибка при сохранении вопросов и вариантов ответов.",
                                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                transaction.Rollback();
                                return;
                            }
                        }

                        // Завершаем транзакцию
                        transaction.Commit();

                        MessageBox.Show(_isEditingMode ?
                                       "Опрос успешно обновлен!" :
                                       "Опрос успешно сохранен в базе данных!",
                                       "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                        Close();
                    }
                    catch (Exception ex)
                    {
                        // В случае ошибки отменяем транзакцию
                        transaction.Rollback();
                        MessageBox.Show($"Ошибка при сохранении данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CreateCandidateImagesTableIfNotExists(MySqlConnection conn)
        {
            try
            {
                string checkTableQuery = "SHOW TABLES LIKE 'candidate_images'";
                using (MySqlCommand cmd = new MySqlCommand(checkTableQuery, conn))
                {
                    object result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        string createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS `candidate_images` (
                          `id` INT NOT NULL AUTO_INCREMENT,
                          `poll_id` INT NOT NULL,
                          `candidate_name` VARCHAR(255) NOT NULL,
                          `image_data` LONGBLOB NULL,
                          `description` TEXT NULL,
                          PRIMARY KEY (`id`),
                          INDEX `idx_poll_id` (`poll_id` ASC)
                        )";
                        using (MySqlCommand createCmd = new MySqlCommand(createTableQuery, conn))
                        {
                            createCmd.ExecuteNonQuery();
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке/создании таблицы candidate_images: {ex.Message}",
                               "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            string title = TitleTextBox.Text;
            FlowDocument descriptionDoc = DescriptionRichTextBox.Document;
            string pollType = ((ComboBoxItem)PollTypeComboBox.SelectedItem)?.Content?.ToString() ?? "Одиночный выбор (радиокнопки)";
            object pollData = null;

            string description = new TextRange(descriptionDoc.ContentStart, descriptionDoc.ContentEnd).Text?.Trim() ?? "";

            switch (pollType)
            {
                case "Одиночный выбор (радиокнопки)":
                case "Множественный выбор (флажки)":
                    pollData = _pollOptions;
                    break;
                case "Тест с вопросами и вариантами ответов":
                    pollData = _questions;
                    break;
                case "Голосование":
                    // Проверяем наличие вариантов
                    if (_pollOptions == null || _pollOptions.Count == 0)
                    {
                        MessageBox.Show("Необходимо добавить хотя бы одного кандидата для предпросмотра голосования.",
                                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Если выбран тип "Голосование", отображаем в TestGolosovania
                    // Для предпросмотра сначала сохраняем временный опрос в базу данных
                    if (_isEditingMode && _editingPollId > 0)
                    {
                        // Если мы редактируем существующий опрос, используем его ID
                        TestGolosovania voteWindow = new TestGolosovania(_editingPollId);
                        voteWindow.Owner = this;
                        voteWindow.ShowDialog();
                    }
                    else
                    {
                        // Для нового опроса нужно сначала создать временную запись
                        try
                        {
                            using (MySqlConnection conn = new MySqlConnection(connectionString))
                            {
                                conn.Open();

                                // Создаем таблицы если нужно
                                if (!CreatePollTableIfNotExists(conn) || !CreateCandidateImagesTableIfNotExists(conn))
                                {
                                    return;
                                }

                                // Создаем временную запись опроса
                                int tempPollId = -1;

                                string insertQuery = @"
                                    INSERT INTO polls 
                                    (user_id, title, description, poll_type, created_at, is_active, options, created_by, password, end_date) 
                                    VALUES 
                                    (@user_id, @title, @description, @poll_type, @created_at, @is_active, @options, @created_by, @password, @end_date)";

                                using (MySqlCommand cmd = new MySqlCommand(insertQuery, conn))
                                {
                                    cmd.Parameters.AddWithValue("@user_id", CurrentUser.UserId);
                                    cmd.Parameters.AddWithValue("@title", title);
                                    cmd.Parameters.AddWithValue("@description", description);
                                    cmd.Parameters.AddWithValue("@poll_type", "Голосование");
                                    cmd.Parameters.AddWithValue("@created_at", DateTime.Now);
                                    cmd.Parameters.AddWithValue("@is_active", true);

                                    // Формируем строку с вариантами
                                    List<string> options = new List<string>();
                                    foreach (var option in _pollOptions)
                                    {
                                        if (!string.IsNullOrEmpty(option.Text))
                                            options.Add(option.Text);
                                    }
                                    cmd.Parameters.AddWithValue("@options", string.Join("|||", options));

                                    cmd.Parameters.AddWithValue("@created_by", CurrentUser.Name);
                                    cmd.Parameters.AddWithValue("@password", !string.IsNullOrEmpty(PasswordBox.Password) ? PasswordBox.Password : (object)DBNull.Value);

                                    // Устанавливаем дату окончания
                                    if (NoLimitCheckBox.IsChecked == true)
                                        cmd.Parameters.AddWithValue("@end_date", new DateTime(9999, 12, 31));
                                    else
                                        cmd.Parameters.AddWithValue("@end_date", EndDatePicker.SelectedDate ?? DateTime.Now.AddDays(7));

                                    cmd.ExecuteNonQuery();
                                    tempPollId = (int)cmd.LastInsertedId;
                                }

                                // Сохраняем изображения кандидатов
                                foreach (var option in _pollOptions)
                                {
                                    if (option.ImageData != null && option.ImageData.Length > 0)
                                    {
                                        string insertImageQuery = @"
                                            INSERT INTO candidate_images 
                                            (poll_id, candidate_name, image_data, description) 
                                            VALUES 
                                            (@poll_id, @candidate_name, @image_data, @description)";

                                        using (MySqlCommand cmd = new MySqlCommand(insertImageQuery, conn))
                                        {
                                            cmd.Parameters.AddWithValue("@poll_id", tempPollId);
                                            cmd.Parameters.AddWithValue("@candidate_name", option.Text);
                                            cmd.Parameters.AddWithValue("@image_data", option.ImageData);
                                            cmd.Parameters.AddWithValue("@description", option.ImageDescription ?? string.Empty);

                                            cmd.ExecuteNonQuery();
                                        }
                                    }
                                }

                                // Открываем окно голосования с созданным ID
                                TestGolosovania voteWindow = new TestGolosovania(tempPollId);
                                voteWindow.Owner = this;
                                voteWindow.ShowDialog();

                                // После закрытия окна удаляем временную запись, если это предпросмотр
                                string deleteQuery = "DELETE FROM polls WHERE id = @id";
                                using (MySqlCommand cmd = new MySqlCommand(deleteQuery, conn))
                                {
                                    cmd.Parameters.AddWithValue("@id", tempPollId);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка при создании временного голосования для предпросмотра: {ex.Message}",
                                           "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    return;
                default:
                    break;
            }

            try
            {
                // Для других типов опросов используем стандартный предпросмотр
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
        private void LoadQuestionsWithImages(int pollId, MySqlConnection conn)
        {
            _questions.Clear();

            try
            {
                // Сначала загружаем все вопросы вместе с их ID
                Dictionary<int, Question> questionsWithIds = new Dictionary<int, Question>();

                string questionsQuery = @"
                    SELECT id, question_text, question_image, image_description
                    FROM questions 
                    WHERE poll_id = @pollId 
                    ORDER BY question_order";

                using (MySqlCommand cmd = new MySqlCommand(questionsQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@pollId", pollId);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int questionId = reader.GetInt32("id");
                            Question question = new Question
                            {
                                QuestionText = reader.GetString("question_text")
                            };

                            // Загружаем изображение вопроса, если оно есть
                            if (!reader.IsDBNull(reader.GetOrdinal("question_image")))
                            {
                                byte[] imageData = (byte[])reader["question_image"];
                                question.ImageData = imageData;

                                try
                                {
                                    // Создаем BitmapImage для отображения
                                    using (MemoryStream ms = new MemoryStream(imageData))
                                    {
                                        BitmapImage bitmap = new BitmapImage();
                                        bitmap.BeginInit();
                                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                        bitmap.StreamSource = ms;
                                        bitmap.EndInit();
                                        bitmap.Freeze();

                                        question.QuestionImageSource = bitmap;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Ошибка при загрузке изображения вопроса: {ex.Message}",
                                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                            }

                            // Загружаем описание изображения
                            if (!reader.IsDBNull(reader.GetOrdinal("image_description")))
                            {
                                question.ImageDescription = reader.GetString("image_description");
                            }

                            questionsWithIds.Add(questionId, question);
                            _questions.Add(question);
                        }
                    }
                }

                // Теперь загружаем варианты ответов для каждого вопроса
                if (questionsWithIds.Count > 0)
                {
                    string allOptionsQuery = @"
                        SELECT question_id, option_text, is_correct, option_image, image_description, option_order
                        FROM question_options
                        WHERE question_id IN (" + string.Join(",", questionsWithIds.Keys) + @")
                        ORDER BY question_id, option_order";

                    using (MySqlCommand cmd = new MySqlCommand(allOptionsQuery, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int questionId = reader.GetInt32("question_id");
                                if (questionsWithIds.TryGetValue(questionId, out Question question))
                                {
                                    PollOption option = new PollOption
                                    {
                                        Text = reader.GetString("option_text"),
                                        IsCorrect = reader.GetBoolean("is_correct")
                                    };

                                    // Загружаем изображение варианта ответа, если оно есть
                                    if (!reader.IsDBNull(reader.GetOrdinal("option_image")))
                                    {
                                        byte[] imageData = (byte[])reader["option_image"];
                                        option.ImageData = imageData;

                                        try
                                        {
                                            // Создаем BitmapImage для отображения
                                            using (MemoryStream ms = new MemoryStream(imageData))
                                            {
                                                BitmapImage bitmap = new BitmapImage();
                                                bitmap.BeginInit();
                                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                                bitmap.StreamSource = ms;
                                                bitmap.EndInit();
                                                bitmap.Freeze();

                                                option.OptionImageSource = bitmap;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show($"Ошибка при загрузке изображения варианта ответа: {ex.Message}",
                                                          "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке вопросов и изображений: {ex.Message}\n\nПодробности: {ex.StackTrace}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

                    // Загружаем основную информацию о тесте
                    string pollInfoQuery = "SELECT title, description, poll_type, options, created_by, password, end_date FROM polls WHERE id = @id";
                    string title = "";
                    string description = "";
                    string pollType = "Одиночный выбор (радиокнопки)";
                    string options = "";
                    string createdBy = "";
                    string password = "";
                    DateTime endDate = DateTime.Now.AddDays(7); // По умолчанию опрос активен 7 дней

                    using (MySqlCommand cmd = new MySqlCommand(pollInfoQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", pollId);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // Заполняем поля формы данными из БД
                                title = reader.GetString("title");
                                description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description");
                                pollType = reader.IsDBNull(reader.GetOrdinal("poll_type")) ? "Одиночный выбор (радиокнопки)" : reader.GetString("poll_type");
                                options = reader.IsDBNull(reader.GetOrdinal("options")) ? "" : reader.GetString("options");
                                createdBy = reader.IsDBNull(reader.GetOrdinal("created_by")) ? "" : reader.GetString("created_by");
                                password = reader.IsDBNull(reader.GetOrdinal("password")) ? "" : reader.GetString("password");

                                // Загружаем дату окончания опроса
                                if (!reader.IsDBNull(reader.GetOrdinal("end_date")))
                                {
                                    endDate = reader.GetDateTime("end_date");
                                }
                            }
                            else
                            {
                                MessageBox.Show("Не удалось загрузить данные теста", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                Close();
                                return;
                            }
                        }
                    }

                    // Обновляем интерфейс
                    TitleTextBox.Text = title;
                    DescriptionRichTextBox.Document.Blocks.Clear();
                    DescriptionRichTextBox.Document.Blocks.Add(new Paragraph(new Run(description)));

                    // Загружаем текущий пароль в поле ввода пароля
                    PasswordBox.Password = password;

                    // Устанавливаем дату окончания и чекбокс "Без ограничения времени"
                    if (endDate.Year >= 9000) // Дата близка к максимальной - без ограничения времени
                    {
                        NoLimitCheckBox.IsChecked = true; // Автоматически вызовет обработчик Checked
                    }
                    else
                    {
                        NoLimitCheckBox.IsChecked = false; // На всякий случай, хотя по умолчанию и так false
                        EndDatePicker.SelectedDate = endDate;
                    }

                    // Показываем панель пароля
                    PasswordPanel.Visibility = Visibility.Visible;

                    switch (pollType)
                    {
                        case "Множественный выбор (флажки)":
                            PollTypeComboBox.SelectedIndex = 1;
                            break;
                        case "Тест с вопросами и вариантами ответов":
                            PollTypeComboBox.SelectedIndex = 1;
                            break;
                        case "Голосование":
                            PollTypeComboBox.SelectedIndex = 0;
                            break;
                        default:
                            PollTypeComboBox.SelectedIndex = 0;
                            break;
                    }

                    if (pollType == "Тест с вопросами и вариантами ответов")
                    {
                        // Загружаем вопросы и варианты с поддержкой изображений (новое соединение)
                        using (MySqlConnection questionsConn = new MySqlConnection(connectionString))
                        {
                            questionsConn.Open();
                            LoadQuestionsWithImages(pollId, questionsConn);
                        }

                        // Если после загрузки вопросов массив пуст, пробуем старый способ
                        if (_questions.Count == 0 && !string.IsNullOrEmpty(options))
                        {
                            LoadQuestionsFromOptions(options);
                        }
                    }
                    else
                    {
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
                        if (_pollOptions.Count == 0)
                        {
                            _pollOptions.Add(new PollOption { Text = "Вариант 1" });
                            _pollOptions.Add(new PollOption { Text = "Вариант 2" });
                            _pollOptions.Add(new PollOption { Text = "Вариант 3" });
                        }

                        // Загружаем изображения кандидатов, если это голосование
                        if (pollType == "Голосование")
                        {
                            LoadCandidateImages(pollId, conn);
                        }
                    }
                    this.Title = "Редактирование опроса";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке опроса: {ex.Message}", "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void LoadQuestionsFromOptions(string options)
        {
            _questions.Clear();

            string[] questionsArray = options.Split(new[] { "|||" }, StringSplitOptions.RemoveEmptyEntries);

            Question currentQuestion = null;

            foreach (var item in questionsArray)
            {
                if (item.StartsWith("Q:"))
                {
                    currentQuestion = new Question { QuestionText = item.Substring(2) };
                    _questions.Add(currentQuestion);
                }
                else if (item.StartsWith("O:") && currentQuestion != null)
                {
                    string[] optionParts = item.Substring(2).Split(':');
                    if (optionParts.Length >= 2)
                    {
                        bool isCorrect = optionParts[1] == "1";
                        currentQuestion.Options.Add(new PollOption
                        {
                            Text = optionParts[0],
                            IsCorrect = isCorrect
                        });
                    }
                    else if (optionParts.Length == 1)
                    {
                        currentQuestion.Options.Add(new PollOption
                        {
                            Text = optionParts[0],
                            IsCorrect = false
                        });
                    }
                }
            }

            // Если всё еще нет вопросов, добавляем пустой вопрос
            if (_questions.Count == 0)
            {
                var newQuestion = new Question { QuestionText = "" };
                newQuestion.Options.Add(new PollOption { Text = "Вариант 1" });
                newQuestion.Options.Add(new PollOption { Text = "Вариант 2" });
                _questions.Add(newQuestion);
            }
        }

        // Обработчик для удаления изображения для вопроса
        private void RemoveQuestionImage_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var question = button.Tag as Question;

            if (question == null)
                return;

            question.ImageData = null;
            question.ImageDescription = null;
            question.QuestionImageSource = null;

            // Найдем контрол Image в интерфейсе
            Image imageControl = null;

            // Пытаемся найти Image через visual tree
            if (button.Parent is FrameworkElement parent)
            {
                // Поднимаемся вверх до Grid
                var grid = parent.Parent as Grid;
                if (grid != null)
                {
                    // Ищем Border в первой колонке
                    var border = grid.Children.OfType<Border>().FirstOrDefault();
                    if (border != null)
                    {
                        // Ищем Image внутри Border
                        imageControl = border.Child as Image;
                    }
                }
            }

            // Если всё еще не нашли, ищем по имени
            if (imageControl == null)
            {
                var expander = VisualTreeHelper.GetParent(button.Parent as DependencyObject);
                while (expander != null && !(expander is Expander))
                {
                    expander = VisualTreeHelper.GetParent(expander);
                }

                if (expander != null)
                {
                    imageControl = FindVisualChild<Image>(expander as DependencyObject, "QuestionImage");
                }
            }

            // Если нашли контрол, очищаем его
            if (imageControl != null)
            {
                imageControl.Source = null;
            }
        }

        // Обработчик для загрузки изображения для вопроса
        private void UploadQuestionImage_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var question = button.Tag as Question;

            if (question == null)
                return;

            // Проверяем наличие текста вопроса
            if (string.IsNullOrWhiteSpace(question.QuestionText))
            {
                MessageBox.Show("Пожалуйста, сначала введите текст вопроса.",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Изображения|*.jpg;*.jpeg;*.png;*.gif;*.bmp";
            openFileDialog.Title = "Выберите изображение для вопроса";

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Загружаем изображение
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(openFileDialog.FileName);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    // Конвертируем изображение в массив байтов
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (MemoryStream ms = new MemoryStream())
                    {
                        encoder.Save(ms);
                        question.ImageData = ms.ToArray();
                    }

                    // Устанавливаем свойство QuestionImageSource
                    question.QuestionImageSource = bitmap;

                    // Найдем контрол Image в интерфейсе и установим источник непосредственно
                    Image imageControl = null;

                    // Пытаемся найти Image через visual tree
                    if (button.Parent is FrameworkElement parent)
                    {
                        // Поднимаемся вверх до Grid
                        var grid = parent.Parent as Grid;
                        if (grid != null)
                        {
                            // Ищем Border в первой колонке
                            var border = grid.Children.OfType<Border>().FirstOrDefault();
                            if (border != null)
                            {
                                // Ищем Image внутри Border
                                imageControl = border.Child as Image;
                            }
                        }
                    }

                    // Если всё еще не нашли, ищем по имени
                    if (imageControl == null)
                    {
                        var expander = VisualTreeHelper.GetParent(button.Parent as DependencyObject);
                        while (expander != null && !(expander is Expander))
                        {
                            expander = VisualTreeHelper.GetParent(expander);
                        }

                        if (expander != null)
                        {
                            imageControl = FindVisualChild<Image>(expander as DependencyObject, "QuestionImage");
                        }
                    }

                    // Если нашли контрол, устанавливаем источник
                    if (imageControl != null)
                    {
                        imageControl.Source = bitmap;

                        // Обновляем UI
                        if (imageControl.Parent is Border imageBorder)
                        {
                            imageBorder.UpdateLayout();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Изображение загружено, но не может быть отображено. Пожалуйста, проверьте в предпросмотре.",
                                      "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке изображения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Обработчик для загрузки изображения для варианта ответа
        private void UploadOptionImage_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var option = button?.Tag as PollOption;

            if (option == null)
                return;

            // Кнопка будет неактивна, если текста нет, поэтому проверка не нужна

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Изображения|*.jpg;*.jpeg;*.png;*.gif;*.bmp";
            openFileDialog.Title = "Выберите изображение для кандидата";

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Загружаем изображение
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(openFileDialog.FileName);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    // Конвертируем изображение в массив байтов
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (MemoryStream ms = new MemoryStream())
                    {
                        encoder.Save(ms);
                        option.ImageData = ms.ToArray();
                    }

                    // Используем новый метод для обновления OptionImageSource из ImageData
                    option.UpdateImageSource();

                    // Находим элемент Image для обновления интерфейса
                    Image imageControl = FindImageControlForOption(button);

                    // Если нашли контрол, устанавливаем источник
                    if (imageControl != null)
                    {
                        // Устанавливаем источник изображения
                        imageControl.Source = option.OptionImageSource;

                        // Обновляем видимость родительского Border, если он есть
                        if (imageControl.Parent is Border imageBorder)
                        {
                            imageBorder.Visibility = Visibility.Visible;
                            imageBorder.UpdateLayout();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Изображение загружено, но может не отображаться до перезагрузки. Проверьте в предпросмотре.",
                                      "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке изображения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Вспомогательный метод для поиска элемента Image для указанного варианта
        private Image FindImageControlForOption(Button button)
        {
            Image imageControl = null;

            if (button == null)
                return null;

            // Пытаемся найти Image через visual tree, поднимаясь вверх до родительского Border
            DependencyObject parent = button;
            while (parent != null && !(parent is Border) && !(parent is Grid))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }

            // Если нашли Border или Grid, продолжаем поиск
            if (parent != null)
            {
                // Ищем Border с Image внутри (для отображения изображения)
                var borders = FindVisualChildren<Border>(parent);
                foreach (var border in borders)
                {
                    // Проверяем, является ли это Border для отображения изображения
                    if (border.Child is Image img)
                    {
                        imageControl = img;
                        break;
                    }
                }
            }

            return imageControl;
        }

        // Вспомогательный метод для поиска всех визуальных дочерних элементов определенного типа
        private IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T childOfType)
                {
                    yield return childOfType;
                }

                foreach (var grandChild in FindVisualChildren<T>(child))
                {
                    yield return grandChild;
                }
            }
        }

        // Вспомогательный метод для поиска визуального дочернего элемента
        private static T FindVisualChild<T>(DependencyObject parent, string childName = null) where T : DependencyObject
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


        private void RemoveOptionImage_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var option = button?.Tag as PollOption;

            if (option == null)
                return;

            // Очищаем данные изображения
            option.ImageData = null;
            option.ImageDescription = null;
            option.UpdateImageSource(); // Обновляем ImageSource с помощью нового метода

            // Находим Image контрол с помощью вспомогательного метода
            Image imageControl = FindImageControlForOption(button);

            // Если нашли контрол, очищаем его
            if (imageControl != null)
            {
                imageControl.Source = null;

                // Скрываем родительский Border, если он есть
                if (imageControl.Parent is Border imageBorder)
                {
                    imageBorder.Visibility = Visibility.Collapsed;
                }
            }
        }
        private bool CreateQuestionOptionImageColumnIfNotExists(MySqlConnection connection)
        {
            try
            {
                string checkQuestionsTableQuery = "SHOW TABLES LIKE 'questions'";
                using (MySqlCommand cmd = new MySqlCommand(checkQuestionsTableQuery, connection))
                {
                    object tableResult = cmd.ExecuteScalar();
                    if (tableResult == null)
                    {
                        string createQuestionsTableQuery = @"
                            CREATE TABLE IF NOT EXISTS `questions` (
                              `id` INT NOT NULL AUTO_INCREMENT,
                              `poll_id` INT NOT NULL,
                              `question_text` TEXT NULL,
                              `question_order` INT NULL DEFAULT 0,
                              `question_image` LONGBLOB NULL,
                              `image_description` TEXT NULL,
                              PRIMARY KEY (`id`),
                              INDEX `fk_questions_polls_idx` (`poll_id` ASC),
                              CONSTRAINT `fk_questions_polls`
                                FOREIGN KEY (`poll_id`)
                                REFERENCES `vopros`.`polls` (`id`)
                                ON DELETE CASCADE
                                ON UPDATE NO ACTION
                            )";
                        using (MySqlCommand createCmd = new MySqlCommand(createQuestionsTableQuery, connection))
                        {
                            createCmd.ExecuteNonQuery();
                        }
                    }
                }

                string checkOptionsTableQuery = "SHOW TABLES LIKE 'question_options'";
                using (MySqlCommand cmd = new MySqlCommand(checkOptionsTableQuery, connection))
                {
                    object tableResult = cmd.ExecuteScalar();
                    if (tableResult == null)
                    {

                        string createOptionsTableQuery = @"
                            CREATE TABLE IF NOT EXISTS `question_options` (
                              `id` INT NOT NULL AUTO_INCREMENT,
                              `question_id` INT NOT NULL,
                              `option_text` TEXT NULL,
                              `is_correct` BOOLEAN NULL DEFAULT FALSE,
                              `option_order` INT NULL DEFAULT 0,
                              `option_image` LONGBLOB NULL,
                              `image_description` TEXT NULL,
                              PRIMARY KEY (`id`),
                              INDEX `fk_question_options_questions_idx` (`question_id` ASC),
                              CONSTRAINT `fk_question_options_questions`
                                FOREIGN KEY (`question_id`)
                                REFERENCES `vopros`.`questions` (`id`)
                                ON DELETE CASCADE
                                ON UPDATE NO ACTION
                            )";
                        using (MySqlCommand createCmd = new MySqlCommand(createOptionsTableQuery, connection))
                        {
                            createCmd.ExecuteNonQuery();
                        }
                    }
                }

                string checkColumnQuery = "SHOW COLUMNS FROM question_options LIKE 'option_image'";
                using (MySqlCommand cmd = new MySqlCommand(checkColumnQuery, connection))
                {
                    object result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        string alterTableQuery = "ALTER TABLE question_options ADD COLUMN option_image LONGBLOB NULL";
                        using (MySqlCommand alterCmd = new MySqlCommand(alterTableQuery, connection))
                        {
                            alterCmd.ExecuteNonQuery();
                        }

                        string alterTableDescQuery = "ALTER TABLE question_options ADD COLUMN image_description TEXT NULL";
                        using (MySqlCommand alterDescCmd = new MySqlCommand(alterTableDescQuery, connection))
                        {
                            alterDescCmd.ExecuteNonQuery();
                        }
                    }
                }
                string checkQuestionImageQuery = "SHOW COLUMNS FROM questions LIKE 'question_image'";
                using (MySqlCommand cmd = new MySqlCommand(checkQuestionImageQuery, connection))
                {
                    object result = cmd.ExecuteScalar();
                    if (result == null)
                    {
                        string alterTableQuery = "ALTER TABLE questions ADD COLUMN question_image LONGBLOB NULL";
                        using (MySqlCommand alterCmd = new MySqlCommand(alterTableQuery, connection))
                        {
                            alterCmd.ExecuteNonQuery();
                        }

                        string alterTableDescQuery = "ALTER TABLE questions ADD COLUMN image_description TEXT NULL";
                        using (MySqlCommand alterDescCmd = new MySqlCommand(alterTableDescQuery, connection))
                        {
                            alterDescCmd.ExecuteNonQuery();
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке/создании таблиц и столбцов для изображений: {ex.Message}",
                                "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        private bool SaveQuestionsAndOptions(int pollId, MySqlConnection connection, MySqlTransaction transaction)
        {
            try
            {
                if (!CreateQuestionOptionImageColumnIfNotExists(connection))
                {
                    return false;
                }

                // Проверяем наличие текста в вопросах
                foreach (var question in _questions)
                {
                    if (string.IsNullOrWhiteSpace(question.QuestionText))
                    {
                        MessageBox.Show("Пожалуйста, введите текст вопроса перед сохранением.",
                                      "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }

                // Удаляем старые вопросы
                string deleteQuestionsQuery = "DELETE FROM questions WHERE poll_id = @pollId";
                using (MySqlCommand cmd = new MySqlCommand(deleteQuestionsQuery, connection, transaction))
                {
                    cmd.Parameters.AddWithValue("@pollId", pollId);
                    cmd.ExecuteNonQuery();
                }

                foreach (var question in _questions)
                {
                    string insertQuestionQuery = @"
                        INSERT INTO questions (poll_id, question_text, question_order, question_image, image_description)
                        VALUES (@pollId, @questionText, @questionOrder, @questionImage, @imageDescription)";

                    int questionId;
                    using (MySqlCommand cmd = new MySqlCommand(insertQuestionQuery, connection, transaction))
                    {
                        cmd.Parameters.AddWithValue("@pollId", pollId);
                        cmd.Parameters.AddWithValue("@questionText", question.QuestionText);
                        cmd.Parameters.AddWithValue("@questionOrder", _questions.IndexOf(question));

                        // Проверяем наличие изображения вопроса
                        if (question.ImageData != null && question.ImageData.Length > 0)
                        {
                            cmd.Parameters.AddWithValue("@questionImage", question.ImageData);
                            cmd.Parameters.AddWithValue("@imageDescription", question.ImageDescription ?? string.Empty);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@questionImage", DBNull.Value);
                            cmd.Parameters.AddWithValue("@imageDescription", DBNull.Value);
                        }

                        cmd.ExecuteNonQuery();
                        questionId = (int)cmd.LastInsertedId;
                    }

                    int savedOptionsCount = 0;
                    foreach (var option in question.Options)
                    {
                        if (string.IsNullOrWhiteSpace(option.Text))
                            continue;

                        string insertOptionQuery = @"
                            INSERT INTO question_options (question_id, option_text, is_correct, option_order, option_image, image_description)
                            VALUES (@questionId, @optionText, @isCorrect, @optionOrder, @optionImage, @imageDescription)";

                        using (MySqlCommand cmd = new MySqlCommand(insertOptionQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@questionId", questionId);
                            cmd.Parameters.AddWithValue("@optionText", option.Text);
                            cmd.Parameters.AddWithValue("@isCorrect", option.IsCorrect);
                            cmd.Parameters.AddWithValue("@optionOrder", savedOptionsCount);

                            // Проверяем наличие изображения варианта ответа
                            if (option.ImageData != null && option.ImageData.Length > 0)
                            {
                                cmd.Parameters.AddWithValue("@optionImage", option.ImageData);
                                cmd.Parameters.AddWithValue("@imageDescription", option.ImageDescription ?? string.Empty);
                            }
                            else
                            {
                                cmd.Parameters.AddWithValue("@optionImage", DBNull.Value);
                                cmd.Parameters.AddWithValue("@imageDescription", DBNull.Value);
                            }

                            cmd.ExecuteNonQuery();
                            savedOptionsCount++;
                        }
                    }

                    if (savedOptionsCount == 0)
                    {
                        string insertEmptyOptionQuery = @"
                            INSERT INTO question_options (question_id, option_text, is_correct, option_order)
                            VALUES (@questionId, 'Нет вариантов ответа', 0, 0)";

                        using (MySqlCommand cmd = new MySqlCommand(insertEmptyOptionQuery, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@questionId", questionId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении вопросов и вариантов ответов: {ex.Message}\n\nПодробности: {ex.StackTrace}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordVisible = !_isPasswordVisible;

            if (_isPasswordVisible)
            {
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordTextBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;
                TogglePasswordButton.Content = "🔒";
            }
            else
            {
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordTextBox.Visibility = Visibility.Collapsed;
                PasswordBox.Visibility = Visibility.Visible;
                TogglePasswordButton.Content = "👁";
            }
        }
        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isPasswordVisible && PasswordTextBox.Visibility == Visibility.Visible)
            {
                PasswordBox.Password = PasswordTextBox.Text;
            }
        }

        private void LoadCandidateImages(int pollId, MySqlConnection conn)
        {
            try
            {
                string query = @"
                    SELECT candidate_name, image_data, description
                    FROM candidate_images
                    WHERE poll_id = @pollId";

                using (MySqlCommand cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@pollId", pollId);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string candidateName = reader.GetString("candidate_name");
                            byte[] imageData = null;
                            string description = null;

                            // Получаем изображение, если оно есть
                            if (!reader.IsDBNull(reader.GetOrdinal("image_data")))
                            {
                                imageData = (byte[])reader["image_data"];
                            }

                            // Получаем описание, если оно есть
                            if (!reader.IsDBNull(reader.GetOrdinal("description")))
                            {
                                description = reader.GetString("description");
                            }

                            // Ищем вариант с таким же именем в коллекции
                            var option = _pollOptions.FirstOrDefault(o => o.Text == candidateName);
                            if (option != null && imageData != null && imageData.Length > 0)
                            {
                                option.ImageData = imageData;
                                option.ImageDescription = description;
                                option.UpdateImageSource(); // Обновляем источник изображения
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке изображений кандидатов: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обработчик события Checked для флажка "Без ограничения времени"
        private void NoLimitCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            EndDatePicker.IsEnabled = false;
            // Устанавливаем максимально возможную дату (9999-12-31) для обозначения "без ограничений"
            EndDatePicker.SelectedDate = new DateTime(9999, 12, 31);
        }

        // Обработчик события Unchecked для флажка "Без ограничения времени"
        private void NoLimitCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            EndDatePicker.IsEnabled = true;
            // Возвращаем дату по умолчанию (+7 дней)
            EndDatePicker.SelectedDate = DateTime.Now.AddDays(7);
        }

        // Метод для проверки активности опроса/теста на основе даты окончания
        public static bool IsPollActive(DateTime endDate)
        {
            // Если дата окончания близка к максимально возможной дате, считаем, что ограничения нет
            if (endDate.Year >= 9000)
                return true;

            // Иначе проверяем, не истек ли срок
            return DateTime.Now.Date <= endDate.Date;
        }

        // Публичный статический метод для проверки активности опроса и вывода сообщения
        public static bool CheckPollExpiration(DateTime endDate, string pollTitle)
        {
            // Проверяем, активен ли опрос
            if (!IsPollActive(endDate))
            {
                MessageBox.Show(
                    $"Срок действия опроса/теста \"{pollTitle}\" истек {endDate.ToShortDateString()}.\n\n" +
                    "Данный опрос больше недоступен для прохождения.",
                    "Срок опроса истек",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false; // Опрос неактивен
            }

            return true; // Опрос активен
        }

        public void SetTemplateValues(string title, string pollType)
        {
            // Устанавливаем значение заголовка
            if (!string.IsNullOrEmpty(title))
            {
                TitleTextBox.Text = title;
                _currentPoll.Title = title;
            }


            if (!string.IsNullOrEmpty(pollType))
            {
                // Находим соответствующий элемент в ComboBox
                foreach (ComboBoxItem item in PollTypeComboBox.Items)
                {
                    if (item.Content.ToString() == pollType)
                    {
                        PollTypeComboBox.SelectedItem = item;
                        _currentPoll.PollType = pollType;
                        break;
                    }
                }
            }
        }
    }
}
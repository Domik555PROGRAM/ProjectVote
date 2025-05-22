using Microsoft.Win32;
using MySql.Data.MySqlClient;
using Project_Vote.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;


namespace Project_Vote
{
    public partial class MainWindow : Window
    {
        private bool isSidePanelOpen = false;
        private Random _random = new Random();
        private Dictionary<Type, Window> _openWindows = new Dictionary<Type, Window>();
        private bool _wavesEffectEnabled = true;
        private bool _bubblesEffectEnabled = true;

        private const double BaseAmplitude = 10;   // базовая амплитуда волны
        private const double AmplitudeRange = 20;    // диапазон изменения амплитуды
        private const double Frequency = 5;          // число полных циклов волны по высоте
        private const double FillWidth = 100;        // ширина заполненной области (с обеих сторон)


        private System.Windows.Shapes.Path leftFill;
        private System.Windows.Shapes.Path rightFill;

        private double _phase;
        private double _currentAmplitude;
        private DateTime _lastRender;
        private bool _effectsInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            StateChanged += MainWindow_StateChanged;
            UpdateUserInfo();
        }

        private void UpdateUserInfo()
        {
            if (CurrentUser.IsLoggedIn)
            {
                UserNameText.Text = CurrentUser.Name;

                LoginButton.Visibility = Visibility.Collapsed;
                RegisterButton.Visibility = Visibility.Collapsed;
                SeparatorText.Visibility = Visibility.Collapsed;

                YourQuestionsButtonMenu.Visibility = Visibility.Visible;

                if (CurrentUser.Photo != null)
                {
                    try
                    {
                        using (MemoryStream ms = new MemoryStream(CurrentUser.Photo))
                        {
                            BitmapImage bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = ms;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            TopBarProfileImage.ImageSource = bitmap;
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowNotification($"Ошибка при загрузке фото: {ex.Message}", NotificationType.Error);
                        SetDefaultAvatar();
                    }
                }
                else
                {
                    SetDefaultAvatar();
                }
            }
            else
            {
                UserNameText.Text = "Гость";
                LoginButton.Visibility = Visibility.Visible;
                RegisterButton.Visibility = Visibility.Visible;
                SeparatorText.Visibility = Visibility.Visible;

                YourQuestionsButtonMenu.Visibility = Visibility.Collapsed;

                SetDefaultAvatar();
            }
        }

        private void SetDefaultAvatar()
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri("C:\\Images\\user.png", UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                TopBarProfileImage.ImageSource = bitmap;
            }
            catch (Exception ex)
            {
                ShowNotification($"Ошибка при загрузке изображения по умолчанию: {ex.Message}", NotificationType.Error);
            }
        }


        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(InitializeEffects), DispatcherPriority.Render);
            AttachPulseEffectToButtons();
            WaveEffectToggle.IsChecked = _wavesEffectEnabled;
            BubbleEffectToggle.IsChecked = _bubblesEffectEnabled;
        }

        private void InitializeEffects()
        {
            try
            {
                if (EffectCanvas == null)
                {
                    Console.WriteLine("EffectCanvas не найден");
                    return;
                }
                if (leftFill != null && EffectCanvas.Children.Contains(leftFill))
                {
                    EffectCanvas.Children.Remove(leftFill);
                }

                if (rightFill != null && EffectCanvas.Children.Contains(rightFill))
                {
                    EffectCanvas.Children.Remove(rightFill);
                }
                leftFill = new System.Windows.Shapes.Path
                {
                    Stroke = Brushes.Transparent,
                    Fill = new SolidColorBrush(Colors.LightBlue),
                    Opacity = 0.6,
                    Visibility = Visibility.Visible
                };
                rightFill = new System.Windows.Shapes.Path
                {
                    Stroke = Brushes.Transparent,
                    Fill = new SolidColorBrush(Color.FromRgb(173, 216, 230)),
                    Opacity = 0.6,
                    Visibility = Visibility.Visible
                };
                EffectCanvas.Children.Add(leftFill);
                EffectCanvas.Children.Add(rightFill);
                SetElementPosition(leftFill, 0, 0);
                _lastRender = DateTime.Now;
                _effectsInitialized = true;
                CompositionTarget.Rendering -= CompositionTarget_Rendering;
                CompositionTarget.Rendering += CompositionTarget_Rendering;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при инициализации эффектов: {ex.Message}");
            }
        }


        private void SetElementPosition(UIElement element, double left, double top)
        {
            if (element != null && EffectCanvas != null && EffectCanvas.Children.Contains(element))
            {
                try
                {
                    Canvas.SetLeft(element, left);
                    Canvas.SetTop(element, top);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при установке позиции элемента: {ex.Message}");
                }
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (EffectCanvas != null)
            {
                EffectCanvas.Width = e.NewSize.Width;
                EffectCanvas.Height = e.NewSize.Height;
                SetElementPosition(leftFill, 0, 50);
                SetElementPosition(rightFill, EffectCanvas.ActualWidth - GetDynamicFillWidth(), 50);
            }
        }

        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            if (!_effectsInitialized || leftFill == null || rightFill == null)
            {
                return;
            }
            if (_wavesEffectEnabled)
            {
                EffectCanvas.Visibility = Visibility.Visible;
                if (WindowState == WindowState.Maximized)
                {
                    if (leftFill != null) leftFill.Visibility = Visibility.Visible;
                    if (rightFill != null) rightFill.Visibility = Visibility.Visible;
                }
                else
                {
                    if (leftFill != null) leftFill.Visibility = Visibility.Collapsed;
                    if (rightFill != null) rightFill.Visibility = Visibility.Collapsed;
                }

                DateTime now = DateTime.Now;
                double deltaTime = (now - _lastRender).TotalSeconds;
                _lastRender = now;

                _phase += deltaTime * 2 * Math.PI / 5;
                _currentAmplitude = BaseAmplitude + AmplitudeRange * (0.5 + 0.5 * Math.Sin(now.TimeOfDay.TotalSeconds * 0.5));

                double height = EffectCanvas.ActualHeight;
                try
                {
                    if (leftFill != null && EffectCanvas.Children.Contains(leftFill))
                    {
                        leftFill.Data = GenerateLeftFillGeometry(height, _currentAmplitude, _phase);
                    }

                    if (rightFill != null && EffectCanvas.Children.Contains(rightFill))
                    {
                        rightFill.Data = GenerateRightFillGeometry(height, _currentAmplitude, _phase + Math.PI / 2);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при обновлении геометрии: {ex.Message}");
                }
            }
            else
            {
                if (leftFill != null) leftFill.Visibility = Visibility.Collapsed;
                if (rightFill != null) rightFill.Visibility = Visibility.Collapsed;
            }
        }

        private Geometry GenerateLeftFillGeometry(double height, double amplitude, double phase)
        {
            double xOffset = FillWidth / 2;
            int numPoints = 100;
            StreamGeometry geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {

                ctx.BeginFigure(new Point(0, 0), true, true);

                for (int i = 0; i <= numPoints; i++)
                {
                    double y = height * i / numPoints;
                    double waveX = xOffset + amplitude * Math.Sin(2 * Math.PI * Frequency * i / numPoints + phase);
                    ctx.LineTo(new Point(waveX, y), true, false);
                }
                ctx.LineTo(new Point(0, height), true, false);
            }
            geometry.Freeze();
            return geometry;
        }


        private Geometry GenerateRightFillGeometry(double height, double amplitude, double phase)
        {
            double xOffset = FillWidth / 2;
            int numPoints = 100;
            StreamGeometry geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(EffectCanvas.ActualWidth, 0), true, true);
                for (int i = 0; i <= numPoints; i++)
                {
                    double y = height * i / numPoints;
                    double waveX = EffectCanvas.ActualWidth - FillWidth +
                                  xOffset + amplitude * Math.Sin(2 * Math.PI * Frequency * i / numPoints + phase);
                    ctx.LineTo(new Point(waveX, y), true, false);
                }
                ctx.LineTo(new Point(EffectCanvas.ActualWidth, height), true, false);
            }
            geometry.Freeze();
            return geometry;
        }
        private double GetDynamicFillWidth()
        {
            return (WindowState == WindowState.Normal) ? EffectCanvas.ActualWidth / 2 : 100;
        }
        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isSidePanelOpen)
            {
                OpenSidePanel();
            }
            else
            {
                CloseSidePanel();
            }
        }

        private void ClosePanelButton_Click(object sender, RoutedEventArgs e)
        {
            CloseSidePanel();
        }
        private void OpenSidePanel()
        {
            var sb = (Storyboard)FindResource("OpenSidePanelStoryboard");
            sb.Completed += (s, args) =>
            {
                isSidePanelOpen = true;
                OverlayPanel.Visibility = Visibility.Visible;
            };
            sb.Begin();
        }

        private void CloseSidePanel()
        {
            var sb = (Storyboard)FindResource("CloseSidePanelStoryboard");
            sb.Completed += (s, args) =>
            {
                isSidePanelOpen = false;
                OverlayPanel.Visibility = Visibility.Collapsed;
            };
            sb.Begin();
            OverlayPanel.Visibility = Visibility.Collapsed;
        }
        private void OverlayPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CloseSidePanel();
            e.Handled = true;
        }
        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.OriginalSource is FrameworkElement element)
                {
                    if (element is Button || element.Parent is Button || element is TextBox || element.Parent is TextBox ||
                        element is ToggleButton || element.Parent is ToggleButton)
                    {
                        return;
                    }
                }
                if (_bubblesEffectEnabled)
                {
                    Point pos = e.GetPosition(EffectCanvas);
                    int bubbleCount = _random.Next(5, 15);
                    for (int i = 0; i < bubbleCount; i++)
                    {
                        CreateBubble(pos);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании пузырьков: {ex.Message}");
            }
        }
        private void CreateBubble(Point origin)
        {
            if (EffectCanvas == null) return;

            try
            {
                double size = _random.Next(5, 21);
                Ellipse bubble = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Opacity = 0.8,
                    Visibility = Visibility.Visible
                };
                LinearGradientBrush bubbleBrush = new LinearGradientBrush();
                double randomAngle = _random.NextDouble() * 360;
                double rad = randomAngle * Math.PI / 180.0;
                double cos = Math.Cos(rad);
                double sin = Math.Sin(rad);
                bubbleBrush.StartPoint = new Point(0.5 - cos / 2, 0.5 - sin / 2);
                bubbleBrush.EndPoint = new Point(0.5 + cos / 2, 0.5 + sin / 2);
                bubbleBrush.GradientStops.Add(new GradientStop(Color.FromArgb(180, 0, 0, 255), 0));
                bubbleBrush.GradientStops.Add(new GradientStop(Color.FromArgb(180, 128, 0, 128), 1));
                bubble.Fill = bubbleBrush;
                EffectCanvas.Children.Add(bubble);
                Canvas.SetLeft(bubble, origin.X - size / 2);
                Canvas.SetTop(bubble, origin.Y - size / 2);
                TranslateTransform transform = new TranslateTransform();
                bubble.RenderTransform = transform;
                double angle = _random.NextDouble() * 2 * Math.PI;
                double distance = _random.Next(30, 101);
                double offsetX = Math.Cos(angle) * distance;
                double offsetY = Math.Sin(angle) * distance;

                DoubleAnimation animX = new DoubleAnimation(0, offsetX, TimeSpan.FromSeconds(1));
                DoubleAnimation animY = new DoubleAnimation(0, offsetY, TimeSpan.FromSeconds(1));
                DoubleAnimation opacityAnim = new DoubleAnimation(0.8, 0, TimeSpan.FromSeconds(1));

                Storyboard sb = new Storyboard();
                sb.Children.Add(animX);
                sb.Children.Add(animY);
                sb.Children.Add(opacityAnim);

                Storyboard.SetTarget(animX, bubble);
                Storyboard.SetTargetProperty(animX, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                Storyboard.SetTarget(animY, bubble);
                Storyboard.SetTargetProperty(animY, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
                Storyboard.SetTarget(opacityAnim, bubble);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));

                sb.Completed += (s, e) => EffectCanvas.Children.Remove(bubble);
                sb.Begin();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании пузырька: {ex.Message}");
            }
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

            Registration registrationWindow = new Registration();


            if (registrationWindow.ShowDialog() == true)
            {

                UpdateUserInfo();
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

            Vhod loginWindow = new Vhod();
            if (loginWindow.ShowDialog() == true)
            {

                UpdateUserInfo();
            }
        }

        private void ProfileBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {

            Border profileBorder = sender as Border;
            if (profileBorder != null)
            {
                profileBorder.ContextMenu.IsOpen = true;
                e.Handled = true;
            }
        }

        private void ChangeNameMenuItem_Click(object sender, RoutedEventArgs e)
        {

            if (!CurrentUser.IsLoggedIn)
            {
                ShowLoginPrompt();
                return;
            }


            Window nameDialog = new Window
            {
                Title = "Изменить имя",
                Width = 350,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243))
            };


            StackPanel panel = new StackPanel { Margin = new Thickness(20) };
            TextBlock label = new TextBlock
            {
                Text = "Новое имя:",
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 10),
                FontSize = 14
            };

            TextBox nameTextBox = new TextBox
            {
                Text = CurrentUser.Name,
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 0, 20),
                FontSize = 14,
                MaxLength = 16
            };

            TextBlock errorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(255, 213, 79)),
                FontSize = 12,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, -15, 0, 10)
            };

            Button saveButton = new Button
            {
                Content = "Сохранить",
                Background = Brushes.White,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Padding = new Thickness(15, 5, 15, 5),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            saveButton.Click += (s, args) =>
            {
                if (string.IsNullOrWhiteSpace(nameTextBox.Text))
                {
                    errorText.Text = "Имя не может быть пустым";
                    errorText.Visibility = Visibility.Visible;
                    return;
                }

                if (nameTextBox.Text.Length < 2)
                {
                    errorText.Text = "Имя должно содержать минимум 2 символа";
                    errorText.Visibility = Visibility.Visible;
                    return;
                }

                if (nameTextBox.Text == CurrentUser.Name)
                {
                    nameDialog.Close();
                    return;
                }

                try
                {
                    using (MySqlConnection conn = new MySqlConnection("Server=localhost;Port=3306;Database=voteuser;Uid=root"))
                    {
                        conn.Open();


                        string checkQuery = "SELECT COUNT(*) FROM users WHERE name = @name AND id != @id";
                        using (MySqlCommand checkCmd = new MySqlCommand(checkQuery, conn))
                        {
                            checkCmd.Parameters.AddWithValue("@name", nameTextBox.Text);
                            checkCmd.Parameters.AddWithValue("@id", CurrentUser.UserId);
                            int userCount = Convert.ToInt32(checkCmd.ExecuteScalar());
                            if (userCount > 0)
                            {
                                errorText.Text = "Пользователь с таким именем уже существует";
                                errorText.Visibility = Visibility.Visible;
                                return;
                            }
                        }


                        string updateQuery = "UPDATE users SET name = @name WHERE id = @id";
                        using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@name", nameTextBox.Text);
                            cmd.Parameters.AddWithValue("@id", CurrentUser.UserId);
                            cmd.ExecuteNonQuery();


                            CurrentUser.Name = nameTextBox.Text;
                            UserNameText.Text = nameTextBox.Text;

                            nameDialog.Close();
                            ShowNotification("Имя успешно изменено", NotificationType.Success);
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorText.Text = $"Ошибка: {ex.Message}";
                    errorText.Visibility = Visibility.Visible;
                }
            };

            panel.Children.Add(label);
            panel.Children.Add(nameTextBox);
            panel.Children.Add(errorText);
            panel.Children.Add(saveButton);

            nameDialog.Content = panel;
            nameDialog.ShowDialog();
        }

        private void ChangeAvatarMenuItem_Click(object sender, RoutedEventArgs e)
        {

            if (!CurrentUser.IsLoggedIn)
            {
                ShowLoginPrompt();
                return;
            }


            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Изображения|*.jpg;*.jpeg;*.png;*.gif;*.bmp";
            openFileDialog.Title = "Выберите новую аватарку";

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {

                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(openFileDialog.FileName);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();


                    byte[] imageData;
                    JpegBitmapEncoder encoder = new JpegBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (MemoryStream ms = new MemoryStream())
                    {
                        encoder.Save(ms);
                        imageData = ms.ToArray();
                    }


                    using (MySqlConnection conn = new MySqlConnection("Server=localhost;Port=3306;Database=voteuser;Uid=root"))
                    {
                        conn.Open();
                        string updateQuery = "UPDATE users SET photo = @photo WHERE id = @id";
                        using (MySqlCommand cmd = new MySqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@photo", imageData);
                            cmd.Parameters.AddWithValue("@id", CurrentUser.UserId);
                            cmd.ExecuteNonQuery();


                            CurrentUser.Photo = imageData;
                            TopBarProfileImage.ImageSource = bitmap;
                            ShowNotification("Аватарка успешно изменена", NotificationType.Success);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShowNotification($"Ошибка при изменении аватарки: {ex.Message}", NotificationType.Error);
                }
            }
        }

        private void LogoutMenuItem_Click(object sender, RoutedEventArgs e)
        {

            CurrentUser.Clear();


            UpdateUserInfo();

            ShowNotification("Вы вышли из аккаунта", NotificationType.Info);
        }

        private void ShowLoginPrompt()
        {
            ShowNotification("Для выполнения этого действия необходимо войти в аккаунт", NotificationType.Warning);
        }
        private enum NotificationType
        {
            Info,
            Success,
            Warning,
            Error
        }


        private void ShowNotification(string message, NotificationType type)
        {
            Border notificationBorder = new Border
            {
                Width = 300,
                MinHeight = 60,
                MaxWidth = 400,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15, 10, 15, 10),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 70, 120, 0),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.5,
                    BlurRadius = 10,
                    ShadowDepth = 3
                }
            };
            SolidColorBrush backgroundBrush;
            switch (type)
            {
                case NotificationType.Info:
                    backgroundBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    break;
                case NotificationType.Success:
                    backgroundBrush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    break;
                case NotificationType.Warning:
                    backgroundBrush = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    break;
                case NotificationType.Error:
                    backgroundBrush = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    break;
                default:
                    backgroundBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    break;
            }

            notificationBorder.Background = backgroundBrush;
            TextBlock messageText = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };


            Polygon arrow = new Polygon
            {
                Points = new PointCollection(new Point[] {
                    new Point(0, 0),
                    new Point(0, 15),
                    new Point(10, 7.5)
                }),
                Fill = backgroundBrush,
                Width = 10,
                Height = 15,
                Stretch = Stretch.Fill,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, -10, 0)
            };
            Grid notificationContainer = new Grid();
            notificationContainer.Children.Add(notificationBorder);
            notificationContainer.Children.Add(arrow);


            notificationBorder.Child = messageText;


            Grid mainGrid = this.Content as Grid;
            if (mainGrid != null)
            {
                Panel.SetZIndex(notificationContainer, 9999);
                mainGrid.Children.Add(notificationContainer);


                notificationContainer.Opacity = 0;
                DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
                notificationContainer.BeginAnimation(UIElement.OpacityProperty, fadeIn);


                DispatcherTimer timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(3);
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5));
                    fadeOut.Completed += (sender, args) =>
                    {
                        mainGrid.Children.Remove(notificationContainer);
                    };
                    notificationContainer.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                };
                timer.Start();
            }
        }

        private void CreateVoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentUser.IsLoggedIn)
            {
                ShowNotification("Для создания голосования необходимо войти в аккаунт", NotificationType.Warning);

                Vhod loginWindow = new Vhod();
                if (loginWindow.ShowDialog() == true)
                {
                    UpdateUserInfo();
                    ShowNotification("Теперь вы можете создать голосование", NotificationType.Success);
                    OpenCreateVoteWindow();
                }
                return;
            }
            OpenCreateVoteWindow();
        }
        private void OpenCreateVoteWindow()
        {
            Golos createVoteWindow = new Golos();
            createVoteWindow.Owner = this;
            createVoteWindow.Show();
        }

        private void TemplatesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentUser.IsLoggedIn)
            {
                ShowNotification("Для доступа к шаблонам необходимо войти в систему", NotificationType.Warning);
                Vhod loginWindow = new Vhod();
                if (loginWindow.ShowDialog() == true)
                {
                    UpdateUserInfo();
                    OpenTemplatesWindow();
                }
                return;
            }
            OpenTemplatesWindow();
        }
        private void OpenTemplatesWindow()
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

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentUser.IsLoggedIn)
            {
                ShowNotification("Для поиска тестов необходимо войти в систему", NotificationType.Warning);
                Vhod loginWindow = new Vhod();
                if (loginWindow.ShowDialog() == true)
                {
                    UpdateUserInfo();
                    PerformSearch();
                }
                return;
            }
            PerformSearch();
        }

        private void PerformSearch()
        {
            string searchQuery = SearchBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                ShowNotification("Введите запрос для поиска", NotificationType.Warning);
                return;
            }
            SearchResultWindow existingWindow = null;
            foreach (Window window in Application.Current.Windows)
            {
                if (window is SearchResultWindow searchWindow)
                {
                    existingWindow = searchWindow;
                    break;
                }
            }

            if (existingWindow != null)
            {
                existingWindow.Activate();
                (existingWindow as SearchResultWindow).UpdateSearch(searchQuery);
            }
            else
            {
                SearchResultWindow searchWindow = new SearchResultWindow(searchQuery);
                searchWindow.Owner = this;
                searchWindow.Show();
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchButton_Click(sender, e);
            }
        }

        private void YourQuestionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentUser.IsLoggedIn)
            {
                ShowNotification("Пожалуйста, войдите в систему, чтобы просмотреть свои опросы.", NotificationType.Warning);
                return;
            }
            List<PollSummary> userPolls = GetUserPolls(CurrentUser.UserId);
            if (userPolls == null)
            {
                return;
            }
            if (userPolls.Count == 0)
            {
                ShowNotification("Вы еще не создали ни одного опроса.", NotificationType.Info);
                return;
            }
            UserPollsWindow pollsWindow = new UserPollsWindow(userPolls);
            pollsWindow.Owner = this;
            pollsWindow.ShowDialog();
        }

        private void ViewResultsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentUser.IsLoggedIn)
            {
                ShowNotification("Для просмотра статистики тестов необходимо войти в систему", NotificationType.Warning);
                return;
            }

            bool windowExists = false;
            foreach (Window window in Application.Current.Windows)
            {
                if (window is TEST_Results)
                {
                    windowExists = true;
                    window.Activate();
                    break;
                }
            }

            if (!windowExists)
            {
                TEST_Results resultsWindow = new TEST_Results();
                resultsWindow.Owner = this;
                resultsWindow.Show();
            }
        }

        private List<PollSummary> GetUserPolls(int userId)
        {
            List<PollSummary> polls = new List<PollSummary>();
            string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";
            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT id, title, created_at, description, poll_type, is_active, options FROM polls WHERE user_id = @userId ORDER BY created_at DESC";
                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@userId", userId);
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.HasRows && !TableExists(conn, "polls"))
                            {
                                ShowNotification("Таблица опросов еще не создана. Создайте свой первый опрос.", NotificationType.Info);
                                return polls;
                            }

                            while (reader.Read())
                            {
                                polls.Add(new PollSummary
                                {
                                    Id = reader.GetInt32("id"),
                                    Title = reader.GetString("title"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    Description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description"),
                                    PollType = reader.IsDBNull(reader.GetOrdinal("poll_type")) ? "" : reader.GetString("poll_type"),
                                    IsActive = reader.GetBoolean("is_active"),
                                    Options = reader.IsDBNull(reader.GetOrdinal("options")) ? "" : reader.GetString("options")
                                });
                            }
                        }
                    }
                }
                return polls;
            }
            catch (MySqlException ex)
            {
                if (ex.Number == 1146)
                {
                    ShowNotification("Таблица опросов еще не создана. Создайте свой первый опрос.", NotificationType.Info);
                    return polls;
                }
                else
                {
                    ShowNotification($"Ошибка базы данных при получении опросов: {ex.Message}", NotificationType.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"Произошла ошибка при получении опросов: {ex.Message}", NotificationType.Error);
                return null;
            }
        }
        private bool TableExists(MySqlConnection connection, string tableName)
        {
            try
            {
                string query = "SHOW TABLES LIKE @tableName";
                using (MySqlCommand cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@tableName", tableName);
                    object result = cmd.ExecuteScalar();
                    return result != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (_effectsInitialized && (WindowState == WindowState.Maximized || WindowState == WindowState.Normal))
            {
                Dispatcher.BeginInvoke(new Action(InitializeEffects), DispatcherPriority.Render);
            }
        }

        private void AddButtonPulseEffect(Button button)
        {
            DoubleAnimation pulseAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.8,
                Duration = TimeSpan.FromSeconds(0.1),
                AutoReverse = true
            };

            ScaleTransform transform = new ScaleTransform(1, 1);
            button.RenderTransform = transform;
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnimation);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnimation);
        }

        private void AttachPulseEffectToButtons()
        {
            foreach (var button in FindVisualChildren<Button>(this))
            {
                button.PreviewMouseDown += (s, e) => AddButtonPulseEffect(button);
            }
        }

        private IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void OpenVotingWindow(int pollId, string title)
        {
            try
            {
                VotingWindow votingWindow = new VotingWindow();
                votingWindow.TitleTextBlock.Text = title;

                votingWindow.Owner = this;
                votingWindow.Show();

                ShowNotification($"Открыто голосование: {title}", NotificationType.Info);
            }
            catch (Exception ex)
            {
                ShowNotification($"Ошибка при открытии голосования: {ex.Message}", NotificationType.Error);
            }
        }

        private void OpenVotingResultsWindow(int pollId, string title)
        {
            try
            {
                Voit_Results resultsWindow = new Voit_Results(pollId);
                resultsWindow.Owner = this;
                resultsWindow.Show();
            }
            catch (Exception ex)
            {
                ShowNotification($"Ошибка при открытии результатов голосования: {ex.Message}", NotificationType.Error);
            }
        }

        // Добавляем обработчики событий для ползунков
        private void WaveEffectToggle_Checked(object sender, RoutedEventArgs e)
        {
            _wavesEffectEnabled = true;

            // Инициализация эффекта волны, если он был выключен
            if (_effectsInitialized)
            {
                if (leftFill != null) leftFill.Visibility = Visibility.Visible;
                if (rightFill != null) rightFill.Visibility = Visibility.Visible;
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(InitializeEffects), DispatcherPriority.Render);
            }
        }

        private void WaveEffectToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _wavesEffectEnabled = false;
            if (leftFill != null) leftFill.Visibility = Visibility.Collapsed;
            if (rightFill != null) rightFill.Visibility = Visibility.Collapsed;
        }

        private void ShowVotingResultsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CurrentUser.IsLoggedIn)
            {
                ShowNotification("Для просмотра результатов голосований необходимо войти в систему", NotificationType.Warning);
                Vhod loginWindow = new Vhod();
                if (loginWindow.ShowDialog() == true)
                {
                    UpdateUserInfo();
                    ShowVotingResultsList();
                }
                return;
            }

            ShowVotingResultsList();
        }

        private void ShowVotingResultsList()
        {
            List<PollSummary> userPolls = GetVotingPolls();

            if (userPolls == null || userPolls.Count == 0)
            {
                ShowNotification("Не найдено голосований для просмотра результатов", NotificationType.Info);
                return;
            }

            // Создаем окно выбора голосования для просмотра результатов
            Window resultsListWindow = new Window
            {
                Title = "Выберите голосование",
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            // Создаем интерфейс выбора голосования
            Grid mainGrid = new Grid();
            mainGrid.Margin = new Thickness(20);
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

            // Заголовок
            TextBlock titleBlock = new TextBlock
            {
                Text = "Выберите голосование для просмотра результатов",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(titleBlock, 0);

            // Список голосований
            ListBox pollsListBox = new ListBox
            {
                Margin = new Thickness(0, 0, 0, 15),
                DisplayMemberPath = "Title"
            };
            pollsListBox.ItemsSource = userPolls;
            Grid.SetRow(pollsListBox, 1);

            // Кнопки
            StackPanel buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Button viewButton = new Button
            {
                Content = "Показать результаты",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(0, 0, 10, 0),
                IsEnabled = false
            };

            Button cancelButton = new Button
            {
                Content = "Отмена",
                Padding = new Thickness(15, 8, 15, 8)
            };

            // Обработчики событий
            pollsListBox.SelectionChanged += (s, args) => viewButton.IsEnabled = pollsListBox.SelectedItem != null;

            viewButton.Click += (s, args) =>
            {
                if (pollsListBox.SelectedItem is PollSummary selectedPoll)
                {
                    resultsListWindow.Close();
                    // Открываем окно с результатами выбранного голосования
                    OpenVotingResultsWindow(selectedPoll.Id, selectedPoll.Title);
                }
            };

            cancelButton.Click += (s, args) => resultsListWindow.Close();

            buttonsPanel.Children.Add(viewButton);
            buttonsPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonsPanel, 2);

            // Добавляем элементы в Grid
            mainGrid.Children.Add(titleBlock);
            mainGrid.Children.Add(pollsListBox);
            mainGrid.Children.Add(buttonsPanel);

            // Устанавливаем Grid в качестве содержимого окна
            resultsListWindow.Content = mainGrid;

            // Показываем окно
            resultsListWindow.ShowDialog();
        }

        private List<PollSummary> GetVotingPolls()
        {
            List<PollSummary> polls = new List<PollSummary>();
            string connectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();

                    // Запрос на получение голосований
                    string query = @"
                        SELECT id, title, created_at, poll_type 
                        FROM polls 
                        WHERE poll_type = 'Голосование' 
                        ORDER BY created_at DESC";

                    using (MySqlCommand cmd = new MySqlCommand(query, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                polls.Add(new PollSummary
                                {
                                    Id = reader.GetInt32("id"),
                                    Title = reader.GetString("title"),
                                    CreatedAt = reader.GetDateTime("created_at"),
                                    PollType = reader.GetString("poll_type")
                                });
                            }
                        }
                    }
                }

                return polls;
            }
            catch (Exception ex)
            {
                ShowNotification($"Ошибка при получении списка голосований: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        private void BubbleEffectToggle_Checked(object sender, RoutedEventArgs e)
        {
            _bubblesEffectEnabled = true;
        }

        private void BubbleEffectToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _bubblesEffectEnabled = false;
        }
    }
}
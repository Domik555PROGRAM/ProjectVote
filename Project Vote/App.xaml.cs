using System;
using System.Windows;
using MySql.Data.MySqlClient;

namespace Project_Vote
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string ConnectionString = "Server=localhost;Port=3306;Database=vopros;Uid=root";
        
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Выполняем проверки при запуске
            bool checksSuccessful = true; // StartupChecks.PerformChecks(ConnectionString);
            
            if (!checksSuccessful)
            {
                MessageBox.Show("Обнаружены проблемы с компонентами приложения. " +
                               "Некоторые функции могут работать некорректно.", 
                               "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            
            // Проверяем соединение с базой данных
            try
            {
                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    conn.Open();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось подключиться к базе данных: {ex.Message}\n\n" +
                               "Убедитесь, что MySQL-сервер запущен и доступен.", 
                               "Ошибка соединения с БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

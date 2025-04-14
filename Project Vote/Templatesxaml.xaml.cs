using System;
using System.Collections.Generic;
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

namespace Project_Vote
{
    /// <summary>
    /// Логика взаимодействия для Templatesxaml.xaml
    /// </summary>
    public partial class Templatesxaml : Window
    {
        public Templatesxaml()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
        
        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            // Обработка клика по гиперссылке "здесь"
            MessageBox.Show("Переход к дизайнам опросов");
        }
        
        private void Template_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Получаем текст выбранного шаблона
            if (sender is TextBlock textBlock)
            {
                string templateName = textBlock.Text;
                MessageBox.Show($"Выбран шаблон: {templateName}");
                
                // Здесь будет логика загрузки выбранного шаблона
                // TODO: Реализовать загрузку шаблона
            }
        }
    }
}

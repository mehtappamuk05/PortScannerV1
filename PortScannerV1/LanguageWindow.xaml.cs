using System;
using System.Windows;
using System.Windows.Input;

namespace PortScannerV1
{
    public partial class LanguageWindow : Window
    {
        public LanguageWindow()
        {
            InitializeComponent();
        }

        // Çerçevesiz pencereyi farenin sol tıkıyla sürüklemeyi sağlar
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void BtnTR_Click(object sender, RoutedEventArgs e)
        {
            SetLanguageAndLaunch("Dictionary-TR.xaml");
        }

        private void BtnEN_Click(object sender, RoutedEventArgs e)
        {
            SetLanguageAndLaunch("Dictionary-EN.xaml");
        }

        private void SetLanguageAndLaunch(string dictName)
        {
            Application.Current.Resources.MergedDictionaries.Clear();
            ResourceDictionary dict = new ResourceDictionary();
            dict.Source = new Uri(dictName, UriKind.Relative);
            Application.Current.Resources.MergedDictionaries.Add(dict);

            MainWindow main = new MainWindow();
            main.Show();
            this.Close();
        }
    }
}
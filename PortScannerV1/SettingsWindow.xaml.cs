using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace PortScannerV1
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            if (AppSettings.Concurrency == 10) rbSlow.IsChecked = true;
            else if (AppSettings.Concurrency == 200) rbAggressive.IsChecked = true;
            else rbNormal.IsChecked = true;

            chkEnableMonitor.IsChecked = AppSettings.AutoScanEnabled;
            pnlMonitorSettings.IsEnabled = AppSettings.AutoScanEnabled;
            txtMonitorIP.Text = AppSettings.AutoScanIP;
            txtInterval.Text = AppSettings.AutoScanInterval.ToString();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ChkEnableMonitor_Click(object sender, RoutedEventArgs e)
        {
            pnlMonitorSettings.IsEnabled = chkEnableMonitor.IsChecked == true;
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (chkEnableMonitor.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(txtMonitorIP.Text) || !txtMonitorIP.Text.Contains("."))
                {
                    CustomMessageBox.Show("Lütfen izlenecek geçerli bir IP adresi giriniz.", "Hata", true);
                    return;
                }
            }

            if (rbSlow.IsChecked == true) AppSettings.Concurrency = 10;
            else if (rbAggressive.IsChecked == true) AppSettings.Concurrency = 200;
            else AppSettings.Concurrency = 50;

            AppSettings.AutoScanEnabled = chkEnableMonitor.IsChecked == true;
            AppSettings.AutoScanIP = txtMonitorIP.Text;
            if (int.TryParse(txtInterval.Text, out int interval))
            {
                AppSettings.AutoScanInterval = interval;
            }

            
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow != null)
            {
                mainWindow.UpdateAutoScanTimer();
            }

            CustomMessageBox.Show("Ayarlar başarıyla kaydedildi ve uygulandı!", "Bilgi", false);
            this.Close();
        }
    }
}
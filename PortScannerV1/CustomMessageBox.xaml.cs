using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PortScannerV1
{
    public partial class CustomMessageBox : Window
    {
        public CustomMessageBox(string message, string title, bool isError = false)
        {
            InitializeComponent();
            txtMessage.Text = message;
            txtTitle.Text = title.ToUpper();

            if (isError)
            {
                txtIcon.Text = "⚠️";
                txtTitle.Foreground = new SolidColorBrush(Color.FromRgb(255, 23, 68));  
                btnOk.Foreground = new SolidColorBrush(Color.FromRgb(255, 23, 68)); 
            }
            else
            {
                txtIcon.Text = "ℹ️";
                txtTitle.Foreground = new SolidColorBrush(Color.FromRgb(0, 229, 255));  
                btnOk.Foreground = new SolidColorBrush(Color.FromRgb(0, 229, 255)); 
            }
            btnOk.Content = Application.Current.TryFindResource("BtnOk")?.ToString() ?? "OK";
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }


        public static void Show(string message, string title = "Bilgi", bool isError = false)
        {
            CustomMessageBox box = new CustomMessageBox(message, title, isError);
            box.ShowDialog();
        }
    }
}



using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PortScannerV1
{
    public partial class HistoryWindow : Window
    {
        DatabaseManager dbManager = new DatabaseManager();

        public HistoryWindow()
        {
            InitializeComponent();
            LoadScans();
        }


        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {

            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close(); 



        private void LoadScans()
        {
            DataTable scans = dbManager.GetScans();
            dgScans.ItemsSource = scans.DefaultView;
        }

        private void DgScans_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgScans.SelectedItem is DataRowView row)
            {
                long scanId = (long)row["Id"];
                DataTable details = dbManager.GetPortResults(scanId);


                foreach (DataRow dr in details.Rows)
                {

                    string currentStatus = dr["Status"]?.ToString() ?? "";
                    if (currentStatus == "Açık" || currentStatus == "Open")
                    {
                        dr["Status"] = Application.Current.TryFindResource("StatusOpen")?.ToString() ?? "Open";
                    }

                    string currentRisk = dr["Risk"]?.ToString() ?? "";
                    if (currentRisk == "TEHLİKE" || currentRisk == "HIGH RISK" || currentRisk == "High")
                    {
                        dr["Risk"] = Application.Current.TryFindResource("RiskHigh")?.ToString() ?? "HIGH RISK";
                    }
                    else if (currentRisk == "GÜVENLİ" || currentRisk == "SAFE" || currentRisk == "Low")
                    {
                        dr["Risk"] = Application.Current.TryFindResource("RiskSafe")?.ToString() ?? "SAFE";
                    }
                }

                dgDetails.ItemsSource = details.DefaultView;
            }
        }
    }
}
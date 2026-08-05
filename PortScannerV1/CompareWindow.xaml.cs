using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace PortScannerV1
{
    public partial class CompareWindow : Window
    {
        DatabaseManager dbManager = new DatabaseManager();
        ObservableCollection<PortResult> currentResults;

        public CompareWindow(string targetIp, ObservableCollection<PortResult> currentScanResults)
        {
            InitializeComponent();
            currentResults = currentScanResults;
            LoadPastScans(targetIp);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();

        private void LoadPastScans(string ip)
        {
            DataTable scans = dbManager.GetScans();
            var filteredRows = scans.AsEnumerable().Where(r => r.Field<string>("TargetIP") == ip);

            if (filteredRows.Any())
            {
                cmbScans.ItemsSource = filteredRows.CopyToDataTable().DefaultView;
                cmbScans.DisplayMemberPath = "ScanDate";
                cmbScans.SelectedValuePath = "Id";
            }
            else
            {
                MessageBox.Show($"Veritabanında {ip} adresine ait geçmiş bir tarama bulunamadı.", "Bilgi", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CmbScans_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbScans.SelectedValue != null)
            {
                long scanId = Convert.ToInt64(cmbScans.SelectedValue);
                DataTable oldPorts = dbManager.GetPortResults(scanId);

                ObservableCollection<CompareResult> comparisons = new ObservableCollection<CompareResult>();

                foreach (var currentPort in currentResults)
                {
                    var oldPortRow = oldPorts.AsEnumerable().FirstOrDefault(r => Convert.ToInt32(r["Port"]) == currentPort.Port);

                    string oldStatus = oldPortRow != null ? oldPortRow["Status"]?.ToString() ?? "Kapalı" : "Kapalı";
                    string analysis = "";

                    if (oldStatus != currentPort.Status)
                    {
                        if (currentPort.Status == "Açık" || currentPort.Status == "Open")
                            analysis = "⚠️ YENİ ZAFİYET! Port son taramadan bu yana açılmış.";
                        else
                            analysis = "✅ GÜVENLİ! Port kapatılmış.";
                    }
                    else
                    {
                        analysis = "➖ Değişiklik Yok";
                    }

                    comparisons.Add(new CompareResult
                    {
                        Port = currentPort.Port,
                        OldStatus = oldStatus,
                        NewStatus = currentPort.Status,
                        Analysis = analysis
                    });
                }
                dgCompare.ItemsSource = comparisons;
            }
        }
    }

    public class CompareResult
    {
        public int Port { get; set; }
        public string OldStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public string Analysis { get; set; } = string.Empty;
    }
}
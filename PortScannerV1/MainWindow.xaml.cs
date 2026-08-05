using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using iText.Kernel.Pdf;
using iText.Layout;
using System.Windows.Threading;
using System.Data;
using System.Linq;

namespace PortScannerV1
{
    // Uygulama Ayarlarını Hafızada Tutan Sınıf
    public static class AppSettings
    {
        public static int Concurrency = 50; // Varsayılan 50 Thread
        public static bool AutoScanEnabled = false;
        public static string AutoScanIP = "127.0.0.1";
        public static int AutoScanInterval = 60; // Dakika
    }

    public partial class MainWindow : Window
    {
        ObservableCollection<PortResult> scanResults = new ObservableCollection<PortResult>();
        bool isScanning = false;
        DatabaseManager dbManager = new DatabaseManager();
        DispatcherTimer autoScanTimer = new DispatcherTimer(); // Otomatik tarama zamanlayıcısı

        public MainWindow()
        {
            InitializeComponent();
            dgResults.ItemsSource = scanResults;

            // Zamanlayıcıyı kur
            autoScanTimer.Tick += AutoScanTimer_Tick;
        }

        private string GetLang(string key)
        {
            return Application.Current.TryFindResource(key)?.ToString() ?? key;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => this.WindowState = this.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void BtnChangeLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (isScanning)
            {
                CustomMessageBox.Show(GetLang("MsgScanFirst"), GetLang("TitleWarning"), true);
                return;
            }

            LanguageWindow langWindow = new LanguageWindow();
            langWindow.Show();
            this.Close();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (isScanning)
            {
                CustomMessageBox.Show(GetLang("MsgScanFirst"), GetLang("TitleWarning"), true);
                return;
            }

            SettingsWindow settings = new SettingsWindow();
            settings.ShowDialog();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (isScanning) return;

            string ip = txtIP.Text;
            if (!int.TryParse(txtStartPort.Text, out int startPort) || !int.TryParse(txtEndPort.Text, out int endPort) || startPort > endPort)
            {
                CustomMessageBox.Show(GetLang("MsgInvalidPort"), GetLang("TitleError"), true);
                return;
            }

            isScanning = true;
            btnStart.Background = new SolidColorBrush(Color.FromRgb(255, 23, 68));
            txtBtnStart.Text = "SCANNING...";

            scanResults.Clear();
            rtbLogs.Document.Blocks.Clear();

            int totalPorts = endPort - startPort + 1;
            int openCount = 0, closedCount = 0;

            lblTotalPorts.Text = totalPorts.ToString();
            lblOpenPorts.Text = "0";
            lblClosedPorts.Text = "0";
            progressBar.Maximum = totalPorts;
            progressBar.Value = 0;
            lblScanningIp.Text = $"Scanning... {ip}";

            brdRiskIndicator.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 230, 118));
            lblRisk.Foreground = new SolidColorBrush(Color.FromRgb(0, 230, 118));
            lblRisk.SetResourceReference(TextBlock.TextProperty, "RiskSafe");

            Log($"[{DateTime.Now:HH:mm:ss}] Tarama başlatıldı: {ip}", Brushes.Cyan);

            Stopwatch sw = Stopwatch.StartNew();

            var progress = new Progress<int>(current =>
            {
                progressBar.Value = current;
                lblProgressText.Text = $"Port {startPort + current - 1}/{endPort} (%{(int)((current / (double)totalPorts) * 100)})";
                lblScanTime.Text = sw.Elapsed.ToString(@"hh\:mm\:ss");
            });

            await PerformScanAsync(ip, startPort, endPort, progress, (port, isOpen, banner) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (isOpen)
                    {
                        openCount++;
                        lblOpenPorts.Text = openCount.ToString();

                        string risk = GetRisk(port);

                        if (risk == "High")
                        {
                            brdRiskIndicator.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 23, 68));
                            lblRisk.Foreground = new SolidColorBrush(Color.FromRgb(255, 23, 68));
                            lblRisk.SetResourceReference(TextBlock.TextProperty, "RiskHigh");
                        }

                        string service = GetKnownService(port);
                        string statusText = Application.Current.TryFindResource("StatusOpen")?.ToString() ?? "Open";
                        string riskText = Application.Current.TryFindResource(risk == "High" ? "RiskHigh" : "RiskSafe")?.ToString() ?? risk;

                        scanResults.Add(new PortResult { Port = port, Status = statusText, Service = service, Protocol = "TCP", Risk = riskText, Banner = banner });
                        Log($"[{DateTime.Now:HH:mm:ss}] Port {port} {statusText.ToUpper()} ({service}) -> {banner}", Brushes.Red);
                    }
                    else
                    {
                        closedCount++;
                        lblClosedPorts.Text = closedCount.ToString();
                    }
                });
            });

            sw.Stop();
            Log($"[{DateTime.Now:HH:mm:ss}] Tarama tamamlandı.", Brushes.Cyan);

            isScanning = false;
            btnStart.Background = new SolidColorBrush(Color.FromRgb(0, 229, 255));
            txtBtnStart.Text = "START SCAN";
            lblScanningIp.Text = "Bekleniyor...";
        }

        private async Task PerformScanAsync(string ip, int start, int end, IProgress<int> progress, Action<int, bool, string> reportResult)
        {
            int scannedPorts = 0;
            int timeoutMs = 1500;

            // Hız artık Ayarlar'dan (AppSettings) dinamik olarak çekiliyor
            int maxConcurrency = AppSettings.Concurrency;

            using var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task>();

            for (int i = start; i <= end; i++)
            {
                int currentPort = i;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    bool isOpen = false;
                    string banner = "-";

                    try
                    {
                        using var client = new TcpClient();
                        var connectTask = client.ConnectAsync(ip, currentPort);
                        var timeoutTask = Task.Delay(timeoutMs);

                        var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                        if (completedTask == connectTask && client.Connected)
                        {
                            isOpen = true;

                            try
                            {
                                using var stream = client.GetStream();
                                byte[] buffer = new byte[256];

                                await Task.Delay(200);
                                if (stream.DataAvailable)
                                {
                                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                    banner = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                                }
                                else
                                {
                                    byte[] request = Encoding.ASCII.GetBytes("HEAD / HTTP/1.0\r\n\r\n");
                                    await stream.WriteAsync(request, 0, request.Length);

                                    await Task.Delay(200);
                                    if (stream.DataAvailable)
                                    {
                                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                                        banner = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(banner))
                                {
                                    banner = banner.Replace("\r", "").Replace("\n", " | ").Trim();
                                    if (banner.Length > 50) banner = banner.Substring(0, 47) + "...";
                                }
                                else
                                {
                                    banner = "-";
                                }
                            }
                            catch
                            {
                                banner = "-";
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        semaphore.Release();
                        reportResult(currentPort, isOpen, banner);
                        int currentCount = Interlocked.Increment(ref scannedPorts);
                        progress.Report(currentCount);
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }

        private void Log(string message, SolidColorBrush color)
        {
            Run run = new Run(message) { Foreground = color };
            Paragraph p = new Paragraph(run) { Margin = new Thickness(0, 2, 0, 2) };
            rtbLogs.Document.Blocks.Add(p);
            rtbLogs.ScrollToEnd();
        }

        private string GetKnownService(int port)
        {
            return port switch { 20 => "FTP", 21 => "FTP", 22 => "SSH", 23 => "Telnet", 25 => "SMTP", 42 => "WINS", 53 => "DNS", 80 => "HTTP", 88 => "Kerberos", 110 => "POP3", 135 => "RPC", 139 => "NetBIOS", 389 => "LDAP", 443 => "HTTPS", 445 => "SMB", 464 => "kpasswd", 593 => "RPC-HTTP", 636 => "LDAPS", 3306 => "MySQL", 3389 => "RDP", _ => "-" };
        }

        private string GetRisk(int port)
        {
            return port switch { 21 => "High", 22 => "Medium", 23 => "High", 135 => "Medium", 139 => "High", 445 => "High", 3389 => "High", _ => "Low" };
        }

        private void BtnExportCSV_Click(object sender, RoutedEventArgs e)
        {
            if (scanResults.Count == 0) { CustomMessageBox.Show(GetLang("MsgNoResultsExport"), GetLang("TitleWarning"), true); return; }

            SaveFileDialog sfd = new SaveFileDialog() { Filter = "CSV Dosyası|*.csv", FileName = "PortTarama_Raporu.csv" };
            if (sfd.ShowDialog() == true)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Port,Status,Service,Protocol,Risk,Banner");
                foreach (var item in scanResults)
                {
                    sb.AppendLine($"{item.Port},{item.Status},{item.Service},{item.Protocol},{item.Risk},{item.Banner}");
                }
                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                CustomMessageBox.Show(GetLang("MsgCsvSuccess"), GetLang("TitleInfo"), false);
            }
        }

        private void BtnSaveDB_Click(object sender, RoutedEventArgs e)
        {
            if (scanResults.Count == 0) { CustomMessageBox.Show(GetLang("MsgNoResultsSave"), GetLang("TitleWarning"), true); return; }
            try
            {
                dbManager.SaveScanResult(txtIP.Text, scanResults);
                CustomMessageBox.Show(GetLang("MsgDbSuccess"), GetLang("TitleSuccess"), false);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(GetLang("MsgDbError") + ex.Message, GetLang("TitleError"), true);
            }
        }

        private void BtnExportPDF_Click(object sender, RoutedEventArgs e)
        {
            if (scanResults.Count == 0) { CustomMessageBox.Show(GetLang("MsgNoResultsExport"), GetLang("TitleWarning"), true); return; }

            SaveFileDialog sfd = new SaveFileDialog() { Filter = "PDF Dosyası|*.pdf", FileName = "PortTarama_Raporu.pdf" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (PdfWriter writer = new PdfWriter(sfd.FileName))
                    using (PdfDocument pdf = new PdfDocument(writer))
                    using (Document document = new Document(pdf))
                    {
                        document.Add(new iText.Layout.Element.Paragraph("PORT SCANNER v1 - Tarama Raporu")
                            .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                            .SetFontSize(18));

                        document.Add(new iText.Layout.Element.Paragraph($"Hedef IP: {txtIP.Text}\nTarih: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n"));

                        iText.Layout.Element.Table table = new iText.Layout.Element.Table(6, true);
                        table.AddHeaderCell("Port");
                        table.AddHeaderCell("Durum");
                        table.AddHeaderCell("Servis");
                        table.AddHeaderCell("Protokol");
                        table.AddHeaderCell("Risk");
                        table.AddHeaderCell("Banner");

                        foreach (var item in scanResults)
                        {
                            table.AddCell(item.Port.ToString());
                            table.AddCell(item.Status);
                            table.AddCell(item.Service);
                            table.AddCell(item.Protocol);
                            table.AddCell(item.Risk);
                            table.AddCell(item.Banner);
                        }
                        document.Add(table);
                    }
                    CustomMessageBox.Show(GetLang("MsgPdfSuccess"), GetLang("TitleInfo"), false);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(GetLang("MsgPdfError") + ex.Message, GetLang("TitleError"), true);
                }
            }
        }

        private void BtnCompare_Click(object sender, RoutedEventArgs e)
        {
            if (scanResults.Count == 0)
            {
                CustomMessageBox.Show(GetLang("MsgScanFirst"), GetLang("TitleWarning"), true);
                return;
            }

            CompareWindow compareWindow = new CompareWindow(txtIP.Text, scanResults);
            compareWindow.ShowDialog();
        }

        private void BtnHistory_Click(object sender, RoutedEventArgs e)
        {
            HistoryWindow historyWindow = new HistoryWindow();
            historyWindow.ShowDialog();
        }

        // ==========================================
        // ARKA PLAN ZAMANLAYICI (TIMER) METODLARI
        // ==========================================

        public void UpdateAutoScanTimer()
        {
            if (AppSettings.AutoScanEnabled)
            {
                autoScanTimer.Interval = TimeSpan.FromMinutes(AppSettings.AutoScanInterval);
                autoScanTimer.Start();
                Log($"[SİSTEM] Otomatik İzleme Aktif: {AppSettings.AutoScanIP} (Her {AppSettings.AutoScanInterval} dakikada bir gizli tarama yapılacak)", Brushes.Yellow);
            }
            else
            {
                autoScanTimer.Stop();
                Log($"[SİSTEM] Otomatik İzleme Kapatıldı.", Brushes.Gray);
            }
        }

        private async void AutoScanTimer_Tick(object? sender, EventArgs e)
        {
            if (isScanning) return;

            Log($"[SİSTEM] {AppSettings.AutoScanIP} için arka plan rutin taraması başlatıldı...", Brushes.Orange);

            DataTable scans = dbManager.GetScans();
            var lastScanRow = scans.AsEnumerable()
                                   .Where(r => r.Field<string>("TargetIP") == AppSettings.AutoScanIP)
                                   .OrderByDescending(r => r.Field<long>("Id"))
                                   .FirstOrDefault();

            ObservableCollection<PortResult> autoResults = new ObservableCollection<PortResult>();
            var silentProgress = new Progress<int>();

            await PerformScanAsync(AppSettings.AutoScanIP, 1, 1024, silentProgress, (port, isOpen, banner) =>
            {
                if (isOpen)
                {
                    string service = GetKnownService(port);
                    autoResults.Add(new PortResult { Port = port, Status = "Açık", Service = service, Protocol = "TCP", Risk = GetRisk(port), Banner = banner });
                }
            });

            bool newVulnerabilityFound = false;
            string newPorts = "";

            if (lastScanRow != null)
            {
                long lastScanId = lastScanRow.Field<long>("Id");
                DataTable oldPorts = dbManager.GetPortResults(lastScanId);

                foreach (var currentPort in autoResults)
                {
                    var oldPortRow = oldPorts.AsEnumerable().FirstOrDefault(r => Convert.ToInt32(r["Port"]) == currentPort.Port);
                    string oldStatus = oldPortRow?["Status"]?.ToString() ?? "Kapalı";

                    if (oldStatus != "Açık" && oldStatus != "Open")
                    {
                        newVulnerabilityFound = true;
                        newPorts += $"{currentPort.Port} ";
                    }
                }
            }

            dbManager.SaveScanResult(AppSettings.AutoScanIP, autoResults);

            if (newVulnerabilityFound)
            {
                Log($"[ALARM] YENİ ZAFİYET TESPİT EDİLDİ! Yeni açılan portlar: {newPorts}", Brushes.Red);

                // Uyumsuz Windows bildirimi yerine, kendi siberpunk uyarı penceremizi tetikliyoruz
                CustomMessageBox.Show($"Dikkat! {AppSettings.AutoScanIP} hedefinde yeni portlar açıldı:\n{newPorts}", "⚠️ YENİ ZAFİYET!", true);
            }
            else
            {
                Log($"[SİSTEM] Arka plan taraması temiz. Ağ durumunda bir değişiklik yok.", Brushes.LimeGreen);
            }
        }
    }

    public class PortResult
    {
        public int Port { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Service { get; set; } = string.Empty;
        public string Protocol { get; set; } = string.Empty;
        public string Risk { get; set; } = string.Empty;
        public string Banner { get; set; } = string.Empty;
    }
}
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace PortScannerV1
{
    public class DatabaseManager
    {
        private string dbFile = "ScannerHistory.sqlite";
        private string connectionString;

        public DatabaseManager()
        {
            connectionString = $"Data Source={dbFile};Version=3;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            if (!File.Exists(dbFile))
            {
                SQLiteConnection.CreateFile(dbFile);
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();

                    // Tarama özetlerini tutan tablo
                    string createScansTable = @"CREATE TABLE IF NOT EXISTS Scans (
                                                Id INTEGER PRIMARY KEY AUTOINCREMENT, 
                                                ScanDate TEXT, 
                                                TargetIP TEXT)";
                    new SQLiteCommand(createScansTable, connection).ExecuteNonQuery();

                    // Port detaylarını tutan tablo
                    string createResultsTable = @"CREATE TABLE IF NOT EXISTS PortResults (
                                                  Id INTEGER PRIMARY KEY AUTOINCREMENT, 
                                                  ScanId INTEGER, 
                                                  Port INTEGER, 
                                                  Status TEXT, 
                                                  Service TEXT, 
                                                  Risk TEXT,
                                                  FOREIGN KEY(ScanId) REFERENCES Scans(Id))";
                    new SQLiteCommand(createResultsTable, connection).ExecuteNonQuery();
                }
            }
        }

        public void SaveScanResult(string ip, ObservableCollection<PortResult> results)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    // Önce ana taramayı kaydet
                    string insertScan = "INSERT INTO Scans (ScanDate, TargetIP) VALUES (@date, @ip); SELECT last_insert_rowid();";
                    using (var cmdScan = new SQLiteCommand(insertScan, connection))
                    {
                        cmdScan.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        cmdScan.Parameters.AddWithValue("@ip", ip);
                        long scanId = (long)cmdScan.ExecuteScalar();

                        // Sonra tüm açık/kapalı port detaylarını bu taramaya bağlayarak kaydet
                        string insertResult = "INSERT INTO PortResults (ScanId, Port, Status, Service, Risk) VALUES (@scanId, @port, @status, @service, @risk)";
                        using (var cmdResult = new SQLiteCommand(insertResult, connection))
                        {
                            foreach (var item in results)
                            {
                                cmdResult.Parameters.AddWithValue("@scanId", scanId);
                                cmdResult.Parameters.AddWithValue("@port", item.Port);
                                cmdResult.Parameters.AddWithValue("@status", item.Status);
                                cmdResult.Parameters.AddWithValue("@service", item.Service);
                                cmdResult.Parameters.AddWithValue("@risk", item.Risk);
                                cmdResult.ExecuteNonQuery();
                            }
                        }
                    }
                    transaction.Commit();
                }
            }
        }

        // Taramaların listesini (Tarih ve IP) getirir
        public DataTable GetScans()
        {
            DataTable dt = new DataTable();
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT Id, ScanDate, TargetIP FROM Scans ORDER BY Id DESC";
                using (var cmd = new SQLiteCommand(query, connection))
                {
                    using (var adapter = new SQLiteDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                }
            }
            return dt;
        }

        // Seçilen taramanın port detaylarını getirir
        public DataTable GetPortResults(long scanId)
        {
            DataTable dt = new DataTable();
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string query = "SELECT Port, Status, Service, Risk FROM PortResults WHERE ScanId = @scanId";
                using (var cmd = new SQLiteCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@scanId", scanId);
                    using (var adapter = new SQLiteDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                }
            }
            return dt;
        }
    }
}
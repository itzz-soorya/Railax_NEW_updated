using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using UserModule;
using UserModule.Models;

public static class OfflineBookingStorage
{
    // Store database in AppData\Local instead of Program Files to avoid permission issues
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "Railax");
    private static readonly string DbPath = Path.Combine(AppDataFolder, "offline_bookings.db");
    private const string CreateBookingApiUrl = "https://railway-worker-backend.artechnology.pro/api/Booking/create";

    static OfflineBookingStorage()
    {
        // Ensure the directory exists
        Directory.CreateDirectory(AppDataFolder);
        InitializeDatabase();
    }
    // All the datas are stored in a local Sqlite database when offline
    private static void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        // Create Bookings table
        string createBookingsTable = @"
        CREATE TABLE IF NOT EXISTS Bookings (
            booking_id TEXT PRIMARY KEY,
            worker_id TEXT,
            guest_name TEXT,
            phone_number TEXT,
            number_of_persons INTEGER,
            booking_type TEXT,
            total_hours INTEGER,
            booking_date TEXT,
            in_time TEXT,
            out_time TEXT,
            proof_type TEXT,
            proof_id TEXT,
            price_per_person REAL,
            total_amount REAL,
            paid_amount REAL,
            balance_amount REAL,
            payment_method TEXT,
            created_at TEXT,
            updated_at TEXT,
            status TEXT,
            IsSynced INTEGER DEFAULT 0
        );";

        using (var cmd = new SqliteCommand(createBookingsTable, connection))
        {
            cmd.ExecuteNonQuery();
        }

        // Create Settings table with type1-4 and their amounts
        string createSettingsTable = @"
        CREATE TABLE IF NOT EXISTS Settings (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            admin_id TEXT,
            type1 TEXT,
            type1_amount REAL,
            type2 TEXT,
            type2_amount REAL,
            type3 TEXT,
            type3_amount REAL,
            type4 TEXT,
            type4_amount REAL,
            advance_payment_enabled INTEGER DEFAULT 0,
            default_advance_percentage REAL DEFAULT 0,
            last_synced TEXT
        );";

        using (var cmd = new SqliteCommand(createSettingsTable, connection))
        {
            cmd.ExecuteNonQuery();
        }

        // Migrate existing Settings table to add new columns if they don't exist
        try
        {
            // Check if columns exist and add them if missing
            string checkColumn1 = "SELECT COUNT(*) FROM pragma_table_info('Settings') WHERE name='advance_payment_enabled'";
            using (var cmd = new SqliteCommand(checkColumn1, connection))
            {
                var result = cmd.ExecuteScalar();
                if (result != null && Convert.ToInt32(result) == 0)
                {
                    // Column doesn't exist, add it
                    string alterTable1 = "ALTER TABLE Settings ADD COLUMN advance_payment_enabled INTEGER DEFAULT 0";
                    using (var alterCmd = new SqliteCommand(alterTable1, connection))
                    
                    {
                        alterCmd.ExecuteNonQuery();
                        Logger.Log("Added column 'advance_payment_enabled' to Settings table");
                    }
                }
            }

            string checkColumn2 = "SELECT COUNT(*) FROM pragma_table_info('Settings') WHERE name='default_advance_percentage'";
            using (var cmd = new SqliteCommand(checkColumn2, connection))
            {
                var result = cmd.ExecuteScalar();
                if (result != null && Convert.ToInt32(result) == 0)
                {
                    // Column doesn't exist, add it
                    string alterTable2 = "ALTER TABLE Settings ADD COLUMN default_advance_percentage REAL DEFAULT 0";
                    using (var alterCmd = new SqliteCommand(alterTable2, connection))
                    {
                        alterCmd.ExecuteNonQuery();
                        Logger.Log("Added column 'default_advance_percentage' to Settings table");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    // Online Sync
    public static void SaveOffline(Booking1 booking)
    {
        if (booking == null) return;

        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        string insert = @"
        INSERT OR REPLACE INTO Bookings
        (booking_id, worker_id, guest_name, phone_number, number_of_persons, booking_type, total_hours,
         booking_date, in_time, out_time, proof_type, proof_id, price_per_person, total_amount, paid_amount,
         balance_amount, payment_method, created_at, updated_at, status, IsSynced)
        VALUES (@booking_id, @worker_id, @guest_name, @phone_number, @number_of_persons, @booking_type, @total_hours,
                @booking_date, @in_time, @out_time, @proof_type, @proof_id, @price_per_person, @total_amount, 
                @paid_amount, @balance_amount, @payment_method, @created_at, @updated_at, @status, 0);";

        using var cmd = new SqliteCommand(insert, connection);
        cmd.Parameters.AddWithValue("@booking_id", booking.booking_id);
        cmd.Parameters.AddWithValue("@worker_id", booking.worker_id ?? "");
        cmd.Parameters.AddWithValue("@guest_name", booking.guest_name ?? "");
        cmd.Parameters.AddWithValue("@phone_number", booking.phone_number ?? "");
        cmd.Parameters.AddWithValue("@number_of_persons", booking.number_of_persons);
        cmd.Parameters.AddWithValue("@booking_type", booking.booking_type ?? "");
        cmd.Parameters.AddWithValue("@total_hours", booking.total_hours);
        cmd.Parameters.AddWithValue("@booking_date", booking.booking_date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@in_time", booking.in_time.ToString());
        cmd.Parameters.AddWithValue("@out_time", booking.out_time?.ToString() ?? "");
        cmd.Parameters.AddWithValue("@proof_type", booking.proof_type ?? "");
        cmd.Parameters.AddWithValue("@proof_id", booking.proof_id ?? "");
        cmd.Parameters.AddWithValue("@price_per_person", booking.price_per_person);
        cmd.Parameters.AddWithValue("@total_amount", booking.total_amount);
        cmd.Parameters.AddWithValue("@paid_amount", booking.paid_amount);
        cmd.Parameters.AddWithValue("@balance_amount", booking.balance_amount);
        cmd.Parameters.AddWithValue("@payment_method", booking.payment_method ?? "Cash");
        cmd.Parameters.AddWithValue("@created_at", booking.created_at?.ToString("yyyy-MM-dd HH:mm:ss") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@updated_at", booking.updated_at?.ToString("yyyy-MM-dd HH:mm:ss") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@status",
            string.IsNullOrWhiteSpace(booking.status)
            ? "active"
            : booking.status.ToLower());
        cmd.ExecuteNonQuery();
    }

    // New method: Save booking with online-first approach
    public static async Task<bool> SaveBookingAsync(Booking1 booking, bool showMessages = true)
    {
        if (booking == null) return false;

        // Check if network is available
        bool isOnline = NetworkInterface.GetIsNetworkAvailable();

        if (isOnline)
        {
            try
            {
                // Try to save directly to API
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                
                var bookingData = new[]
                {
                    booking
                };
                
                var json = JsonConvert.SerializeObject(bookingData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(CreateBookingApiUrl, content);
                
                if (response.IsSuccessStatusCode)
                {
                    // Save to local database as synced (IsSynced = 1)
                    SaveOfflineAsSynced(booking);
                    
                    if (showMessages)
                    {
                        MessageBox.Show("✅ Booking saved online successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return true;
                }
                else
                {
                    // API rejected, save offline
                    string responseBody = await response.Content.ReadAsStringAsync();
                    Logger.Log($"API rejected booking: {response.StatusCode} - {responseBody}");
                    SaveOffline(booking);
                    
                    if (showMessages)
                    {
                        MessageBox.Show($"Booking saved locally.\nWill sync when connection is restored.", 
                            "Saved Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Network error, save offline
                Logger.Log($"Error saving booking online: {ex.Message}");
                SaveOffline(booking);
                
                if (showMessages)
                {
                    MessageBox.Show($"Booking saved locally.\nWill sync when connection is restored.", 
                        "Saved Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                return false;
            }
        }
        else
        {
            // No network, save offline
            SaveOffline(booking);
            
            if (showMessages)
            {
                MessageBox.Show("📴 No internet connection. Booking saved locally.\nWill sync when connection is restored.", 
                    "Saved Offline", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return false;
        }
    }

    // Helper method to save booking as already synced
    private static void SaveOfflineAsSynced(Booking1 booking)
    {
        if (booking == null) return;

        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        string insert = @"
        INSERT OR REPLACE INTO Bookings
        (booking_id, worker_id, guest_name, phone_number, number_of_persons, booking_type, total_hours,
         booking_date, in_time, out_time, proof_type, proof_id, price_per_person, total_amount, paid_amount,
         balance_amount, payment_method, created_at, updated_at, status, IsSynced)
        VALUES (@booking_id, @worker_id, @guest_name, @phone_number, @number_of_persons, @booking_type, @total_hours,
                @booking_date, @in_time, @out_time, @proof_type, @proof_id, @price_per_person, @total_amount, 
                @paid_amount, @balance_amount, @payment_method, @created_at, @updated_at, @status, 1);";

        using var cmd = new SqliteCommand(insert, connection);
        cmd.Parameters.AddWithValue("@booking_id", booking.booking_id);
        cmd.Parameters.AddWithValue("@worker_id", booking.worker_id ?? "");
        cmd.Parameters.AddWithValue("@guest_name", booking.guest_name ?? "");
        cmd.Parameters.AddWithValue("@phone_number", booking.phone_number ?? "");
        cmd.Parameters.AddWithValue("@number_of_persons", booking.number_of_persons);
        cmd.Parameters.AddWithValue("@booking_type", booking.booking_type ?? "");
        cmd.Parameters.AddWithValue("@total_hours", booking.total_hours);
        cmd.Parameters.AddWithValue("@booking_date", booking.booking_date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@in_time", booking.in_time.ToString());
        cmd.Parameters.AddWithValue("@out_time", booking.out_time?.ToString() ?? "");
        cmd.Parameters.AddWithValue("@proof_type", booking.proof_type ?? "");
        cmd.Parameters.AddWithValue("@proof_id", booking.proof_id ?? "");
        cmd.Parameters.AddWithValue("@price_per_person", booking.price_per_person);
        cmd.Parameters.AddWithValue("@total_amount", booking.total_amount);
        cmd.Parameters.AddWithValue("@paid_amount", booking.paid_amount);
        cmd.Parameters.AddWithValue("@balance_amount", booking.balance_amount);
        cmd.Parameters.AddWithValue("@payment_method", booking.payment_method ?? "Cash");
        cmd.Parameters.AddWithValue("@created_at", booking.created_at?.ToString("yyyy-MM-dd HH:mm:ss") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@updated_at", booking.updated_at?.ToString("yyyy-MM-dd HH:mm:ss") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@status",
            string.IsNullOrWhiteSpace(booking.status)
            ? "active"
            : booking.status.ToLower());
        cmd.ExecuteNonQuery();
    }

    // Get count of pending bookings to sync (both new and updated)
    public static async Task<int> GetPendingSyncCountAsync()
    {
        return await Task.Run(() =>
        {
            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            // Count both IsSynced = 0 (new/offline bookings) and IsSynced = 2 (updated bookings)
            string countQuery = "SELECT COUNT(*) FROM Bookings WHERE IsSynced = 0 OR IsSynced = 2";
            using var cmd = new SqliteCommand(countQuery, connection);
            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        });
    }

    // Online Syncing
    public static async Task<int> SyncAllOfflineBookingsAsync(string apiUrl = "https://railway-worker-backend.artechnology.pro/api/Booking/create", bool showMessages = true)
    {
        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        string select = "SELECT * FROM Bookings WHERE IsSynced = 0";
        var bookings = new List<Booking1>();

        using (var cmd = new SqliteCommand(select, connection))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                bookings.Add(new Booking1
                {
                    booking_id = reader["booking_id"]?.ToString(),
                    worker_id = reader["worker_id"]?.ToString(),
                    guest_name = reader["guest_name"]?.ToString(),
                    phone_number = reader["phone_number"]?.ToString(),
                    number_of_persons = Convert.ToInt32(reader["number_of_persons"]),
                    booking_type = reader["booking_type"]?.ToString(),
                    total_hours = Convert.ToInt32(reader["total_hours"]),
                    booking_date = DateTime.Parse(reader["booking_date"].ToString()),
                    in_time = TimeSpan.Parse(reader["in_time"].ToString()),
                    out_time = TimeSpan.TryParse(reader["out_time"]?.ToString(), out var outT) ? outT : TimeSpan.Zero,
                    proof_type = reader["proof_type"]?.ToString(),
                    proof_id = reader["proof_id"]?.ToString(),
                    price_per_person = Convert.ToDecimal(reader["price_per_person"]),
                    total_amount = Convert.ToDecimal(reader["total_amount"]),
                    paid_amount = Convert.ToDecimal(reader["paid_amount"]),
                    balance_amount = Convert.ToDecimal(reader["balance_amount"]),
                    payment_method = reader["payment_method"]?.ToString(),
                    status = reader["status"]?.ToString(),
                });
            }
        }

        if (bookings.Count == 0)
        {
            return 0;
        }

        try
        {
            int batchSize = 50;
            int totalBookings = bookings.Count;
            int totalBatches = (int)Math.Ceiling(totalBookings / (double)batchSize);

            using var client = new HttpClient();

            for (int i = 0; i < totalBatches; i++)
            {
                var batch = bookings.Skip(i * batchSize).Take(batchSize).ToList();
                var json = JsonConvert.SerializeObject(batch);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response;

                try
                {
                    response = await client.PostAsync(apiUrl, content);
                }
                catch (HttpRequestException ex)
{
    string errorDetails = $"❌ Internet error while uploading batch {i + 1}/{totalBatches}.\n\n" +
                          $"Error Message: {ex.Message}";

    if (ex.InnerException != null)
        errorDetails += $"\nInner Exception: {ex.InnerException.Message}";

    errorDetails += "\n\nSync paused — please reconnect to internet and try again.";

    MessageBox.Show(errorDetails, "Network Error", MessageBoxButton.OK, MessageBoxImage.Warning);
    break;
}
                catch (TaskCanceledException)
                {
                    MessageBox.Show($"Request timed out while uploading batch {i + 1}/{totalBatches}. " +
                        "Sync paused — please check your connection.",
                        "Timeout", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
                }

                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Mark this batch as synced
                    string ids = string.Join(",", batch.Select(b => $"'{b.booking_id}'"));
                    string update = $"UPDATE Bookings SET IsSynced = 1 WHERE booking_id IN ({ids})";

                    using var cmd = new SqliteCommand(update, connection);
                    cmd.ExecuteNonQuery();

                    Logger.Log($"Batch {i + 1}/{totalBatches} uploaded successfully ({batch.Count} bookings)");
                }
                else
                {
                    Logger.Log($"Server rejected batch {i + 1}/{totalBatches} - Status: {response.StatusCode}");
                    if (showMessages)
                    {
                        // MessageBox.Show($"❌ Server rejected batch {i + 1}/{totalBatches}\n\nStatus: {response.StatusCode}\nResponse: {responseBody}",
                        //     "Sync Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    break; // stop syncing further
                }

                if (i < totalBatches - 1)
                {
                    await Task.Delay(5000); // wait before next batch
                }
            }

            Logger.Log($"Synced offline bookings process completed");
            return totalBookings;
        }
        catch (Exception ex)
        {
            if (showMessages)
            {
                MessageBox.Show($"Unexpected error during sync",
                    "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Logger.Log($"Error syncing offline bookings: {ex.Message}");
            return 0;
        }
    }

    // Sync Updated Bookings (IsSynced = 2) - Only completion/payment updates
    public static async Task<int> SyncUpdatedBookingsAsync(string apiUrl = "https://railway-worker-backend.artechnology.pro/api/Booking/checkout", bool showMessages = true)
    {
        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        // Select only the fields needed for update sync
        string select = "SELECT booking_id, out_time, status, payment_method FROM Bookings WHERE IsSynced = 2";
        var updatedBookings = new List<dynamic>();

        using (var cmd = new SqliteCommand(select, connection))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                updatedBookings.Add(new
                {
                    booking_id = reader["booking_id"]?.ToString(),
                    out_time = reader["out_time"]?.ToString(),
                    status = reader["status"]?.ToString(),
                    payment_method = reader["payment_method"]?.ToString()
                });
            }
        }

        if (updatedBookings.Count == 0)
        {
            return 0;
        }

        try
        {
            int totalBookings = updatedBookings.Count;
            int successCount = 0;
            int failCount = 0;

            using var client = new HttpClient();

            // Process each booking individually
            for (int i = 0; i < updatedBookings.Count; i++)
            {
                var booking = updatedBookings[i];
                string bookingId = booking.booking_id;
                string outTimeStr = booking.out_time ?? "";
                string status = booking.status ?? "Completed";
                string paymentMethod = booking.payment_method ?? "cash";
                
                // Parse out_time string to TimeSpan
                TimeSpan outTime = TimeSpan.TryParse(outTimeStr, out var parsedTime) ? parsedTime : DateTime.Now.TimeOfDay;
                
                // Create checkout request matching backend CheckoutRequest model
                var checkoutRequest = new
                {
                    booking_id = bookingId,
                    out_time = outTime.ToString(@"hh\:mm\:ss"),  // Format as HH:mm:ss string
                    status = status,
                    payment_method = paymentMethod
                };
                
                var json = JsonConvert.SerializeObject(checkoutRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpResponseMessage response;

                try
                {
                    response = await client.PutAsync(apiUrl, content);  // Use PUT as backend expects
                }
                catch (HttpRequestException ex)
                {
                    Logger.Log($"Network error syncing booking {bookingId}: {ex.Message}");
                    failCount++;
                    continue; // Continue with next booking
                }
                catch (TaskCanceledException)
                {
                    Logger.Log($"Timeout syncing booking {bookingId}");
                    failCount++;
                    continue; // Continue with next booking
                }

                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Mark this booking as update synced (IsSynced = 3)
                    string update = $"UPDATE Bookings SET IsSynced = 3 WHERE booking_id = '{bookingId}'";

                    using var cmd = new SqliteCommand(update, connection);
                    cmd.ExecuteNonQuery();

                    successCount++;
                    Logger.Log($"Successfully synced booking {bookingId} ({i + 1}/{totalBookings})");
                }
                else
                {
                    Logger.Log($"Server rejected booking {bookingId} - Status: {response.StatusCode}, Response: {responseBody}");
                    failCount++;
                }

                // Small delay between requests
                if (i < updatedBookings.Count - 1)
                {
                    await Task.Delay(500); // 0.5 second delay
                }
            }

            // Log final summary
            Logger.Log($"Update sync completed - Success: {successCount}, Failed: {failCount}");
            return successCount;
        }
        catch (Exception ex)
        {
            if (showMessages)
            {
                MessageBox.Show($"Unexpected error during update sync",
                    "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Logger.Log($"Error syncing updated bookings: {ex.Message}");
            return 0;
        }
    }

    // Sync a single booking update immediately (for real-time online sync)
    private static async Task<bool> SyncSingleBookingUpdateAsync(string bookingId, TimeSpan outTime, string status, string paymentMethod)
    {
        string apiUrl = "https://railway-worker-backend.artechnology.pro/api/Booking/checkout";
        
        try
        {
            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            // Create checkout request
            var checkoutRequest = new
            {
                booking_id = bookingId,
                out_time = outTime.ToString(@"hh\:mm\:ss"),
                status = status,
                payment_method = paymentMethod
            };

            var json = JsonConvert.SerializeObject(checkoutRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await client.PutAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                // Mark as fully synced (IsSynced = 3)
                string update = "UPDATE Bookings SET IsSynced = 3 WHERE booking_id = @id";
                using var cmd = new SqliteCommand(update, connection);
                cmd.Parameters.AddWithValue("@id", bookingId);
                cmd.ExecuteNonQuery();

                Logger.Log($"✅ Booking {bookingId} update synced successfully to cloud");
                return true;
            }
            else
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                Logger.Log($"❌ Failed to sync booking {bookingId} update: {response.StatusCode} - {responseBody}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"❌ Error syncing booking {bookingId} update: {ex.Message}");
            return false;
        }
    }

    public static List<Booking1> GetBasicBookings()
    {
        var bookings = new List<Booking1>();

        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        string query = @"
        SELECT booking_id, guest_name, phone_number, booking_type, in_time, out_time, status, created_at,
               total_amount, paid_amount, balance_amount, worker_id, number_of_persons, total_hours, price_per_person
        FROM Bookings
        ORDER BY created_at DESC;";

        using var cmd = new SqliteCommand(query, connection);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            bookings.Add(new Booking1
            {
                booking_id = reader["booking_id"]?.ToString(),
                worker_id = reader["worker_id"]?.ToString(),
                guest_name = reader["guest_name"]?.ToString(),
                phone_number = reader["phone_number"]?.ToString(),
                booking_type = reader["booking_type"]?.ToString(),
                number_of_persons = Convert.ToInt32(reader["number_of_persons"] ?? 0),
                total_hours = Convert.ToInt32(reader["total_hours"] ?? 0),
                price_per_person = Convert.ToDecimal(reader["price_per_person"] ?? 0),
                total_amount = Convert.ToDecimal(reader["total_amount"] ?? 0),
                paid_amount = Convert.ToDecimal(reader["paid_amount"] ?? 0),
                balance_amount = Convert.ToDecimal(reader["balance_amount"] ?? 0),
                in_time = TimeSpan.TryParse(reader["in_time"]?.ToString(), out var inT) ? inT : TimeSpan.Zero,
                out_time = TimeSpan.TryParse(reader["out_time"]?.ToString(), out var outT) ? outT : TimeSpan.Zero,
                status = reader["status"]?.ToString(),
                created_at = reader["created_at"] != DBNull.Value && DateTime.TryParse(reader["created_at"]?.ToString(), out var createdAt) 
                    ? createdAt 
                    : (DateTime?)null
            });
        }

        return bookings;
    }

    public static Booking1? GetBookingById(string bookingId)
    {
        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        string query = @"
        SELECT * FROM Bookings WHERE booking_id = @id";

        using var cmd = new SqliteCommand(query, connection);
        cmd.Parameters.AddWithValue("@id", bookingId);
        
        using var reader = cmd.ExecuteReader();
        
        if (reader.Read())
        {
            return new Booking1
            {
                booking_id = reader["booking_id"]?.ToString(),
                worker_id = reader["worker_id"]?.ToString(),
                guest_name = reader["guest_name"]?.ToString(),
                phone_number = reader["phone_number"]?.ToString(),
                number_of_persons = Convert.ToInt32(reader["number_of_persons"] ?? 0),
                booking_type = reader["booking_type"]?.ToString(),
                total_hours = Convert.ToInt32(reader["total_hours"] ?? 0),
                booking_date = DateTime.TryParse(reader["booking_date"]?.ToString(), out var date) ? date : DateTime.Now,
                in_time = TimeSpan.TryParse(reader["in_time"]?.ToString(), out var inT) ? inT : TimeSpan.Zero,
                out_time = TimeSpan.TryParse(reader["out_time"]?.ToString(), out var outT) ? outT : null,
                proof_type = reader["proof_type"]?.ToString(),
                proof_id = reader["proof_id"]?.ToString(),
                price_per_person = Convert.ToDecimal(reader["price_per_person"] ?? 0),
                total_amount = Convert.ToDecimal(reader["total_amount"] ?? 0),
                paid_amount = Convert.ToDecimal(reader["paid_amount"] ?? 0),
                balance_amount = Convert.ToDecimal(reader["balance_amount"] ?? 0),
                payment_method = reader["payment_method"]?.ToString(),
                status = reader["status"]?.ToString(),
                IsSynced = Convert.ToInt32(reader["IsSynced"] ?? 0)
            };
        }

        return null;
    }

    // Method to fetch and display all booking data from local database for debugging
    public static void ShowAllBookingsData()
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            string query = @"
            SELECT booking_id, guest_name, phone_number, booking_type, status, 
                   total_amount, paid_amount, balance_amount, payment_method,
                   in_time, out_time, total_hours, created_at, IsSynced
            FROM Bookings 
            ORDER BY created_at DESC";

            using var cmd = new SqliteCommand(query, connection);
            using var reader = cmd.ExecuteReader();

            var bookingData = "📋 LOCAL DATABASE BOOKINGS:\n" +
                            "================================================\n\n";

            int count = 0;
            while (reader.Read())
            {
                count++;
                bookingData += $"🔹 Booking #{count}\n";
                bookingData += $"   ID: {reader["booking_id"]}\n";
                bookingData += $"   Name: {reader["guest_name"]}\n";
                bookingData += $"   Phone: {reader["phone_number"]}\n";
                bookingData += $"   Type: {reader["booking_type"]}\n";
                bookingData += $"   Status: {reader["status"]}\n";
                bookingData += $"   Total Amount: ₹{reader["total_amount"]}\n";
                bookingData += $"   Paid Amount: ₹{reader["paid_amount"]}\n";
                bookingData += $"   Balance: ₹{reader["balance_amount"]}\n";
                bookingData += $"   Payment Method: {reader["payment_method"]}\n";
                bookingData += $"   In Time: {reader["in_time"]}\n";
                bookingData += $"   Out Time: {reader["out_time"]}\n";
                bookingData += $"   Total Hours: {reader["total_hours"]}\n";
                bookingData += $"   Created: {reader["created_at"]}\n";
                bookingData += $"   Synced: {(Convert.ToInt32(reader["IsSynced"]) == 1 ? "Yes" : "No")}\n";
                bookingData += "\n" + new string('-', 50) + "\n\n";
            }

            if (count == 0)
            {
                bookingData += "❌ No bookings found in local database.\n";
            }
            else
            {
                bookingData += $"📊 Total Bookings: {count}\n";
                bookingData += $"💾 Database Path: {DbPath}";
            }

            // MessageBox.Show(bookingData, "Local Database Data", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            // MessageBox.Show($"❌ Error reading database:\n{ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public static async Task SyncSingleBookingAsync(string bookingId)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            string select = "SELECT * FROM Bookings WHERE booking_id = @id";
            Booking1 booking = null;

            using (var cmd = new SqliteCommand(select, connection))
            {
                cmd.Parameters.AddWithValue("@id", bookingId);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        booking = new Booking1
                        {
                            booking_id = reader["booking_id"]?.ToString(),
                            worker_id = reader["worker_id"]?.ToString(),
                            guest_name = reader["guest_name"]?.ToString(),
                            phone_number = reader["phone_number"]?.ToString(),
                            number_of_persons = Convert.ToInt32(reader["number_of_persons"]),
                            booking_type = reader["booking_type"]?.ToString(),
                            total_hours = Convert.ToInt32(reader["total_hours"]),
                            booking_date = DateTime.Parse(reader["booking_date"].ToString()),
                            in_time = TimeSpan.Parse(reader["in_time"].ToString()),
                            out_time = TimeSpan.TryParse(reader["out_time"]?.ToString(), out var outT) ? outT : TimeSpan.Zero,
                            proof_type = reader["proof_type"]?.ToString(),
                            proof_id = reader["proof_id"]?.ToString(),
                            price_per_person = Convert.ToDecimal(reader["price_per_person"]),
                            total_amount = Convert.ToDecimal(reader["total_amount"]),
                            paid_amount = Convert.ToDecimal(reader["paid_amount"]),
                            balance_amount = Convert.ToDecimal(reader["balance_amount"]),
                            payment_method = reader["payment_method"]?.ToString(),
                            status = reader["status"]?.ToString(),
                        };
                    }
                }
            }

            if (booking == null)
            {
                MessageBox.Show($"No completed booking found for ID: {bookingId}", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 🔹 API endpoint
            string apiUrl = "https://railway-worker-backend.artechnology.pro/api/Booking/online-book";

            // 🔹 Convert booking object to JSON
            string json = JsonConvert.SerializeObject(booking);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            var response = await client.PostAsync(apiUrl, content);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Mark as synced in local DB
                string update = "UPDATE Bookings SET IsSynced = 1 WHERE booking_id = @id";
                using var updateCmd = new SqliteCommand(update, connection);
                updateCmd.Parameters.AddWithValue("@id", bookingId);
                updateCmd.ExecuteNonQuery();

                MessageBox.Show("✅ Booking synced successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                
                // MessageBox.Show($"❌ Server rejected booking.\nStatus: {response.StatusCode}\nResponse: {responseBody}",
                //     "Sync Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            // MessageBox.Show($"❌ Error during sync:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    

    // Method to update booking with payment details and completion info
    public static async Task<string> CompleteBookingWithPaymentAsync(
    string bookingId,
    decimal paidAmount,
    decimal totalAmount,
    decimal extraCharges,
    string paymentMethod,
    TimeSpan outTime)
{
    try
    {
        using var connection = new SqliteConnection($"Data Source={DbPath}");
        await connection.OpenAsync();

        // 🔹 Step 1: Check if booking exists
        string select = "SELECT status, total_amount, IsSynced FROM Bookings WHERE booking_id = @id";
        using (var checkCmd = new SqliteCommand(select, connection))
        {
            checkCmd.Parameters.AddWithValue("@id", bookingId);
            using var reader = await checkCmd.ExecuteReaderAsync();

            if (!reader.Read())
            {
                return $"❌ No booking found with ID: {bookingId}";
            }

            string currentStatus = reader["status"]?.ToString()?.ToLower() ?? "";
            int isSynced = Convert.ToInt32(reader["IsSynced"]);

            if (currentStatus == "completed")
            {
                return $"⚠️ Booking ID {bookingId} is already completed.";
            }

            reader.Close();

            // 🔹 Step 2: Build update query conditionally
            decimal balanceAmount = totalAmount - paidAmount;
            string updateQuery;

            if (isSynced == 0)
            {
                // Only update the booking details, don't touch IsSynced
                updateQuery = @"
                    UPDATE Bookings 
                    SET status = 'completed',
                        paid_amount = @paid_amount,
                        total_amount = @total_amount,
                        balance_amount = @balance_amount,
                        payment_method = @payment_method,
                        out_time = @out_time,
                        updated_at = @updated_at
                    WHERE booking_id = @booking_id";
            }
            else if (isSynced == 1)
            {
                // Update booking details and set IsSynced = 2
                updateQuery = @"
                    UPDATE Bookings 
                    SET status = 'completed',
                        paid_amount = @paid_amount,
                        total_amount = @total_amount,
                        balance_amount = @balance_amount,
                        payment_method = @payment_method,
                        out_time = @out_time,
                        updated_at = @updated_at,
                        IsSynced = 2
                    WHERE booking_id = @booking_id";
            }
            else
            {
                return $"⚠️ Invalid IsSynced value ({isSynced}) for booking {bookingId}.";
            }

            using (var updateCmd = new SqliteCommand(updateQuery, connection))
            {
                updateCmd.Parameters.AddWithValue("@booking_id", bookingId);
                updateCmd.Parameters.AddWithValue("@paid_amount", paidAmount);
                updateCmd.Parameters.AddWithValue("@total_amount", totalAmount);
                updateCmd.Parameters.AddWithValue("@balance_amount", balanceAmount);
                updateCmd.Parameters.AddWithValue("@payment_method", paymentMethod);
                updateCmd.Parameters.AddWithValue("@out_time", outTime.ToString());
                updateCmd.Parameters.AddWithValue("@updated_at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    Logger.Log($"Booking {bookingId} completed with payment: {paymentMethod}, Total: ₹{totalAmount}, Balance: ₹{balanceAmount}, IsSynced: {isSynced}");
                    
                    // If online and booking was previously synced, immediately sync the update
                    if (isSynced == 1 && NetworkInterface.GetIsNetworkAvailable())
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await SyncSingleBookingUpdateAsync(bookingId, outTime, "completed", paymentMethod);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log($"Background sync failed for booking {bookingId}: {ex.Message}");
                            }
                        });
                    }
                    
                    return $"✅ Booking completed successfully!\n💰 Total: ₹{totalAmount}\n💳 Payment: {paymentMethod}\n⏰ Out Time: {outTime:hh\\:mm}";
                }
                else
                {
                    return $"⚠️ Could not complete booking for ID: {bookingId}";
                }
            }
        }
    }
    catch (Exception ex)
    {
        Logger.LogError(ex);
        return $"❌ Error completing booking: {ex.Message}";
    }
}

    public static void AfterSavedOffline(Booking1 booking)
    {
        if (booking == null) return;

        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        string insert = @"
            INSERT OR REPLACE INTO Bookings
            (booking_id, worker_id, guest_name, phone_number, number_of_persons, booking_type, total_hours,
            booking_date, in_time, out_time, proof_type, proof_id, price_per_person, total_amount, paid_amount,
            balance_amount, payment_method, created_at, updated_at, status, IsSynced)
            VALUES (@booking_id, @worker_id, @guest_name, @phone_number, @number_of_persons, @booking_type, @total_hours,
            @booking_date, @in_time, @out_time, @proof_type, @proof_id, @price_per_person, @total_amount, 
            @paid_amount, @balance_amount, @payment_method, @created_at, @updated_at, @status, 1);";

        using var cmd = new SqliteCommand(insert, connection);
        cmd.Parameters.AddWithValue("@booking_id", booking.booking_id);
        cmd.Parameters.AddWithValue("@worker_id", booking.worker_id ?? "");
        cmd.Parameters.AddWithValue("@guest_name", booking.guest_name ?? "");
        cmd.Parameters.AddWithValue("@phone_number", booking.phone_number ?? "");
        cmd.Parameters.AddWithValue("@number_of_persons", booking.number_of_persons);
        cmd.Parameters.AddWithValue("@booking_type", booking.booking_type ?? "");
        cmd.Parameters.AddWithValue("@total_hours", booking.total_hours);
        cmd.Parameters.AddWithValue("@booking_date", booking.booking_date.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@in_time", booking.in_time.ToString());
        cmd.Parameters.AddWithValue("@out_time", booking.out_time?.ToString() ?? "");
        cmd.Parameters.AddWithValue("@proof_type", booking.proof_type ?? "");
        cmd.Parameters.AddWithValue("@proof_id", booking.proof_id ?? "");
        cmd.Parameters.AddWithValue("@price_per_person", booking.price_per_person);
        cmd.Parameters.AddWithValue("@total_amount", booking.total_amount);
        cmd.Parameters.AddWithValue("@paid_amount", booking.paid_amount);
        cmd.Parameters.AddWithValue("@balance_amount", booking.balance_amount);
        cmd.Parameters.AddWithValue("@payment_method", booking.payment_method ?? "Cash");
        cmd.Parameters.AddWithValue("@created_at", booking.created_at?.ToString("yyyy-MM-dd HH:mm:ss") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@updated_at", booking.updated_at?.ToString("yyyy-MM-dd HH:mm:ss") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@status",
            string.IsNullOrWhiteSpace(booking.status)
            ? "active"
            : booking.status.ToLower());

        cmd.ExecuteNonQuery();
        Logger.Log($"Synced booking saved: {booking.booking_id}");
    }

    // Method to mark booking as completed
    public static async Task<string> MarkBookingAsCompletedAsync(string bookingId)
{
    try
    {
        using var connection = new SqliteConnection($"Data Source={DbPath}");
        await connection.OpenAsync();

            // 🔹 Step 1: Check if booking exists and get current status + IsSync
            string select = "SELECT status, IsSynced FROM Bookings WHERE booking_id = @id";
        using (var checkCmd = new SqliteCommand(select, connection))
        {
            checkCmd.Parameters.AddWithValue("@id", bookingId);
            using var reader = await checkCmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return $"❌ No booking found with ID: {bookingId}";
            }

            string status = reader["status"].ToString()?.ToLower() ?? "";
            int isSync = Convert.ToInt32(reader["IsSynced"]);

            if (status == "completed")
            {
                return $"⚠️ Booking ID {bookingId} is already marked as completed.";
            }

            // 🔹 Step 2: Build the update query dynamically
            string update;
            if (isSync == 1)
                update = "UPDATE Bookings SET status = 'completed', IsSynced = 2 WHERE booking_id = @id";
            else
                update = "UPDATE Bookings SET status = 'completed' WHERE booking_id = @id";

            using (var updateCmd = new SqliteCommand(update, connection))
            {
                updateCmd.Parameters.AddWithValue("@id", bookingId);
                int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

                if (rowsAffected > 0)
                {
                    Logger.Log($"Booking {bookingId} marked as completed");
                    return $"✅ Booking ID {bookingId} marked as completed successfully!";
                }
                else
                {
                    return $"⚠️ Could not update booking status for ID: {bookingId}.";
                }
            }
        }
    }
    catch (Exception ex)
    {
        Logger.LogError(ex);
        return $"❌ Error while updating booking status: {ex.Message}";
    }
}

    public static async Task<string> CompleteBookingWithOvertimeAsync(
        string bookingId,
        TimeSpan outTime,
        decimal overtimeCharges,
        decimal finalBalance,
        string paymentMethod)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={DbPath}");
            await connection.OpenAsync();

            // Check if booking exists and get current status + IsSynced
            string select = "SELECT status, IsSynced FROM Bookings WHERE booking_id = @id";
            using (var checkCmd = new SqliteCommand(select, connection))
            {
                checkCmd.Parameters.AddWithValue("@id", bookingId);
                using var reader = await checkCmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return $"❌ No booking found with ID: {bookingId}";
                }

                string status = reader["status"].ToString()?.ToLower() ?? "";
                int isSync = Convert.ToInt32(reader["IsSynced"]);

                if (status == "completed")
                {
                    return $"⚠️ Booking ID {bookingId} is already marked as completed.";
                }

                // Update booking with out_time, balance_amount, payment_method, and status
                string update;
                if (isSync == 1)
                {
                    update = @"UPDATE Bookings SET 
                              out_time = @outTime, 
                              balance_amount = @balance, 
                              payment_method = @payment,
                              status = 'completed', 
                              IsSynced = 2 
                              WHERE booking_id = @id";
                }
                else
                {
                    update = @"UPDATE Bookings SET 
                              out_time = @outTime, 
                              balance_amount = @balance, 
                              payment_method = @payment,
                              status = 'completed' 
                              WHERE booking_id = @id";
                }

                using (var updateCmd = new SqliteCommand(update, connection))
                {
                    updateCmd.Parameters.AddWithValue("@id", bookingId);
                    updateCmd.Parameters.AddWithValue("@outTime", outTime.ToString());
                    updateCmd.Parameters.AddWithValue("@balance", finalBalance);
                    updateCmd.Parameters.AddWithValue("@payment", paymentMethod);
                    
                    int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

                    if (rowsAffected > 0)
                    {
                        Logger.Log($"Booking {bookingId} completed with overtime charges: ₹{overtimeCharges}");
                        return $"✅ Booking ID {bookingId} completed successfully!";
                    }
                    else
                    {
                        return $"⚠️ Could not update booking for ID: {bookingId}.";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return $"❌ Error completing booking: {ex.Message}";
        }
    }

    public static async Task<bool> FetchAndSaveWorkerSettingsAsync(string adminId, string apiUrl = "https://railway-worker-backend.artechnology.pro/api/Settings/hall-types")
    {
        try
        {
            using var client = new HttpClient();
            
            // Make API call to get hall types (booking types) settings
            string fullUrl = $"{apiUrl}/{adminId}";
            var response = await client.GetAsync(fullUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync();
                // MessageBox.Show($"Failed to fetch hall types settings.\n\n" +
                //     $"Status Code: {response.StatusCode}\n" +
                //     $"Reason: {response.ReasonPhrase}\n\n" +
                //     $"API URL: {fullUrl}\n\n" +
                //     $"Response: {errorBody}", 
                //     "API Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            string jsonResponse = await response.Content.ReadAsStringAsync();
            var hallTypesResponse = JsonConvert.DeserializeObject<HallTypesResponse>(jsonResponse);

            if (hallTypesResponse == null || hallTypesResponse.Types == null)
            {
                // MessageBox.Show($"Invalid response from server.\n\n" +
                //     $"Response received:\n{jsonResponse}", 
                //     "Deserialization Error", 
                //     MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Save to local database (save types + advance settings)
            SaveSettings(adminId, hallTypesResponse);

            string typesInfo = string.Join("\n", hallTypesResponse.Types.Select(t => $"• {t.Type}: ₹{t.Amount}"));
            string hallNameDisplay = !string.IsNullOrEmpty(hallTypesResponse.HallName) 
                ? hallTypesResponse.HallName 
                : "Not Available";
            
            // MessageBox.Show($"✅ Settings Loaded Successfully!\n\n" +
            //     $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
            //     $"Hall Name: {hallNameDisplay}\n" +
            //     $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n\n" +
            //     $"Booking Types ({hallTypesResponse.Types.Count}):\n{typesInfo}\n\n" +
            //     $"Advance Payment: {(hallTypesResponse.AdvancePaymentEnabled ? "Enabled" : "Disabled")}\n" +
            //     $"Default Advance: {hallTypesResponse.DefaultAdvancePercentage}%", 
            //     "Settings Loaded", MessageBoxButton.OK, MessageBoxImage.Information);

            return true;
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show($"❌ Network Error\n\n" +
                $"Message: {ex.Message}\n\n" +
                $"Details: {ex.InnerException?.Message ?? "No additional details"}\n\n" +
                $"Please check your internet connection and ensure the API is running.", 
                "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            Logger.LogError(ex);
            return false;
        }
        catch (JsonException ex)
        {
            // MessageBox.Show($"❌ JSON Parse Error\n\n" +
            //     $"Message: {ex.Message}\n\n" +
            //     $"The API response format doesn't match the expected structure.\n" +
            //     $"Check the API documentation or logs for details.", 
            //     "Data Format Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Logger.LogError(ex);
            return false;
        }
        catch (Exception ex)
        {
            // MessageBox.Show($"❌ Unexpected Error\n\n" +
            //     $"Error Type: {ex.GetType().Name}\n" +
            //     $"Message: {ex.Message}\n\n" +
            //     $"Stack Trace:\n{ex.StackTrace}", 
            //     "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Logger.LogError(ex);
            return false;
        }
    }

    private static void SaveSettings(string adminId, HallTypesResponse response)
    {
        using var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();

        // Save hall_name to LocalStorage (not in database)
        if (!string.IsNullOrEmpty(response.HallName))
        {
            LocalStorage.SetItem("hallName", response.HallName, TimeSpan.FromHours(8));
            Logger.Log($"Hall name saved to LocalStorage: {response.HallName}");
        }

        // Clear existing settings
        string deleteQuery = "DELETE FROM Settings";
        using (var deleteCmd = new SqliteCommand(deleteQuery, connection))
        {
            deleteCmd.ExecuteNonQuery();
        }

        // Prepare insert query (store up to 4 types and advance settings)
        string insertQuery = @"
            INSERT INTO Settings (admin_id, type1, type1_amount, type2, type2_amount, 
                                 type3, type3_amount, type4, type4_amount, advance_payment_enabled, default_advance_percentage, last_synced)
            VALUES (@admin_id, @type1, @type1_amount, @type2, @type2_amount, 
                   @type3, @type3_amount, @type4, @type4_amount, @advance_payment_enabled, @default_advance_percentage, @last_synced)";

        using var cmd = new SqliteCommand(insertQuery, connection);
        cmd.Parameters.AddWithValue("@admin_id", adminId);

        // Add up to 4 types
        for (int i = 0; i < 4; i++)
        {
            if (i < response.Types.Count)
            {
                cmd.Parameters.AddWithValue($"@type{i + 1}", response.Types[i].Type ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue($"@type{i + 1}_amount", (object)response.Types[i].Amount);
            }
            else
            {
                cmd.Parameters.AddWithValue($"@type{i + 1}", DBNull.Value);
                cmd.Parameters.AddWithValue($"@type{i + 1}_amount", DBNull.Value);
            }
        }

        cmd.Parameters.AddWithValue("@advance_payment_enabled", response.AdvancePaymentEnabled ? 1 : 0);
    cmd.Parameters.AddWithValue("@default_advance_percentage", (object)response.DefaultAdvancePercentage);
        cmd.Parameters.AddWithValue("@last_synced", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();

        Logger.Log($"Settings saved for admin_id: {adminId} with {response.Types.Count} hall types, advance_enabled={response.AdvancePaymentEnabled}, default_adv_pct={response.DefaultAdvancePercentage}");
    }

    private static void DeleteExpiredSettings()
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            // Calculate the cutoff time (8 hours ago)
            DateTime cutoffTime = DateTime.Now.AddHours(-8);
            string cutoffTimeStr = cutoffTime.ToString("yyyy-MM-dd HH:mm:ss");

            string deleteQuery = "DELETE FROM Settings WHERE last_synced < @cutoff_time";
            using var cmd = new SqliteCommand(deleteQuery, connection);
            cmd.Parameters.AddWithValue("@cutoff_time", cutoffTimeStr);

            int deletedRows = cmd.ExecuteNonQuery();
            if (deletedRows > 0)
            {
                Logger.Log($"Deleted {deletedRows} expired settings (older than 8 hours)");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    public static Settings? GetSettings()
    {
        try
        {
            // First, delete any settings older than 8 hours
            DeleteExpiredSettings();

            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            string query = "SELECT * FROM Settings ORDER BY id DESC LIMIT 1";
            using var cmd = new SqliteCommand(query, connection);
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                var settings = new Settings
                {
                    Id = Convert.ToInt32(reader["id"]),
                    AdminId = reader["admin_id"]?.ToString() ?? "",
                    Type1 = reader["type1"]?.ToString(),
                    Type1Amount = reader["type1_amount"] != DBNull.Value ? Convert.ToDecimal(reader["type1_amount"]) : null,
                    Type2 = reader["type2"]?.ToString(),
                    Type2Amount = reader["type2_amount"] != DBNull.Value ? Convert.ToDecimal(reader["type2_amount"]) : null,
                    Type3 = reader["type3"]?.ToString(),
                    Type3Amount = reader["type3_amount"] != DBNull.Value ? Convert.ToDecimal(reader["type3_amount"]) : null,
                    Type4 = reader["type4"]?.ToString(),
                    Type4Amount = reader["type4_amount"] != DBNull.Value ? Convert.ToDecimal(reader["type4_amount"]) : null,
                    AdvancePaymentEnabled = reader["advance_payment_enabled"] != DBNull.Value && Convert.ToInt32(reader["advance_payment_enabled"]) == 1,
                    DefaultAdvancePercentage = reader["default_advance_percentage"] != DBNull.Value ? Convert.ToDecimal(reader["default_advance_percentage"]) : 0m,
                    LastSynced = DateTime.TryParse(reader["last_synced"]?.ToString(), out var date) 
                        ? date : DateTime.MinValue
                };

                // Double-check if this setting is still valid (within 8 hours)
                if ((DateTime.Now - settings.LastSynced).TotalHours >= 8)
                {
                    Logger.Log("Settings expired, returning null");
                    return null;
                }

                return settings;
            }

            return null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return null;
        }
    }

    public static List<BookingType> GetBookingTypes()
    {
        var types = new List<BookingType>();

        try
        {
            var settings = GetSettings();
            if (settings == null) return types;

            if (!string.IsNullOrEmpty(settings.Type1) && settings.Type1Amount.HasValue)
                types.Add(new BookingType { Type = settings.Type1, Amount = settings.Type1Amount.Value });

            if (!string.IsNullOrEmpty(settings.Type2) && settings.Type2Amount.HasValue)
                types.Add(new BookingType { Type = settings.Type2, Amount = settings.Type2Amount.Value });

            if (!string.IsNullOrEmpty(settings.Type3) && settings.Type3Amount.HasValue)
                types.Add(new BookingType { Type = settings.Type3, Amount = settings.Type3Amount.Value });

            if (!string.IsNullOrEmpty(settings.Type4) && settings.Type4Amount.HasValue)
                types.Add(new BookingType { Type = settings.Type4, Amount = settings.Type4Amount.Value });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }

        return types;
    }

    public static decimal GetBookingTypeAmount(string typeName)
    {
        try
        {
            var types = GetBookingTypes();
            var matchingType = types.FirstOrDefault(t => t.Type.Equals(typeName, StringComparison.OrdinalIgnoreCase));
            return matchingType?.Amount ?? 0;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return 0;
        }
    }

    public static async Task<bool> SyncSingleBookingToServer(Booking1 booking)
    {
        if (booking == null) return false;

        // Check if network is available
        bool isOnline = NetworkInterface.GetIsNetworkAvailable();

        if (!isOnline)
        {
            Logger.Log("No internet connection available for syncing.");
            return false;
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            
            var bookingData = new[] { booking };
            var json = JsonConvert.SerializeObject(bookingData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(CreateBookingApiUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                // Update IsSynced to 1 in local database
                UpdateBookingSyncStatus(booking.booking_id, 1);
                Logger.Log($"Booking {booking.booking_id} synced successfully to server.");
                return true;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // 409 Conflict - booking already exists on server
                // Mark as synced locally
                UpdateBookingSyncStatus(booking.booking_id, 1);
                Logger.Log($"Booking {booking.booking_id} already exists on server. Marked as synced locally.");
                return true;
            }
            else
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                Logger.Log($"Failed to sync booking {booking.booking_id}: {response.StatusCode} - {responseBody}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error syncing booking {booking.booking_id}: {ex.Message}");
            Logger.LogError(ex);
            return false;
        }
    }

    /// <summary>
    /// Updates the IsSynced status of a booking in local database
    /// </summary>
    private static void UpdateBookingSyncStatus(string bookingId, int isSynced)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();

            string updateQuery = @"
                UPDATE Bookings 
                SET IsSynced = @IsSynced, 
                    updated_at = @UpdatedAt 
                WHERE booking_id = @BookingId";

            using var cmd = new SqliteCommand(updateQuery, connection);
            cmd.Parameters.AddWithValue("@IsSynced", isSynced);
            cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@BookingId", bookingId);

            int rowsAffected = cmd.ExecuteNonQuery();
            Logger.Log($"Updated IsSynced status for booking {bookingId}. Rows affected: {rowsAffected}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }
}

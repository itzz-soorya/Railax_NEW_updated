using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using UserModule.Models;

namespace UserModule
{
    public partial class Dashboard : UserControl
    {
        public ObservableCollection<Booking1> Bookings { get; set; } = new ObservableCollection<Booking1>();
        private List<Booking1> allBookings = new List<Booking1>(); // Store all bookings for filtering

        private Dictionary<string, int> bookingTypeCounts = new Dictionary<string, int>();
        private Dictionary<string, TextBlock> typeTextBlocks = new Dictionary<string, TextBlock>();
        private string currentFilter = "All"; // Track current filter state
        
        // Internet status monitoring
        private DispatcherTimer? internetCheckTimer;
        private int consecutiveFailures = 0;

        public Dashboard()
        {
            InitializeComponent();
            DataContext = this;
            BookingDataGrid.ItemsSource = Bookings;

            try
            {
                LoadBookings();
                DateTextBlock.Text = DateTime.Now.ToString("MMMM d, yyyy");
                
                // Get username from LocalStorage instead of hardcoded "User"
                string username = LocalStorage.GetItem("username");
                if (string.IsNullOrEmpty(username))
                {
                    username = "User"; // Fallback if not found
                }
                UpdateGreeting(username);
                
                InitializeBookingTypeCounts();
                UpdateCountsFromBookings();
                
                // Initialize internet status monitoring
                InitializeInternetStatusMonitor();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                MessageBox.Show("An error occurred while loading the dashboard.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadBookings()
        {
            try
            {
                Bookings.Clear();
                allBookings.Clear(); // Clear stored bookings
                var localBookings = OfflineBookingStorage.GetBasicBookings();

                if (localBookings != null && localBookings.Any())
                {
                    // Filter to only show bookings created in the last 2 weeks using created_at
                    DateTime twoWeeksAgo = DateTime.Now.AddDays(-14);
                    
                    allBookings = localBookings
                        .Where(b => b.created_at.HasValue && b.created_at.Value >= twoWeeksAgo)
                        .OrderByDescending(b => b.created_at)
                        .ToList();
                    
                    Logger.Log($"Loaded {allBookings.Count} bookings created in the last 2 weeks (since {twoWeeksAgo:yyyy-MM-dd})");
                    
                    // Apply current filter
                    ApplyFilter();
                }
                else
                {
                    Logger.Log("No bookings found in local database.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                MessageBox.Show("Failed to load bookings from local database. Please try again later", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            Bookings.Clear();
            
            IEnumerable<Booking1> filteredBookings = allBookings;

            if (currentFilter == "Active")
            {
                filteredBookings = allBookings.Where(b => b.status?.ToLower() == "active");
            }
            else if (currentFilter == "Completed")
            {
                filteredBookings = allBookings.Where(b => b.status?.ToLower() == "completed");
            }
            // "All" shows everything, no filter needed

            foreach (var booking in filteredBookings)
            {
                Bookings.Add(booking);
            }

            BookingDataGrid.ItemsSource = Bookings;
        }

        private void FilterAll_Click(object sender, RoutedEventArgs e)
        {
            currentFilter = "All";
            UpdateFilterButtons();
            ApplyFilter();
            Logger.Log("Filter: All bookings");
        }

        private void FilterActive_Click(object sender, RoutedEventArgs e)
        {
            currentFilter = "Active";
            UpdateFilterButtons();
            ApplyFilter();
            Logger.Log("Filter: Active bookings only");
        }

        private void FilterCompleted_Click(object sender, RoutedEventArgs e)
        {
            currentFilter = "Completed";
            UpdateFilterButtons();
            ApplyFilter();
            Logger.Log("Filter: Completed bookings only");
        }

        private void UpdateFilterButtons()
        {
            // Reset all buttons to inactive state
            btnAllFilter.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
            btnAllFilter.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
            
            btnActiveFilter.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
            btnActiveFilter.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
            
            btnCompletedFilter.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0"));
            btnCompletedFilter.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));

            // Set active button
            if (currentFilter == "All")
            {
                btnAllFilter.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#232323"));
                btnAllFilter.Foreground = Brushes.White;
            }
            else if (currentFilter == "Active")
            {
                btnActiveFilter.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                btnActiveFilter.Foreground = Brushes.White;
            }
            else if (currentFilter == "Completed")
            {
                btnCompletedFilter.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
                btnCompletedFilter.Foreground = Brushes.White;
            }
        }

        private void AddBookingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var parentWindow = Window.GetWindow(this);
                if (parentWindow is MainWindow mainWin && mainWin.MainContent.Content is Header header)
                {
                    header.OpenNewBooking();
                    Logger.Log("New booking page opened.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private void BookingDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var dataGrid = sender as DataGrid;
                var selectedItem = dataGrid?.SelectedItem as Booking1;
                
                // Clear selection immediately to prevent visual glitches
                if (dataGrid != null)
                {
                    dataGrid.SelectedItem = null;
                    dataGrid.SelectedIndex = -1;
                }

                // Commented out the message box functionality
                /*
                if (selectedItem != null)
                {
                    // Only allow sync for active bookings
                    if (selectedItem.status?.ToLower() != "active")
                    {
                        MessageBox.Show(
                            "Only active bookings can be synced to the network.",
                            "Cannot Sync",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }

                    // Check if already synced
                    if (selectedItem.IsSynced == 1)
                    {
                        MessageBox.Show(
                            $"Booking Details:\n\n" +
                            $"Booking ID: {selectedItem.booking_id}\n" +
                            $"Name: {selectedItem.guest_name}\n" +
                            $"Phone: {selectedItem.phone_number}\n" +
                            $"Type: {selectedItem.booking_type}\n" +
                            $"In Time: {selectedItem.in_time}\n" +
                            $"Out Time: {selectedItem.out_time}\n" +
                            $"Status: {selectedItem.status}\n\n" +
                            $"✓ This booking is already synced to the network.",
                            "Already Synced",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }

                    // Booking is active and not synced - ask to sync
                    string details =
                        $"Booking ID: {selectedItem.booking_id}\n" +
                        $"Name: {selectedItem.guest_name}\n" +
                        $"Phone: {selectedItem.phone_number}\n" +
                        $"Type: {selectedItem.booking_type}\n" +
                        $"In Time: {selectedItem.in_time}\n" +
                        $"Out Time: {selectedItem.out_time}\n" +
                        $"Status: {selectedItem.status}\n\n" +
                        $"Do you want to add this booking to the network?";

                    // Show MessageBox with Yes/No buttons
                    var result = MessageBox.Show(
                        details, 
                        "Add to Network?", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // User clicked "Yes" - add to network
                        AddBookingToNetwork(selectedItem);
                    }

                    Logger.Log($"Viewed booking details for {selectedItem.booking_id}");
                }
                */
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                // MessageBox.Show("Error displaying booking details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddBookingToNetwork(Booking1 booking)
        {
            try
            {
                // First, check if internet is available
                bool isOnline = System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
                
                if (!isOnline)
                {
                    MessageBox.Show(
                        "No internet connection available.\n\nPlease connect to the internet and try again.",
                        "No Internet",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Log the sync attempt
                Logger.Log($"Attempting to sync booking {booking.booking_id} to server...");

                // Call the API to sync the booking
                bool success = await OfflineBookingStorage.SyncSingleBookingToServer(booking);

                if (success)
                {
                    // MessageBox.Show(
                    //     "Booking successfully added to the network!",
                    //     "Success",
                    //     MessageBoxButton.OK,
                    //     MessageBoxImage.Information);

                    // Reload bookings to reflect updated sync status
                    LoadBookings();
                }
                else
                {
                    // Sync failed - server issue
                    // MessageBox.Show(
                    //     "Server is temporarily unavailable.\n\nPlease try again later.",
                    //     "Server Error",
                    //     MessageBoxButton.OK,
                    //     MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                // MessageBox.Show(
                //     $"An error occurred while syncing:\n\n{ex.Message}",
                //     "Error",
                //     MessageBoxButton.OK,
                //     MessageBoxImage.Error);
            }
        }

        public void UpdateGreeting(string username)
        {
            try
            {
                if (GreetingTextBlock != null)
                    GreetingTextBlock.Text = $"{GetGreeting()}, {username}";
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private string GetGreeting()
        {
            var hour = DateTime.Now.Hour;
            if (hour < 12)
                return "Good morning";
            else if (hour < 17)
                return "Good afternoon";
            else
                return "Good evening";
        }

        public void UpdateCountsFromBookings()
        {
            try
            {
                // Reset all counts
                bookingTypeCounts.Clear();
                foreach (var key in typeTextBlocks.Keys)
                {
                    bookingTypeCounts[key] = 0;
                }

                int activeCount = 0;
                int completedCount = 0;

                // Count bookings by status and type from ALL bookings (not filtered)
                foreach (var booking in allBookings)
                {
                    if (booking.status?.ToLower() == "active")
                    {
                        activeCount++;
                        
                        // Count by type (only for active bookings) - case-insensitive comparison
                        string bookingType = booking.booking_type?.Trim();
                        if (!string.IsNullOrEmpty(bookingType))
                        {
                            // Find matching key in dictionary (case-insensitive)
                            var matchingKey = bookingTypeCounts.Keys
                                .FirstOrDefault(k => k.Equals(bookingType, StringComparison.OrdinalIgnoreCase));
                            
                            if (matchingKey != null)
                            {
                                bookingTypeCounts[matchingKey]++;
                            }
                            else
                            {
                                Logger.Log($"Warning: Booking type '{bookingType}' not found in counts dictionary. Available types: {string.Join(", ", bookingTypeCounts.Keys)}");
                            }
                        }
                    }
                    else if (booking.status?.ToLower() == "completed")
                    {
                        completedCount++;
                    }
                }

                // Update status counts
                ActiveTextBlock.Text = $"Active: {activeCount}";
                CompletedTextBlock.Text = $"Completed: {completedCount}";

                // Update booking type counts
                foreach (var kvp in typeTextBlocks)
                {
                    kvp.Value.Text = $"{kvp.Key}: {bookingTypeCounts[kvp.Key]}";
                }

                Logger.Log($"Counts updated - Active: {activeCount}, Completed: {completedCount}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private void InitializeBookingTypeCounts()
        {
            try
            {
                // Get booking types from database settings
                var bookingTypes = OfflineBookingStorage.GetBookingTypes();
                
                if (bookingTypes == null || bookingTypes.Count == 0)
                {
                    Logger.Log("No booking types found in database settings.");
                    return;
                }

                // Define colors for each type
                var colors = new[]
                {
                    new { Background = "#EAF3FF", Foreground = "#3A72C5" },  // Blue
                    new { Background = "#F5E8FF", Foreground = "#C53ABF" },  // Purple
                    new { Background = "#FFF8E1", Foreground = "#F9A825" },  // Yellow
                    new { Background = "#E8F5E9", Foreground = "#66BB6A" }   // Green
                };

                int colorIndex = 0;

                foreach (var bookingType in bookingTypes)
                {
                    if (string.IsNullOrEmpty(bookingType.Type)) continue;

                    string typeName = bookingType.Type.Trim();
                    bookingTypeCounts[typeName] = 0;

                    // Create UI element for this type
                    var border = new Border
                    {
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[colorIndex % colors.Length].Background)),
                        Padding = new Thickness(6, 4, 6, 4),
                        CornerRadius = new CornerRadius(16),
                        Margin = new Thickness(0, 0, 6, 0)
                    };

                    var stackPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var textBlock = new TextBlock
                    {
                        Text = $"{typeName}: 0",
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[colorIndex % colors.Length].Foreground)),
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold
                    };

                    typeTextBlocks[typeName] = textBlock;
                    stackPanel.Children.Add(textBlock);
                    border.Child = stackPanel;
                    CountsPanel.Children.Add(border);

                    colorIndex++;
                }

                Logger.Log($"Initialized {bookingTypeCounts.Count} booking type counters.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Logger.Log("Scan button clicked - attempting to create SimpleScanControl");
                
                SimpleScanControl scanControl;
                try
                {
                    scanControl = new SimpleScanControl();
                    Logger.Log("SimpleScanControl created successfully");
                }
                catch (Exception createEx)
                {
                    Logger.LogError(createEx);
                    MessageBox.Show(
                        $"Error creating scan control", 
                        "Control Creation Error", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                    return;
                }
                
                // Handle close event
                scanControl.CloseRequested += (s, ev) =>
                {
                    ContentGrid.Children.Clear();
                    ContentGrid.Visibility = Visibility.Collapsed;
                    
                    // Reload bookings to reflect any completed bookings
                    LoadBookings();
                    UpdateCountsFromBookings();
                    
                    Logger.Log("Scan control closed - Dashboard refreshed");
                };
                
                // Handle billing completed event (removed for simple test)
                
                ContentGrid.Children.Clear();
                ContentGrid.Children.Add(scanControl);
                ContentGrid.Visibility = Visibility.Visible;
                Logger.Log("Scan billing control opened and displayed.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                // MessageBox.Show(
                //     $"Error opening scan control:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                //     "Error", 
                //     MessageBoxButton.OK, 
                //     MessageBoxImage.Error);
            }
        }

        // Method to view all database contents - for debugging
        public void ViewDatabaseContents()
        {
            try
            {
                OfflineBookingStorage.ShowAllBookingsData();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                // MessageBox.Show($"Error viewing database: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void RefreshBookings()
        {
            try
            {
                BookingDataGrid.ItemsSource = null;
                BookingDataGrid.ItemsSource = Bookings;
                Logger.Log("Bookings refreshed."); // ✅ Added Logger
            }
            catch (Exception ex)
            {
                Logger.LogError(ex); // ✅ Added Logger
            }
        }

        /// <summary>
        /// Reloads all bookings from database and updates the display
        /// </summary>
        public void ReloadData()
        {
            try
            {
                LoadBookings();
                UpdateCountsFromBookings();
                Logger.Log("Dashboard data reloaded from database.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                // MessageBox.Show("Error reloading dashboard data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                string query = SearchTextBox.Text.Trim().ToLower();

                if (string.IsNullOrEmpty(query))
                {
                    BookingDataGrid.ItemsSource = Bookings;
                    return;
                }

                var filtered = Bookings.Where(b =>
                    (!string.IsNullOrEmpty(b.guest_name) && b.guest_name.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(b.phone_number) && b.phone_number.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(b.booking_id) && b.booking_id.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(b.booking_type) && b.booking_type.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(b.status) && b.status.ToLower().Contains(query))
                ).ToList();

                BookingDataGrid.ItemsSource = filtered;
                Logger.Log($"Search performed: {query}"); // ✅ Added Logger
            }
            catch (Exception ex)
            {
                Logger.LogError(ex); // ✅ Added Logger
                // MessageBox.Show($"Search error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Initialize the internet status monitoring timer
        /// </summary>
        private void InitializeInternetStatusMonitor()
        {
            // Check immediately on load
            _ = CheckInternetStatusAsync();

            // Set up timer to check every 5 seconds
            internetCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            internetCheckTimer.Tick += async (s, e) => await CheckInternetStatusAsync();
            internetCheckTimer.Start();
        }

        /// <summary>
        /// Check internet connection status and update the indicator
        /// </summary>
        private async Task CheckInternetStatusAsync()
        {
            try
            {
                bool isConnected = NetworkInterface.GetIsNetworkAvailable();
                
                if (!isConnected)
                {
                    // No network interface available - Red (No Internet)
                    consecutiveFailures = 3;
                    UpdateInternetStatus(InternetStatus.NoConnection);
                    return;
                }

                // Try to ping a reliable server to check actual internet connectivity
                bool hasInternet = await PingServerAsync("8.8.8.8", 3000); // Google DNS
                
                if (hasInternet)
                {
                    consecutiveFailures = 0;
                    UpdateInternetStatus(InternetStatus.Good);
                }
                else
                {
                    consecutiveFailures++;
                    
                    if (consecutiveFailures >= 3)
                    {
                        // Multiple failures - Red (No Internet)
                        UpdateInternetStatus(InternetStatus.NoConnection);
                    }
                    else if (consecutiveFailures >= 1)
                    {
                        // Some failures - Yellow (Unstable)
                        UpdateInternetStatus(InternetStatus.Unstable);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                consecutiveFailures++;
                
                if (consecutiveFailures >= 2)
                {
                    UpdateInternetStatus(InternetStatus.NoConnection);
                }
                else
                {
                    UpdateInternetStatus(InternetStatus.Unstable);
                }
            }
        }

        /// <summary>
        /// Ping a server to check internet connectivity
        /// </summary>
        private async Task<bool> PingServerAsync(string host, int timeout)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(host, timeout);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Update the UI with the current internet status
        /// </summary>
        private void UpdateInternetStatus(InternetStatus status)
        {
            if (InternetStatusDot == null || InternetStatusText == null)
                return;

            switch (status)
            {
                case InternetStatus.Good:
                    InternetStatusDot.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                    InternetStatusText.Text = "Online";
                    InternetStatusDot.ToolTip = "Internet connection is stable";
                    break;

                case InternetStatus.Unstable:
                    InternetStatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Yellow/Amber
                    InternetStatusText.Text = "Unstable";
                    InternetStatusDot.ToolTip = "Internet connection is weak or unstable";
                    break;

                case InternetStatus.NoConnection:
                    InternetStatusDot.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                    InternetStatusText.Text = "Offline";
                    InternetStatusDot.ToolTip = "No internet connection";
                    break;
            }
        }

        /// <summary>
        /// Internet connection status enum
        /// </summary>
        private enum InternetStatus
        {
            Good,       // Green - Stable connection
            Unstable,   // Yellow - Weak/intermittent connection
            NoConnection // Red - No internet
        }
    }
}

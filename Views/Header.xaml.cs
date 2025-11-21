using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;               // For Brush, Brushes, Colors, BrushConverter
using System.Windows.Media.Effects;       // For DropShadowEffect
using System.Windows.Threading;          // For DispatcherTimer


namespace UserModule
{
    /// <summary>
    /// Interaction logic for Header.xaml
    /// </summary>
    public partial class Header : UserControl
    {
        private Button? _selectedButton;

        public Header()
        {
            InitializeComponent();
            LoadContent(new Dashboard());

            // Initially select Dashboard button
            _selectedButton = DashboardButton;
            SetSelectedButton(_selectedButton);

            


        }
        public void SetLoggedInUser(string username)
        {
            // Capitalize first letter of username
            string capitalizedUsername = !string.IsNullOrEmpty(username) 
                ? char.ToUpper(username[0]) + username.Substring(1).ToLower() 
                : username;

            // Get time-based greeting
            string greeting = GetTimeBasedGreeting();

            // Update username text
            UserNameTextBlock.Text = capitalizedUsername;

            // Create popup container
            var popupBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 10, 20, 0),
                Opacity = 0,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 15,
                    ShadowDepth = 3,
                    Opacity = 0.25
                }
            };

            // Text inside popup
            var popupText = new TextBlock
            {
                Text = $"{greeting}, {capitalizedUsername}! Welcome Back. Have a good day!!",
                Foreground = (Brush)new BrushConverter().ConvertFromString("#28C76F"),
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            };

            popupBorder.Child = popupText;

            // Add to MainContentGrid
            MainContentGrid.Children.Add(popupBorder);

            // Animate slide-in from top-right
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(400)));
            popupBorder.BeginAnimation(OpacityProperty, anim);

            // Auto remove after 3 seconds
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) =>
            {
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(300)));
                fadeOut.Completed += (s2, e2) => MainContentGrid.Children.Remove(popupBorder);
                popupBorder.BeginAnimation(OpacityProperty, fadeOut);
                timer.Stop();
            };
            timer.Start();
        }
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show confirmation dialog
                var result = MessageBox.Show(
                    "Are you sure you want to logout?", 
                    "Confirm Logout", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Clear stored credentials from LocalStorage
                    LocalStorage.RemoveItem("username");
                    LocalStorage.RemoveItem("password");
                    LocalStorage.RemoveItem("workerId");
                    LocalStorage.RemoveItem("rememberMe");
                    
                    // Log the logout action
                    Logger.Log($"User {UserNameTextBlock.Text} logged out successfully");

                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        // Create a fresh Login UserControl
                        var loginControl = new Login();

                        // Handle LoginSuccess to reload Header + Dashboard
                        loginControl.LoginSuccess += username =>
                        {
                            // Create new Header inside the lambda
                            var header = new Header();

                            // Set the logged-in user
                            header.SetLoggedInUser(username);

                            // Load Dashboard inside Header's MainContentHost
                            header.MainContentHost.Content = new Dashboard();

                            // Set MainContent of MainWindow to the new Header
                            mainWindow.MainContent.Content = header;
                        };

                        // Replace current MainContent (Header + Dashboard) with Login
                        mainWindow.MainContent.Content = loginControl;
                        
                        // Show logout success message
                        MessageBox.Show(
                            "You have been logged out successfully!", 
                            "Logout Successful", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                // MessageBox.Show(
                //     "An error occurred during logout. Please try again.", 
                //     "Logout Error", 
                //     MessageBoxButton.OK, 
                //     MessageBoxImage.Error);
            }
        }




        // Ex
        public void LoadContent(UserControl control)
        {
            MainContentHost.Content = control;
        }

        private void SetSelectedButton(Button button)
        {
            // Prevent unnecessary restyle
            if (_selectedButton == button)
                return;

            // Reset previous selected button style (except Submit)
            if (_selectedButton != null && _selectedButton != SubmitBookingButton)
                _selectedButton.Style = (Style)FindResource("HeaderButtonStyle");

            // Update current button
            _selectedButton = button;

            // Always keep Submit button in selected style
            if (button == SubmitBookingButton)
            {
                SubmitBookingButton.Style = (Style)FindResource("HeaderButtonSelectedStyle");
            }
            else
            {
                _selectedButton.Style = (Style)FindResource("HeaderButtonSelectedStyle");
            }
        }

        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedButton(DashboardButton);
            SetSubmitButtonVisibility(false); // Hide Submit button
            LoadContent(new Dashboard());
        }

        private void NewBooking_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedButton(NewBookingButton);
            SetSubmitButtonVisibility(false); // Hide Submit button
            var dashboard = MainContentGrid.Children.OfType<Dashboard>().FirstOrDefault() ?? new Dashboard();
            LoadContent(new NewBooking(dashboard));
        }

        public void OpenNewBooking()
        {
            SetSelectedButton(NewBookingButton);
            SetSubmitButtonVisibility(false); // Hide Submit button
            var dashboard = MainContentGrid.Children.OfType<Dashboard>().FirstOrDefault() ?? new Dashboard();
            LoadContent(new NewBooking(dashboard));
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            // Submit button should always appear selected
            SubmitBookingButton.Style = (Style)FindResource("HeaderButtonSelectedStyle");
            SetSubmitButtonVisibility(true); // Show Submit button
            SetSelectedButton(SubmitBookingButton);
            LoadContent(new Submit());
        }

        public void UpdateUsername(string username)
        {
            UserNameTextBlock.Text = username;
        }

        // Controls Submit button visibility in Header
        public void SetSubmitButtonVisibility(bool isVisible)
        {
            SubmitBookingButton.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Gets a greeting message based on the current time
        /// </summary>
        private string GetTimeBasedGreeting()
        {
            int hour = DateTime.Now.Hour;

            if (hour >= 5 && hour < 12)
                return "Good Morning";
            else if (hour >= 12 && hour < 17)
                return "Good Afternoon";
            else if (hour >= 17 && hour < 21)
                return "Good Evening";
            else
                return "Good Night";
        }
    }
}

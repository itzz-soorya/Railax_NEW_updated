using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UserModule.Models;

namespace UserModule
{
    public partial class SimpleScanControl : UserControl
    {
        public event EventHandler? CloseRequested;
        private Booking1? currentBooking;

        public SimpleScanControl()
        {
            try
            {
                InitializeComponent();
                
                // Safe focus setting
                Loaded += (s, e) => 
                {
                    try 
                    { 
                        txtTest?.Focus(); 
                    } 
                    catch (Exception ex) 
                    { 
                        Logger.LogError(ex); 
                    }
                };

                // Set default out time to current time
                txtOutTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                throw; // Re-throw to let caller handle
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void txtTest_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ProcessScan();
            }
        }

        private void ProcessScan()
        {
            string bookingId = txtTest.Text.Trim();
            
            if (string.IsNullOrEmpty(bookingId))
            {
                MessageBox.Show("Please enter or scan a booking ID", "Missing Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Check if booking exists and get its status
                var booking = OfflineBookingStorage.GetBookingById(bookingId);
                
                if (booking == null)
                {
                    MessageBox.Show($"❌ Booking ID '{bookingId}' not found in database", "Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtTest.SelectAll();
                    return;
                }

                // Check current status
                if (booking.status?.ToLower() == "completed")
                {
                    MessageBox.Show($"⚠️ Booking ID '{bookingId}' is already completed", "Already Completed", MessageBoxButton.OK, MessageBoxImage.Information);
                    txtTest.SelectAll();
                    return;
                }

                // Store current booking and show payment section
                currentBooking = booking;
                ShowPaymentSection(booking);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                // MessageBox.Show($"Error processing booking: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowPaymentSection(Booking1 booking)
        {
            try
            {
                // Hide scan section
                ScanSection.Visibility = Visibility.Collapsed;
                
                // Populate customer info
                lblCustomerName.Text = booking.guest_name ?? "N/A";
                lblCustomerPhone.Text = $"Phone: {booking.phone_number ?? "N/A"}";
                lblSeatType.Text = $"Booking ID: {booking.booking_id} | Type: {booking.booking_type}";

                // Calculate extra charges based on time (if stayed longer than expected)
                decimal extraCharges = 0;
                
                // Calculate duration from in_time to now
                DateTime bookingDate = booking.booking_date;
                DateTime inDateTime = bookingDate.Date + booking.in_time;
                DateTime currentTime = DateTime.Now;
                TimeSpan duration = currentTime - inDateTime;
                
                // Example: Charge ₹50 per hour after 6 hours
                if (duration.TotalHours > 6)
                {
                    double extraHours = Math.Ceiling(duration.TotalHours - 6);
                    extraCharges = (decimal)(extraHours * 50);
                }

                // Calculate total amount
                decimal originalAmount = booking.total_amount;
                decimal totalAmount = originalAmount + extraCharges;
                decimal paidAmount = booking.paid_amount;
                decimal balanceAmount = totalAmount - paidAmount;

                // Display amounts
                lblOriginalAmount.Text = $"₹{originalAmount:F2}";
                lblExtraCharges.Text = $"₹{extraCharges:F2}";
                lblTotalAmount.Text = $"₹{totalAmount:F2}";
                lblPaidAmount.Text = $"₹{paidAmount:F2}";
                txtBalanceAmount.Text = balanceAmount.ToString("F2");

                // Set current out time dynamically
                txtOutTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                // Show payment section
                PaymentSection.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                // MessageBox.Show($"Error displaying payment section: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void cmbPaymentMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidatePaymentForm();
        }

        private void ValidatePaymentForm()
        {
            try
            {
                // Defensive null checks in case control not yet initialized
                if (cmbPaymentMethod == null || btnCompletePayment == null || errPaymentMethod == null)
                    return;

                // Only check if payment method is selected
                bool isValid = cmbPaymentMethod.SelectedIndex > 0 && cmbPaymentMethod.Items.Count > 0;

                // Enable/disable complete button
                btnCompletePayment.IsEnabled = isValid;
                
                // Show/hide error message
                if (cmbPaymentMethod.SelectedIndex == 0 && cmbPaymentMethod.IsFocused)
                {
                    errPaymentMethod.Visibility = Visibility.Visible;
                }
                else
                {
                    errPaymentMethod.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        private async void btnCompletePayment_Click(object sender, RoutedEventArgs e)
        {


            try
            {
                if (decimal.TryParse(txtBalanceAmount.Text, out decimal balanceAmount) && balanceAmount <= 0)
                {
                    MessageBox.Show("No balance to pay! Booking already settled.", "Info",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (currentBooking == null)
                {
                    MessageBox.Show("No booking selected!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Validate inputs
                if (cmbPaymentMethod == null || cmbPaymentMethod.SelectedIndex <= 0)
                {
                    MessageBox.Show("Please select a payment method!", "Validation Error", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get out time from the read-only textbox (already set to current time)
                string outTimeStr = txtOutTime.Text.Trim();
                DateTime outDateTime;
                if (string.IsNullOrEmpty(outTimeStr))
                {
                    outDateTime = DateTime.Now;
                }
                else
                {
                    if (!DateTime.TryParse(outTimeStr, out outDateTime))
                    {
                        // MessageBox.Show("Invalid out time format. Using current time.", "Warning", 
                        //     MessageBoxButton.OK, MessageBoxImage.Warning);
                        outDateTime = DateTime.Now;
                    }
                }
                
                // Convert to TimeSpan (time of day only)
                TimeSpan outTime = outDateTime.TimeOfDay;

                string paymentMethod = ((ComboBoxItem)cmbPaymentMethod.SelectedItem).Content.ToString() ?? "Cash";

                // Calculate amounts
                decimal extraCharges = decimal.Parse(lblExtraCharges.Text.Replace("₹", ""));
                decimal totalAmount = decimal.Parse(lblTotalAmount.Text.Replace("₹", ""));
                decimal paidAmount = currentBooking.paid_amount + balanceAmount;

                // Disable button to prevent double clicks
                btnCompletePayment.IsEnabled = false;

                // Complete payment
                var result = await OfflineBookingStorage.CompleteBookingWithPaymentAsync(
                    currentBooking.booking_id,
                    paidAmount,
                    totalAmount,
                    extraCharges,
                    paymentMethod,
                    outTime
                );

                bool success = result.Contains("✅");

                if (success)
                {
                    MessageBox.Show($"✅ Payment completed successfully!\n\n" +
                        $"📋 Booking ID: {currentBooking.booking_id}\n" +
                        $"👤 Customer: {currentBooking.guest_name}\n" +
                        $"💰 Total Amount: ₹{totalAmount:F2}\n" +
                        $"💵 Paid Amount: ₹{paidAmount:F2}\n" +
                        $"💳 Payment Method: {paymentMethod}\n" +
                        $"⏰ Out Time: {outDateTime:yyyy-MM-dd HH:mm:ss}", 
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                    Logger.Log($"Payment completed for booking {currentBooking.booking_id} - Amount: {paidAmount}, Method: {paymentMethod}");

                    // Close the control
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    // MessageBox.Show($"Failed to complete payment!\n\n{result}", "Error", 
                    //     MessageBoxButton.OK, MessageBoxImage.Error);
                    btnCompletePayment.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                // MessageBox.Show($"Error completing payment: {ex.Message}", "Error", 
                //     MessageBoxButton.OK, MessageBoxImage.Error);
                btnCompletePayment.IsEnabled = true;
            }
        }
    }
}
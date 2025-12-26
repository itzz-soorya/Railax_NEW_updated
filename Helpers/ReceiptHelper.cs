using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using UserModule.Models;

namespace UserModule
{
    public static class ReceiptHelper
    {
        private static BitmapSource GenerateBarcodeImage(string text, int width = 300, int height = 80)
        {
            if (string.IsNullOrEmpty(text)) text = "NA";

            var writer = new BarcodeWriterPixelData
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 2
                }
            };

            var pixelData = writer.Write(text);

            int stride = pixelData.Width * 4;
            return BitmapSource.Create(pixelData.Width, pixelData.Height,
                300, 300, PixelFormats.Bgra32, null, pixelData.Pixels, stride);
        }

        private static UIElement BuildReceiptVisual(
            string billId,
            string customerName,
            string phoneNo,
            int totalHours,
            int persons,
            double ratePerPerson,
            double paidAmount,
            double extraCharges = 0,
            string heading1 = "Railway Booking",
            string heading2 = "",
            string info1 = "",
            string info2 = "",
            string note = "Thank you for your visit!",
            string hallName = "")
        {
            double baseAmount = ratePerPerson * persons * Math.Max(1, totalHours);
            double totalAmount = baseAmount + extraCharges;
            double balance = totalAmount - paidAmount;

            // Root panel
            var root = new Border
            {
                Background = Brushes.White,
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(6),
                Child = new StackPanel
                {
                    Width = 320, // default receipt width; Print helper will scale if needed
                    Orientation = Orientation.Vertical
                }
            };

            var stack = (StackPanel)root.Child;

            // Header - Heading 1
            if (!string.IsNullOrWhiteSpace(heading1))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = heading1,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                });
            }

            // Header - Heading 2
            if (!string.IsNullOrWhiteSpace(heading2))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = heading2,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                });
            }

            // Info 1
            if (!string.IsNullOrWhiteSpace(info1))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = info1,
                    FontSize = 10,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 1)
                });
            }

            // Info 2
            if (!string.IsNullOrWhiteSpace(info2))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = info2,
                    FontSize = 10,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6)
                });
            }

            // Hall Name (if provided)
            if (!string.IsNullOrWhiteSpace(hallName))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = hallName,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 6)
                });
            }

            // Separator after header
            stack.Children.Add(new Separator { Margin = new Thickness(0, 6, 0, 6) });

            // Helper function to add key-value rows
            void AddRow(string label, string value, bool isLast = false)
            {
                var row = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    Margin = new Thickness(0, 0, 0, isLast ? 6 : 4),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                
                var left = new TextBlock 
                { 
                    Text = label.PadRight(20), 
                    FontSize = 12,
                    FontFamily = new FontFamily("Consolas, Courier New")
                };
                
                var colon = new TextBlock 
                { 
                    Text = ": ", 
                    FontSize = 12,
                    FontFamily = new FontFamily("Consolas, Courier New")
                };
                
                var right = new TextBlock 
                { 
                    Text = value, 
                    FontSize = 12,
                    FontFamily = new FontFamily("Consolas, Courier New")
                };
                
                row.Children.Add(left);
                row.Children.Add(colon);
                row.Children.Add(right);
                stack.Children.Add(row);
            }

            // Customer details in key-value format
            AddRow("Name", customerName);
            AddRow("Phone", phoneNo);
            AddRow("Persons", persons.ToString());

            // Separator
            stack.Children.Add(new Separator { Margin = new Thickness(0, 6, 0, 6) });

            // Billing details
            AddRow("Total Hours", totalHours.ToString());
            AddRow("Rate / Person", $"₹{ratePerPerson}");
            AddRow("Base Amount", $"₹{baseAmount}");
            
            // Show extra charges if any
            if (extraCharges > 0)
            {
                AddRow("Extra Charges", $"₹{extraCharges}");
            }
            
            AddRow("Total Amount", $"₹{totalAmount}");
            AddRow("Paid Amount", $"₹{paidAmount}");
            AddRow("Balance", $"₹{balance}", true);

            // Space before barcode
            stack.Children.Add(new StackPanel { Height = 8 });

            // Barcode image
            var barcode = new Image
            {
                Source = GenerateBarcodeImage(billId, width: 280, height: 60),
                Width = 280,
                Height = 60,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 4)
            };
            stack.Children.Add(barcode);

            stack.Children.Add(new TextBlock
            {
                Text = "Scan to close the bill",
                FontSize = 11,
                Foreground = Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });

            // Note at the bottom
            if (!string.IsNullOrWhiteSpace(note))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"Note: {note}",
                    FontSize = 10,
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0)
                });
            }

            return root;
        }

        public static bool GenerateAndPrintReceipt(Booking1 booking, string? savePngPath = null, double extraCharges = 0)
        {
            if (booking == null) throw new ArgumentNullException(nameof(booking));

            // calculate values (defensive conversions)
            int hours = booking.total_hours;
            int persons = booking.number_of_persons;
            double rate = Convert.ToDouble(booking.price_per_person);
            double paid = Convert.ToDouble(booking.paid_amount);

            // Get printer details from storage
            var printerDetails = OfflineBookingStorage.GetPrinterDetails();

            var visual = BuildReceiptVisual(
                billId: booking.booking_id ?? "N/A",
                customerName: booking.guest_name ?? "",
                phoneNo: booking.phone_number ?? "",
                totalHours: hours,
                persons: persons,
                ratePerPerson: rate,
                paidAmount: paid,
                extraCharges: extraCharges,
                heading1: printerDetails.heading1,
                heading2: printerDetails.heading2,
                info1: printerDetails.info1,
                info2: printerDetails.info2,
                note: printerDetails.note,
                hallName: printerDetails.hallName
            );

            // Optionally save to PNG file for record
            if (!string.IsNullOrWhiteSpace(savePngPath))
            {
                try
                {
                    SaveVisualToPng(visual, savePngPath);
                }
                catch (Exception)
                {
                    // swallow or log — saving is optional
                    MessageBox.Show($"Failed to save receipt PNG", "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            // Print (uses your PrinterHelper.TryPrint that checks printer online)
            bool printed = PrinterHelper.TryPrint(visual);

            return printed;
        }

        /// <summary>
        /// Utility: render UIElement to PNG file on disk.
        /// </summary>
        private static void SaveVisualToPng(UIElement visual, string path)
        {
            if (visual == null) throw new ArgumentNullException(nameof(visual));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));

            // measure & arrange at desired size (use 320x... like receipt width)
            double width = 320;
            double height = 600; // generous; RenderTargetBitmap will crop if smaller needed
            visual.Measure(new Size(width, height));
            visual.Arrange(new Rect(new Size(width, height)));
            visual.UpdateLayout();

            var rtb = new RenderTargetBitmap((int)width, (int)height, 300, 300, PixelFormats.Pbgra32);
            rtb.Render(visual);

            // Trim transparent bottom if you want (left as-is for simplicity)
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                encoder.Save(fs);
            }
        }
    }
}

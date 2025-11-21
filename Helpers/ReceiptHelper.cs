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
                96, 96, PixelFormats.Bgra32, null, pixelData.Pixels, stride);
        }

        private static UIElement BuildReceiptVisual(
            string billId,
            string customerName,
            string phoneNo,
            int totalHours,
            int persons,
            double ratePerPerson,
            double paidAmount)
        {
            double totalAmount = ratePerPerson * persons * Math.Max(1, totalHours);
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

            // Header
            stack.Children.Add(new TextBlock
            {
                Text = "Erode Railway",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });

            // Bill meta
            stack.Children.Add(new TextBlock
            {
                Text = $"Bill ID: {billId}",
                FontSize = 12,
                Foreground = Brushes.Black
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"Name: {customerName}",
                FontSize = 12,
                Foreground = Brushes.Black
            });
            stack.Children.Add(new TextBlock
            {
                Text = $"Phone: {phoneNo}",
                FontSize = 12,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 0, 6)
            });

            // Separator
            stack.Children.Add(new Separator { Margin = new Thickness(0, 6, 0, 6) });

            // Details grid
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            void AddRow(string label, string value)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
                var left = new TextBlock { Text = label, FontSize = 13 };
                var right = new TextBlock { Text = value, FontSize = 13, HorizontalAlignment = HorizontalAlignment.Right };
                row.Children.Add(left);
                row.Children.Add(new StackPanel { Width = 8 }); // spacer
                row.Children.Add(right);
                stack.Children.Add(row);
            }

            AddRow("Total Hours", totalHours.ToString());
            AddRow("Persons", persons.ToString());
            AddRow("Rate / Person", $"₹{ratePerPerson}");
            AddRow("Total Amount", $"₹{totalAmount}");
            AddRow("Paid Amount", $"₹{paidAmount}");
            AddRow("Balance", $"₹{balance}");

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
                Margin = new Thickness(0, 4, 0, 0)
            });

            return root;
        }

        public static bool GenerateAndPrintReceipt(Booking1 booking, string savePngPath = null)
        {
            if (booking == null) throw new ArgumentNullException(nameof(booking));

            // calculate values (defensive conversions)
            int hours = booking.total_hours;
            int persons = booking.number_of_persons;
            double rate = Convert.ToDouble(booking.price_per_person);
            double paid = Convert.ToDouble(booking.paid_amount);

            var visual = BuildReceiptVisual(
                billId: booking.booking_id ?? "N/A",
                customerName: booking.guest_name ?? "",
                phoneNo: booking.phone_number ?? "",
                totalHours: hours,
                persons: persons,
                ratePerPerson: rate,
                paidAmount: paid
            );

            // Optionally save to PNG file for record
            if (!string.IsNullOrWhiteSpace(savePngPath))
            {
                try
                {
                    SaveVisualToPng(visual, savePngPath);
                }
                catch (Exception ex)
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

            var rtb = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
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

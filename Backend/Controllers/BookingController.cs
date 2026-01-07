using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text.Json.Serialization;

namespace RAILWAY_BACKEND.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public BookingController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        #region Create Bookings (Single Online Booking)

        [HttpPost("online-book")]
        public IActionResult CreateOnlineBooking([FromBody] NewBookingRequest booking)
        {
            if (booking == null)
                return BadRequest(new { message = "No booking provided" });

            string connectionString = _configuration.GetConnectionString("DefaultConnection")!;

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            // Step 1: Get admin_id for the worker
            string adminId;
            using (var getAdminCmd = new NpgsqlCommand(
                "SELECT admin_id FROM worker_accounts WHERE worker_id = @workerId", connection))
            {
                getAdminCmd.Parameters.AddWithValue("@workerId", booking.WorkerId);
                var result = getAdminCmd.ExecuteScalar();

                if (result == null)
                    return BadRequest(new { message = $"Invalid worker_id {booking.WorkerId}. No admin found." });

                adminId = result.ToString()!;
            }

            // Step 2: Prepare insert query (includes booking_id)
            string insertQuery = @"
                INSERT INTO bookings 
                (booking_id, admin_id, worker_id, guest_name, phone_number, number_of_persons, 
                 booking_type, total_hours, booking_date, in_time, out_time, 
                 proof_type, proof_id, price_per_person, total_amount, paid_amount, 
                 balance_amount, payment_method, status)
                VALUES 
                (@booking_id, @admin_id, @worker_id, @guest_name, @phone_number, @number_of_persons, 
                 @booking_type, @total_hours, @booking_date, @in_time, @out_time, 
                 @proof_type, @proof_id, @price_per_person, @total_amount, @paid_amount, 
                 @balance_amount, @payment_method, @status);";

            string status = string.IsNullOrEmpty(booking.Status) ? "Active" : booking.Status;

            using var cmd = new NpgsqlCommand(insertQuery, connection);
            cmd.Parameters.AddWithValue("@booking_id", booking.BookingId);
            cmd.Parameters.AddWithValue("@admin_id", adminId);
            cmd.Parameters.AddWithValue("@worker_id", booking.WorkerId);
            cmd.Parameters.AddWithValue("@guest_name", booking.GuestName);
            cmd.Parameters.AddWithValue("@phone_number", booking.PhoneNumber);
            cmd.Parameters.AddWithValue("@number_of_persons", booking.NumberOfPersons);
            cmd.Parameters.AddWithValue("@booking_type", booking.BookingType);
            cmd.Parameters.AddWithValue("@total_hours", booking.TotalHours);
            cmd.Parameters.AddWithValue("@booking_date", booking.BookingDate);
            cmd.Parameters.AddWithValue("@in_time", booking.InTime);
            cmd.Parameters.AddWithValue("@out_time", booking.OutTime ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@proof_type", booking.ProofType);
            cmd.Parameters.AddWithValue("@proof_id", booking.ProofId);
            cmd.Parameters.AddWithValue("@price_per_person", booking.PricePerPerson);
            cmd.Parameters.AddWithValue("@total_amount", booking.TotalAmount);
            cmd.Parameters.AddWithValue("@paid_amount", booking.PaidAmount);
            cmd.Parameters.AddWithValue("@balance_amount", booking.BalanceAmount);
            cmd.Parameters.AddWithValue("@payment_method", booking.PaymentMethod);
            cmd.Parameters.AddWithValue("@status", status);

            cmd.ExecuteNonQuery();

            // Step 3: Update worker balance
string updateWorkerBalanceQuery = @"
    UPDATE public.worker_accounts
    SET 
        balance = COALESCE(balance, 0) + @total_amount,
        updated_at = NOW()
    WHERE admin_id = @admin_id
      AND worker_id = @worker_id;
";

using var updateCmd = new NpgsqlCommand(updateWorkerBalanceQuery, connection);
updateCmd.Parameters.AddWithValue("@total_amount", booking.TotalAmount);
updateCmd.Parameters.AddWithValue("@admin_id", adminId);
updateCmd.Parameters.AddWithValue("@worker_id", booking.WorkerId);

updateCmd.ExecuteNonQuery();

            return Ok(new
            {
                message = "Booking created successfully",
                booking_id = booking.BookingId
            });
        }

        [HttpPost("create")]
        public IActionResult CreateBookings([FromBody] List<NewBookingRequest> bookings)
        {
            if (bookings == null || bookings.Count == 0)
                return BadRequest(new { message = "No bookings provided" });

            string connectionString = _configuration.GetConnectionString("DefaultConnection")!;

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            var createdBookings = new List<object>();

            foreach (var request in bookings)
            {
            // Step 1: Find admin_id for the given worker
                string adminId;
                using (var getAdminCmd = new NpgsqlCommand(
                    "SELECT admin_id FROM worker_accounts WHERE worker_id = @workerId", connection))
                {
                    getAdminCmd.Parameters.AddWithValue("@workerId", request.WorkerId);
                    var result = getAdminCmd.ExecuteScalar();
                    if (result == null)
                        return BadRequest(new { message = $"Invalid worker_id {request.WorkerId}. No admin found." });

                    adminId = result.ToString()!;
                }

                // Step 2: Insert with booking_id (manually)
                string insertQuery = @"
                    INSERT INTO bookings 
                    (booking_id, admin_id, worker_id, guest_name, phone_number, number_of_persons, 
                    booking_type, total_hours, booking_date, in_time, out_time,
                    proof_type, proof_id, price_per_person, total_amount, paid_amount, 
                    balance_amount, payment_method, status)
                    VALUES 
                    (@booking_id, @admin_id, @worker_id, @guest_name, @phone_number, @number_of_persons, 
                    @booking_type, @total_hours, @booking_date, @in_time, @out_time,
                    @proof_type, @proof_id, @price_per_person, @total_amount, @paid_amount, 
                    @balance_amount, @payment_method, @status);";

                string status = string.IsNullOrEmpty(request.Status) ? "Active" : request.Status;

                using var cmd = new NpgsqlCommand(insertQuery, connection);
                cmd.Parameters.AddWithValue("@booking_id", request.BookingId);
                cmd.Parameters.AddWithValue("@admin_id", adminId);
                cmd.Parameters.AddWithValue("@worker_id", request.WorkerId);
                cmd.Parameters.AddWithValue("@guest_name", request.GuestName);
                cmd.Parameters.AddWithValue("@phone_number", request.PhoneNumber);
                cmd.Parameters.AddWithValue("@number_of_persons", request.NumberOfPersons);
                cmd.Parameters.AddWithValue("@booking_type", request.BookingType);
                cmd.Parameters.AddWithValue("@total_hours", request.TotalHours);
                cmd.Parameters.AddWithValue("@booking_date", request.BookingDate);
                cmd.Parameters.AddWithValue("@in_time", request.InTime);
                cmd.Parameters.AddWithValue("@out_time", request.OutTime ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@proof_type", request.ProofType);
                cmd.Parameters.AddWithValue("@proof_id", request.ProofId);
                cmd.Parameters.AddWithValue("@price_per_person", request.PricePerPerson);
                cmd.Parameters.AddWithValue("@total_amount", request.TotalAmount);
                cmd.Parameters.AddWithValue("@paid_amount", request.PaidAmount);
                cmd.Parameters.AddWithValue("@balance_amount", request.BalanceAmount);
                cmd.Parameters.AddWithValue("@payment_method", request.PaymentMethod);
                cmd.Parameters.AddWithValue("@status", status);

                cmd.ExecuteNonQuery();

                // Step 3: Update worker balance
        string updateWorkerBalanceQuery = @"
            UPDATE public.worker_accounts
            SET 
                balance = COALESCE(balance, 0) + @total_amount,
                updated_at = NOW()
            WHERE admin_id = @admin_id
              AND worker_id = @worker_id;
        ";

        using (var updateCmd = new NpgsqlCommand(updateWorkerBalanceQuery, connection))
        {
            updateCmd.Parameters.AddWithValue("@total_amount", request.TotalAmount);
            updateCmd.Parameters.AddWithValue("@admin_id", adminId);
            updateCmd.Parameters.AddWithValue("@worker_id", request.WorkerId);

            updateCmd.ExecuteNonQuery();
        }

                createdBookings.Add(new
            {
            booking_id = request.BookingId,
            admin_id = adminId,
            worker_id = request.WorkerId,
            status = status
        });
    }

    return Ok(new
    {
        message = "Bookings created successfully",
        bookings = createdBookings
    });
}


        #endregion

        #region Checkout / Complete Booking

        [HttpPut("checkout")]
        public IActionResult Checkout([FromBody] CheckoutRequest request)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            // Build dynamic update query based on provided fields
            var updateFields = new List<string>
            {
                "out_time = @out_time",
                "status = @status",
                "payment_method = @payment_method",
                "updated_at = CURRENT_TIMESTAMP"
            };

            if (request.total_amount.HasValue)
                updateFields.Add("total_amount = @total_amount");
            if (request.paid_amount.HasValue)
                updateFields.Add("paid_amount = @paid_amount");
            if (request.balance_amount.HasValue)
                updateFields.Add("balance_amount = @balance_amount");
            if (request.total_hours.HasValue)
                updateFields.Add("total_hours = @total_hours");

            string updateQuery = $@"
            UPDATE bookings 
            SET {string.Join(", ", updateFields)}
            WHERE booking_id = @booking_id;";

            using var cmd = new NpgsqlCommand(updateQuery, connection);

            TimeSpan outTime = request.out_time == TimeSpan.Zero ? DateTime.Now.TimeOfDay : request.out_time;
            string status = string.IsNullOrEmpty(request.status) ? "Completed" : request.status;
            string paymentMethod = request.payment_method ?? "cash";

            cmd.Parameters.AddWithValue("@out_time", outTime);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@payment_method", paymentMethod);
            cmd.Parameters.AddWithValue("@booking_id", request.booking_id);

            if (request.total_amount.HasValue)
                cmd.Parameters.AddWithValue("@total_amount", request.total_amount.Value);
            if (request.paid_amount.HasValue)
                cmd.Parameters.AddWithValue("@paid_amount", request.paid_amount.Value);
            if (request.balance_amount.HasValue)
                cmd.Parameters.AddWithValue("@balance_amount", request.balance_amount.Value);
            if (request.total_hours.HasValue)
                cmd.Parameters.AddWithValue("@total_hours", request.total_hours.Value);

            int rows = cmd.ExecuteNonQuery();
            if (rows == 0)
                return NotFound(new { message = "Booking not found" });

            return Ok(new
            {
                message = "Checkout completed",
                booking_id = request.booking_id,
                out_time = outTime.ToString(),
                status = status,
                payment_method = paymentMethod,
                total_amount = request.total_amount,
                paid_amount = request.paid_amount,
                balance_amount = request.balance_amount,
                total_hours = request.total_hours
            });
        }



        #endregion

        #region Get Bookings

        [HttpGet("{id}")]
        public IActionResult GetBooking(int id)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string query = "SELECT * FROM bookings WHERE booking_id = @id";
            using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return NotFound(new { message = "Booking not found" });

            var booking = new
            {
                booking_id = reader["booking_id"],
                guest_name = reader["guest_name"],
                phone_number = reader["phone_number"],
                booking_date = reader["booking_date"],
                in_time = reader["in_time"],
                out_time = reader["out_time"],
                status = reader["status"]
            };

            return Ok(booking);
        }

        [HttpGet("active/{id}")]
        public IActionResult GetActiveBookings(int id)
        {
            string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            string query = @"
                SELECT booking_id, guest_name, phone_number, booking_date, in_time, status 
                FROM bookings 
                WHERE status = 'Active' AND worker_id = @worker_id
                ORDER BY booking_date DESC";

            using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@worker_id", id);

            using var reader = cmd.ExecuteReader();

            var activeBookings = new List<object>();
            while (reader.Read())
            {
                activeBookings.Add(new
                {
                    booking_id = reader["booking_id"],
                    guest_name = reader["guest_name"],
                    phone_number = reader["phone_number"],
                    booking_date = reader["booking_date"],
                    in_time = reader["in_time"],
                    status = reader["status"]
                });
            }

            return Ok(activeBookings);
        }

        #endregion

        #region Sync Completed Bookings

        [HttpPost("completed-today")]
        public IActionResult GetCompletedBookingsToday([FromBody] CompletedBookingsRequest request)
        {
            if (string.IsNullOrEmpty(request.admin_id))
                return BadRequest(new { message = "admin_id is required" });

            if (string.IsNullOrEmpty(request.worker_id))
                return BadRequest(new { message = "worker_id is required" });

            string connectionString = _configuration.GetConnectionString("DefaultConnection")!;
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            // Query to get completed booking IDs for the worker on the specified date
            string query = @"
    SELECT booking_id, out_time 
    FROM bookings 
    WHERE admin_id = @admin_id 
      AND worker_id = @worker_id 
      AND booking_date = CURRENT_DATE
      AND status = 'completed'
    ORDER BY booking_id DESC";


            using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@admin_id", request.admin_id);
            cmd.Parameters.AddWithValue("@worker_id", request.worker_id);

            using var reader = cmd.ExecuteReader();

            var completedBookings = new List<object>();
            while (reader.Read())
            {
                completedBookings.Add(new
                {
                    booking_id = reader["booking_id"].ToString()!,
                    out_time = reader["out_time"] != DBNull.Value ? reader["out_time"].ToString() : null
                });
            }

            return Ok(new
            {
                completedBookingIds = completedBookings
            });
        }

        #endregion

        #region Request Models

        public class CompletedBookingsRequest
        {
            public string admin_id { get; set; } = "";
            public string worker_id { get; set; } = "";
        }

        public class CheckoutRequest
        {
            public string booking_id { get; set; } = "";
            public TimeSpan out_time { get; set; }
            public string status { get; set; } = "";
            public string payment_method { get; set; } = "cash";
            public decimal? total_amount { get; set; }
            public decimal? paid_amount { get; set; }
            public decimal? balance_amount { get; set; }
            public int? total_hours { get; set; }
        }

        public class NewBookingRequest
        {
            [JsonPropertyName("booking_id")]
            public string BookingId { get; set; } = "";

            [JsonPropertyName("worker_id")]
            public string WorkerId { get; set; } = "";

            [JsonPropertyName("guest_name")]
            public string GuestName { get; set; } = "";

            [JsonPropertyName("phone_number")]
            public string PhoneNumber { get; set; } = "";

            [JsonPropertyName("number_of_persons")]
            public int NumberOfPersons { get; set; }

            [JsonPropertyName("booking_type")]
            public string BookingType { get; set; } = "";

            [JsonPropertyName("total_hours")]
            public int TotalHours { get; set; }

            [JsonPropertyName("booking_date")]
            public DateTime BookingDate { get; set; }

            [JsonPropertyName("in_time")]
            public TimeSpan InTime { get; set; }

            [JsonPropertyName("out_time")]
            public TimeSpan? OutTime { get; set; }

            [JsonPropertyName("proof_type")]
            public string ProofType { get; set; } = "";

            [JsonPropertyName("proof_id")]
            public string ProofId { get; set; } = "";

            [JsonPropertyName("price_per_person")]
            public decimal PricePerPerson { get; set; }

            [JsonPropertyName("total_amount")]
            public decimal TotalAmount { get; set; }

            [JsonPropertyName("paid_amount")]
            public decimal PaidAmount { get; set; }

            [JsonPropertyName("balance_amount")]
            public decimal BalanceAmount { get; set; }

            [JsonPropertyName("payment_method")]
            public string PaymentMethod { get; set; } = "Cash";

            [JsonPropertyName("status")]
            public string? Status { get; set; }
        }

        #endregion
    }
}

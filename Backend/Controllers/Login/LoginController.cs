using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RAILWAY_BACKEND.Connection;
using System.Threading.Tasks;
using BCrypt.Net; 

namespace RAILWAY_BACKEND.Controllers.Login
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly DatabaseConnection _dbConnection;

        public LoginController(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        [HttpPost("check")]
        public async Task<IActionResult> CheckLogin([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Username and password are required" });
            }

            await using var connection = _dbConnection.GetConnection();
            await connection.OpenAsync();


            string sql = @"SELECT worker_id, admin_id, full_name, password_hash 
                           FROM worker_accounts 
                           WHERE user_name=@username 
                           LIMIT 1";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("username", request.Username);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                string workerId = reader["worker_id"].ToString() ?? "";
                string adminId = reader["admin_id"].ToString() ?? "";
                string fullName = reader["full_name"].ToString() ?? "";
                string storedHash = reader["password_hash"].ToString() ?? "";

                // ✅ Verify entered password with stored bcrypt hash
                bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, storedHash);

                if (!isPasswordValid)
                {
                    return Unauthorized(new { message = "Invalid username or password" });
                }

                return Ok(new
                {
                    message = "Login successful",
                    worker_id = workerId,
                    admin_id = adminId,
                    name = fullName
                });
            }

            return Unauthorized(new { message = "Invalid username or password" });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}

using Microsoft.AspNetCore.Mvc;
using RAILWAY_BACKEND.Connection;
using RAILWAY_BACKEND.Models;
using Npgsql;
using System.Collections.Generic;

namespace RAILWAY_BACKEND.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly DatabaseConnection _dbConnection;

        public SettingsController(DatabaseConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        [HttpGet("hall-types/{adminId}")]
public async Task<IActionResult> GetHallTypes(string adminId)
{
    try
    {
        using var connection = _dbConnection.GetConnection();
        await connection.OpenAsync();

        string query = @"
            SELECT id, admin_id, type_1, type_1_amount, type_2, grace_amount,
                   advance_payment_enabled, advanced_payment, grace_amount_type2
            FROM public.settings
            WHERE admin_id = @adminId";

        using var cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@adminId", adminId);

        using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return Ok(new
            {
                type_1 = reader.IsDBNull(2) ? null : reader.GetString(2),
                type_1_amount = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3),
                type_2 = reader.IsDBNull(4) ? null : reader.GetString(4),
                grace_amount = reader.IsDBNull(5) ? (decimal?)null : reader.GetDecimal(5),
                advance_payment_enabled = reader.IsDBNull(6) ? false : reader.GetBoolean(6),
                advance_payment = reader.IsDBNull(7) ? (decimal?)null : reader.GetDecimal(7),
                grace_amount_type_2 = reader.IsDBNull(8) ? (decimal?)null : reader.GetDecimal(8)
            });
        }

        return NotFound(new { message = "Hall not found" });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { message = "An error occurred", error = ex.Message });
    }
}



        [HttpGet("printer-details/{adminId}")]
public async Task<IActionResult> GetPrinterDetails(string adminId)
{
    try
    {
        using var connection = _dbConnection.GetConnection();
        await connection.OpenAsync();

        string query = @"
            SELECT id, admin_id, heading1, heading2, info1, info2, note, hall_name, logo_url
            FROM public.printer
            WHERE admin_id = @adminId";

        using var cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@adminId", adminId);

        using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            var details = new
            {
                heading1 = reader.IsDBNull(2) ? null : reader.GetString(2),
                heading2 = reader.IsDBNull(3) ? null : reader.GetString(3),
                info1 = reader.IsDBNull(4) ? null : reader.GetString(4),
                info2 = reader.IsDBNull(5) ? null : reader.GetString(5),
                note = reader.IsDBNull(6) ? null : reader.GetString(6),
                hall_name = reader.IsDBNull(7) ? null : reader.GetString(7),
                logo_url = reader.IsDBNull(8) ? null : reader.GetString(8)
            };

            return Ok(details);
        }

        return NotFound(new { message = "Printer details not found" });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { message = "An error occurred", error = ex.Message });
    }
}

        [HttpGet("sleeping-details/{adminId}")]
public async Task<IActionResult> GetType2Details(string adminId)
{
    try
    {
        using var connection = _dbConnection.GetConnection();
        await connection.OpenAsync();

        // Step 1: Get settings.id using adminId
        string settingsQuery = @"
            SELECT id 
            FROM public.settings 
            WHERE admin_id = @adminId
            LIMIT 1";

        int? settingId = null;
        using (var settingsCmd = new NpgsqlCommand(settingsQuery, connection))
        {
            settingsCmd.Parameters.AddWithValue("@adminId", adminId);

            var result = await settingsCmd.ExecuteScalarAsync();
            if (result != null)
                settingId = Convert.ToInt32(result);
        }

        if (settingId == null)
        {
            return NotFound(new { message = "Settings not found for this admin" });
        }

        // Step 2: Fetch Type2 details using setting_id
        string query = @"
            SELECT id, min_duration, max_duration, amount
            FROM public.type2_amount
            WHERE setting_id = @settingId";

        using var cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@settingId", settingId);

        using var reader = await cmd.ExecuteReaderAsync();

        var list = new List<object>();

        while (await reader.ReadAsync())
        {
            list.Add(new
            {
                id = reader.GetInt32(0),
                min_duration = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                max_duration = reader.IsDBNull(2) ? (int?)null : reader.GetInt32(2),
                amount = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3)
            });
        }

        if (list.Count == 0)
        {
            return NotFound(new { message = "Type2 details not found" });
        }

        return Ok(list);
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { message = "An error occurred", error = ex.Message });
    }
}


    }
}

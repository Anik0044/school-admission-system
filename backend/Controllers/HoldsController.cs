using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
public class HoldsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly int _holdDurationSeconds;

    public HoldsController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _holdDurationSeconds = int.Parse(config["HOLD_DURATION_SECONDS"] ?? "600");
    }

    // 4. POST /classes/{classId}/holds (Create a seat hold)
    [HttpPost("classes/{classId}/holds")]
    public async Task<IActionResult> CreateHold(string classId, [FromHeader(Name = "X-Student-Id")] string studentId)
    {
        if (string.IsNullOrEmpty(studentId)) return BadRequest(new { message = "X-Student-Id header is required." });

        // Use database transaction to prevent overbooking across multiple processes
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Lock the Class row so no other process can read it until we finish
            var c = await _context.Classes
                .FromSqlRaw("SELECT * FROM \"Classes\" WHERE \"Id\" = {0} FOR UPDATE", classId)
                .Include(x => x.Holds)
                .FirstOrDefaultAsync();

            if (c == null) return NotFound(new { message = "Class not found." });

            await ExpireOldHoldsAsync();

            var existingActiveHold = c.Holds.FirstOrDefault(h => h.StudentId == studentId && 
                (h.Status == HoldStatus.HELD && h.ExpiresAt > DateTime.UtcNow || h.Status == HoldStatus.CONFIRMED));
            
            if (existingActiveHold != null)
                return BadRequest(new { message = "You already have an active or confirmed application for this class." });

            var heldSeats = c.Holds.Count(h => h.Status == HoldStatus.HELD && h.ExpiresAt > DateTime.UtcNow);
            var confirmedSeats = c.Holds.Count(h => h.Status == HoldStatus.CONFIRMED);
            
            if (c.TotalSeats - heldSeats - confirmedSeats <= 0)
                return StatusCode(409, new { message = "No seats available." });

            var hold = new Hold
            {
                ClassId = classId,
                StudentId = studentId,
                ExpiresAt = DateTime.UtcNow.AddSeconds(_holdDurationSeconds)
            };

            _context.Holds.Add(hold);
            await _context.SaveChangesAsync();
            
            await transaction.CommitAsync();

            return StatusCode(201, new
            {
                holdId = hold.Id,
                classId = hold.ClassId,
                studentId = hold.StudentId,
                status = hold.Status.ToString(),
                expiresAt = hold.ExpiresAt
            });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "An error occurred while processing the request." });
        }
    }

    // 5. GET /holds/{holdId}
    [HttpGet("holds/{holdId}")]
    public async Task<IActionResult> GetHold(string holdId)
    {
        await ExpireOldHoldsAsync();
        var hold = await _context.Holds.FindAsync(holdId);
        if (hold == null) return NotFound(new { message = "Hold not found." });

        return Ok(new
        {
            holdId = hold.Id,
            classId = hold.ClassId,
            status = hold.Status.ToString(),
            expiresAt = hold.ExpiresAt
        });
    }

    // 6. POST /holds/{holdId}/confirm
    [HttpPost("holds/{holdId}/confirm")]
    public async Task<IActionResult> ConfirmHold(string holdId, [FromHeader(Name = "X-Student-Id")] string studentId)
    {
        if (string.IsNullOrEmpty(studentId)) return BadRequest(new { message = "X-Student-Id header is required." });

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await ExpireOldHoldsAsync();
            var hold = await _context.Holds.FirstOrDefaultAsync(h => h.Id == holdId);
            
            if (hold == null) return NotFound(new { message = "Hold not found." });
            if (hold.StudentId != studentId) return Unauthorized(new { message = "You do not own this hold." });
            if (hold.Status == HoldStatus.CONFIRMED) return Ok(new { holdId = hold.Id, status = "CONFIRMED" });
            
            if (hold.Status == HoldStatus.EXPIRED || hold.ExpiresAt <= DateTime.UtcNow)
            {
                hold.Status = HoldStatus.EXPIRED;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return BadRequest(new { message = "Hold has expired." });
            }

            if (hold.Status == HoldStatus.CANCELLED)
                return BadRequest(new { message = "Hold was cancelled." });

            hold.Status = HoldStatus.CONFIRMED;
            await _context.SaveChangesAsync();
            
            await transaction.CommitAsync();
            return Ok(new { holdId = hold.Id, status = "CONFIRMED" });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { message = "An error occurred." });
        }
    }

    // 7. DELETE /holds/{holdId}
    [HttpDelete("holds/{holdId}")]
    public async Task<IActionResult> CancelHold(string holdId, [FromHeader(Name = "X-Student-Id")] string studentId)
    {
        if (string.IsNullOrEmpty(studentId)) return BadRequest(new { message = "X-Student-Id header is required." });

        var hold = await _context.Holds.FirstOrDefaultAsync(h => h.Id == holdId);
        if (hold == null) return NotFound(new { message = "Hold not found." });
        if (hold.StudentId != studentId) return Unauthorized(new { message = "You do not own this hold." });

        if (hold.Status == HoldStatus.HELD)
        {
            hold.Status = HoldStatus.CANCELLED;
            await _context.SaveChangesAsync();
        }
        return NoContent();
    }

    // 8. GET /me/holds
    [HttpGet("me/holds")]
    public async Task<IActionResult> GetMyHolds([FromHeader(Name = "X-Student-Id")] string studentId)
    {
        if (string.IsNullOrEmpty(studentId)) return BadRequest(new { message = "X-Student-Id header is required." });

        await ExpireOldHoldsAsync();
        var holds = await _context.Holds
            .Include(h => h.Class)
            .Where(h => h.StudentId == studentId)
            .Select(h => new
            {
                holdId = h.Id,
                classId = h.ClassId,
                className = h.Class != null ? h.Class.Name : "Unknown",
                status = h.Status.ToString(),
                expiresAt = h.ExpiresAt
            })
            .ToListAsync();

        return Ok(holds);
    }

    private async Task ExpireOldHoldsAsync()
    {
        var expiredHolds = await _context.Holds
            .Where(h => h.Status == HoldStatus.HELD && h.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync();

        if (expiredHolds.Any())
        {
            foreach (var hold in expiredHolds)
            {
                hold.Status = HoldStatus.EXPIRED;
            }
            await _context.SaveChangesAsync();
        }
    }
}
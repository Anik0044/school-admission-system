using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
public class HoldsController : ControllerBase
{
    private readonly AppDbContext _context;
    // Static lock object to prevent concurrent requests from overbooking
    private static readonly object _lock = new object();
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

        // Use lock to ensure thread safety (only one request at a time)
        lock (_lock)
        {
            var c = _context.Classes.Include(x => x.Holds).FirstOrDefault(x => x.Id == classId);
            if (c == null) return NotFound(new { message = "Class not found." });

            ExpireOldHolds();

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
            _context.SaveChanges();

            return StatusCode(201, new
            {
                holdId = hold.Id,
                classId = hold.ClassId,
                studentId = hold.StudentId,
                status = hold.Status.ToString(),
                expiresAt = hold.ExpiresAt
            });
        }
    }

    // 5. GET /holds/{holdId} (Get hold status)
    [HttpGet("holds/{holdId}")]
    public async Task<IActionResult> GetHold(string holdId)
    {
        ExpireOldHolds();
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

    // 6. POST /holds/{holdId}/confirm (Confirm the hold)
    [HttpPost("holds/{holdId}/confirm")]
    public async Task<IActionResult> ConfirmHold(string holdId, [FromHeader(Name = "X-Student-Id")] string studentId)
    {
        if (string.IsNullOrEmpty(studentId)) return BadRequest(new { message = "X-Student-Id header is required." });

        lock (_lock)
        {
            ExpireOldHolds();
            var hold = _context.Holds.FirstOrDefault(h => h.Id == holdId);
            
            if (hold == null) return NotFound(new { message = "Hold not found." });
            if (hold.StudentId != studentId) return Unauthorized(new { message = "You do not own this hold." });
            if (hold.Status == HoldStatus.CONFIRMED) return Ok(new { holdId = hold.Id, status = "CONFIRMED" });
            
            if (hold.Status == HoldStatus.EXPIRED || hold.ExpiresAt <= DateTime.UtcNow)
            {
                hold.Status = HoldStatus.EXPIRED;
                _context.SaveChanges();
                return BadRequest(new { message = "Hold has expired." });
            }

            if (hold.Status == HoldStatus.CANCELLED)
                return BadRequest(new { message = "Hold was cancelled." });

            hold.Status = HoldStatus.CONFIRMED;
            _context.SaveChanges();

            return Ok(new { holdId = hold.Id, status = "CONFIRMED" });
        }
    }

    // 7. DELETE /holds/{holdId} (Cancel hold)
    [HttpDelete("holds/{holdId}")]
    public async Task<IActionResult> CancelHold(string holdId, [FromHeader(Name = "X-Student-Id")] string studentId)
    {
        if (string.IsNullOrEmpty(studentId)) return BadRequest(new { message = "X-Student-Id header is required." });

        lock (_lock)
        {
            var hold = _context.Holds.FirstOrDefault(h => h.Id == holdId);
            
            if (hold == null) return NotFound(new { message = "Hold not found." });
            if (hold.StudentId != studentId) return Unauthorized(new { message = "You do not own this hold." });

            if (hold.Status == HoldStatus.HELD)
            {
                hold.Status = HoldStatus.CANCELLED;
                _context.SaveChanges();
            }
            return NoContent();
        }
    }

    // 8. GET /me/holds (Get all holds for a student)
    [HttpGet("me/holds")]
    public async Task<IActionResult> GetMyHolds([FromHeader(Name = "X-Student-Id")] string studentId)
    {
        if (string.IsNullOrEmpty(studentId)) return BadRequest(new { message = "X-Student-Id header is required." });

        ExpireOldHolds();
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

    // Helper method to automatically expire holds
    private void ExpireOldHolds()
    {
        var expiredHolds = _context.Holds
            .Where(h => h.Status == HoldStatus.HELD && h.ExpiresAt <= DateTime.UtcNow)
            .ToList();

        if (expiredHolds.Any())
        {
            foreach (var hold in expiredHolds)
            {
                hold.Status = HoldStatus.EXPIRED;
            }
            _context.SaveChanges();
        }
    }
}
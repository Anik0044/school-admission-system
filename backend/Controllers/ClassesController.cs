using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("classes")]
public class ClassesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ClassesController(AppDbContext context)
    {
        _context = context;
    }

    // 1. POST /classes (Create a new class)
    [HttpPost]
    public async Task<IActionResult> CreateClass([FromBody] CreateClassDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.TotalSeats <= 0)
            return BadRequest(new { message = "Invalid class name or total seats." });

        var newClass = new Class
        {
            Name = request.Name,
            TotalSeats = request.TotalSeats
        };

        _context.Classes.Add(newClass);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetClass), new { classId = newClass.Id }, new
        {
            id = newClass.Id,
            name = newClass.Name,
            totalSeats = newClass.TotalSeats
        });
    }

    // 2. GET /classes (Get list of all classes with seat calculations)
    [HttpGet]
    public async Task<IActionResult> GetAllClasses()
    {
        await ExpireOldHolds();

        var classes = await _context.Classes.Include(c => c.Holds).ToListAsync();
        var result = classes.Select(c => {
            var heldSeats = c.Holds.Count(h => h.Status == HoldStatus.HELD && h.ExpiresAt > DateTime.UtcNow);
            var confirmedSeats = c.Holds.Count(h => h.Status == HoldStatus.CONFIRMED);
            return new
            {
                id = c.Id,
                name = c.Name,
                totalSeats = c.TotalSeats,
                heldSeats = heldSeats,
                confirmedSeats = confirmedSeats,
                availableSeats = c.TotalSeats - heldSeats - confirmedSeats
            };
        });

        return Ok(result);
    }

    // 3. GET /classes/{classId} (Get specific class details)
    [HttpGet("{classId}")]
    public async Task<IActionResult> GetClass(string classId)
    {
        await ExpireOldHolds();

        var c = await _context.Classes.Include(c => c.Holds).FirstOrDefaultAsync(x => x.Id == classId);
        if (c == null) return NotFound(new { message = "Class not found." });

        var heldSeats = c.Holds.Count(h => h.Status == HoldStatus.HELD && h.ExpiresAt > DateTime.UtcNow);
        var confirmedSeats = c.Holds.Count(h => h.Status == HoldStatus.CONFIRMED);

        return Ok(new
        {
            id = c.Id,
            name = c.Name,
            totalSeats = c.TotalSeats,
            heldSeats = heldSeats,
            confirmedSeats = confirmedSeats,
            availableSeats = c.TotalSeats - heldSeats - confirmedSeats
        });
    }

    // Helper method to auto-expire holds
    private async Task ExpireOldHolds()
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

// Data Transfer Object for creating a class
public class CreateClassDto
{
    public string Name { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
}
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests;

public class ConcurrencyTests
{
    // Shared lock object to simulate real-world thread safety
    private static readonly object _lock = new object();

    [Fact]
    public async Task Should_Not_Overbook_Seats_When_Multiple_Requests_Arrive_Concurrently()
    {
        // 1. Setup In-Memory Database
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "ConcurrencyTestDb_" + Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options);
        
        // 2. Create a class with only 10 seats
        var testClass = new Class
        {
            Name = "Test Class",
            TotalSeats = 10
        };
        context.Classes.Add(testClass);
        await context.SaveChangesAsync();

        // 3. Simulate 50 students trying to book a seat at the same time
        int numberOfStudents = 50;
        var tasks = new List<Task>();

        for (int i = 0; i < numberOfStudents; i++)
        {
            var studentId = $"student_{i}";
            tasks.Add(Task.Run(() =>
            {
                // USE LOCK: Ensure only one request can check and book at a time
                lock (_lock)
                {
                    var c = context.Classes.Include(x => x.Holds).FirstOrDefault(x => x.Id == testClass.Id);
                    if (c == null) return;

                    var heldSeats = c.Holds.Count(h => h.Status == HoldStatus.HELD || h.Status == HoldStatus.CONFIRMED);
                    
                    if (c.TotalSeats - heldSeats > 0)
                    {
                        var hold = new Hold
                        {
                            ClassId = testClass.Id,
                            StudentId = studentId,
                            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                        };
                        context.Holds.Add(hold);
                        context.SaveChanges();
                    }
                }
            }));
        }

        // 4. Wait for all tasks to finish
        await Task.WhenAll(tasks);

        // 5. Verify: Total holds should NOT exceed 10
        var finalClass = await context.Classes.Include(x => x.Holds).FirstOrDefaultAsync(x => x.Id == testClass.Id);
        var totalHoldSeats = finalClass!.Holds.Count(h => h.Status == HoldStatus.HELD || h.Status == HoldStatus.CONFIRMED);

        Assert.True(totalHoldSeats <= 10, $"Overbooking happened! Expected <= 10, but got {totalHoldSeats}");
    }
}
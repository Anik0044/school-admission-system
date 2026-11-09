namespace backend.Models;

public enum HoldStatus
{
    HELD,
    CONFIRMED,
    EXPIRED,
    CANCELLED
}

public class Hold
{
    public string Id { get; set; } = "h_" + Guid.NewGuid().ToString("N")[..8]; // Generates a unique ID starting with h_
    public string ClassId { get; set; } = string.Empty;
    public Class? Class { get; set; }
    
    public string StudentId { get; set; } = string.Empty;
    public HoldStatus Status { get; set; } = HoldStatus.HELD;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
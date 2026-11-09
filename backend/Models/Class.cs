namespace backend.Models;

public class Class
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8]; // Generates a unique 8-character ID
    public string Name { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public List<Hold> Holds { get; set; } = new();
}
namespace Backend.Models;

public class Calculation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Expression { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
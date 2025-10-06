namespace KasseApp.Server.Models;

public class Device
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastDailySyncAt { get; set; }
}
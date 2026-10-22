using System.Diagnostics;

namespace inzynierka.Models.Entities;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserFtpHistory> FtpHistory { get; set; } = new List<UserFtpHistory>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<DailyMetric> DailyMetrics { get; set; } = new List<DailyMetric>();
}
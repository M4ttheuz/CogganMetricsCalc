namespace inzynierka.Models.Entities;

using inzynierka.Models.Enums;
using inzynierka.Models.Entities;

public class Activity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public SportType SportType { get; set; } = SportType.Road;
    public DateTime StartTime { get; set; }
    public int DurationSeconds { get; set; }
    public int MovingTimeSeconds { get; set; }
    public decimal DistanceMeters { get; set; }
    public int TotalElevationGain { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ActivityCogganMetrics? CogganMetrics { get; set; }
    public ICollection<ActivityRecord> Records { get; set; } = new List<ActivityRecord>();
    public ICollection<ActivityPowerPeak> PowerPeaks { get; set; } = new List<ActivityPowerPeak>();
}
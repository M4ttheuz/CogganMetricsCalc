namespace inzynierka.Models.Entities;

public class ActivityRecord
{
    public long Id { get; set; }
    public int ActivityId { get; set; }
    public DateTime Timestamp { get; set; }

    public int? Power { get; set; }
    public int? HeartRate { get; set; }
    public int? Cadence { get; set; }
    public decimal? SpeedKmh { get; set; }
    public decimal? AltitudeMeters { get; set; }
    public decimal? DistanceMeters { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public Activity Activity { get; set; } = null!;
}
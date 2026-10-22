namespace inzynierka.Models.Entities;

public class ActivityRecord
{
    public DateTime Timestamp { get; set; }
    public int? Power { get; set; }
    public int? HeartRate { get; set; }
    public int? Cadence { get; set; }
    public decimal? SpeedKmh { get; set; }
    public decimal? AltitudeMeters { get; set; }
    public decimal? DistanceMeters { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
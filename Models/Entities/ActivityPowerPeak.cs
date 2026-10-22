namespace inzynierka.Models.Entities;

public class ActivityPowerPeak
{
    public int Id { get; set; }
    public int ActivityId { get; set; }
    public Activity Activity { get; set; } = null!;

    public int DurationSeconds { get; set; }
    public int Watts { get; set; }
    public decimal WattsPerKg { get; set; }
}
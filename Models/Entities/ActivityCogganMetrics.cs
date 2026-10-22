namespace inzynierka.Models.Entities;

using inzynierka.Models.Enums;

public class ActivityCogganMetrics
{
    public int ActivityId { get; set; }


    public int? NormalizedPower { get; set; }
    public decimal? IntensityFactor { get; set; }
    public decimal? TrainingStressScore { get; set; }
    public int? WorkKj { get; set; }
    public int? AvgPower { get; set; }
    public int? MaxPower { get; set; }
    public int? AvgHeartRate { get; set; }
    public int? MaxHeartRate { get; set; }
    public decimal? Decoupling { get; set; }

    public Activity Activity { get; set; } = null!;
}
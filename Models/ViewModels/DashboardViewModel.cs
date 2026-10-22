using inzynierka.Models.Entities;

namespace inzynierka.Models.ViewModels;

public class DashboardViewModel
{
    public int CurrentFtp { get; set; }
    public decimal WeightKg { get; set; }
    public decimal? FtpPerKg => WeightKg > 0 ? Math.Round(CurrentFtp / WeightKg, 2) : null;

    public decimal TodayCtl { get; set; }
    public decimal TodayAtl { get; set; }
    public decimal TodayTsb { get; set; }
    public string FormStatus { get; set; } = string.Empty;
    public string FormBadgeClass { get; set; } = string.Empty;

    public decimal WeeklyTss { get; set; }
    public decimal WeeklyDistanceKm { get; set; }
    public int WeeklyDurationSeconds { get; set; }

    public List<Activity> RecentActivities { get; set; } = new();
}
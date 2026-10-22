using inzynierka.Models.Entities;

namespace inzynierka.Models.Entities;

public class DailyMetric
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal TotalTss { get; set; }
    public decimal Ctl { get; set; }
    public decimal Atl { get; set; }
    public decimal Tsb { get; set; }

    public User User { get; set; } = null!;
}
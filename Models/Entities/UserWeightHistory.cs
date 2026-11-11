namespace inzynierka.Models.Entities;

public class UserWeightHistory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal WeightKg { get; set; }
    public DateTime EffectiveDate { get; set; }

    public User User { get; set; } = null!;
}
using inzynierka.Models.Entities;

namespace inzynierka.Models.ViewModels;

public class ProfileViewModel
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public int CurrentFtp { get; set; }
    public List<UserFtpHistory> FtpHistory { get; set; } = new();

    public int NewFtpValue { get; set; }
    public DateTime NewFtpEffectiveDate { get; set; } = DateTime.Today;
}
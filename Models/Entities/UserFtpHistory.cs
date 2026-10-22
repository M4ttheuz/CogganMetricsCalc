using inzynierka.Models.Entities;

namespace inzynierka.Models.Entities;

public class UserFtpHistory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int FtpValue { get; set; }
    public DateTime EffectiveDate { get; set; }

    public User User { get; set; } = null!;
}
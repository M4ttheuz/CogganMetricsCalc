namespace inzynierka.Services;

public interface IPmcService
{
    Task RecalculatePmcAsync(int userId);
    Task<List<PmcDailyDto>> GetPmcDataAsync(int userId, int daysBack = 90);
}

public record PmcDailyDto(
    string Date,
    decimal Tss,
    decimal Ctl,
    decimal Atl,
    decimal Tsb
);
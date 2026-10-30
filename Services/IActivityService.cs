using inzynierka.Models.Entities;
using Microsoft.AspNetCore.Http;

namespace inzynierka.Services;

public record BulkUploadResult(int SuccessCount, int SkippedCount, List<string> Messages);

public interface IActivityService
{
    Task<Activity> ProcessAndSaveFitFileAsync(Stream fileStream, string fileName, int userId, bool recalculatePmc = true);
    Task<Activity?> GetActivityDetailsAsync(int activityId);
    Task<BulkUploadResult> BulkUploadAsync(IEnumerable<(Stream fileStream, string fileName)> files, int userId);
    Task<List<Activity>> GetRecentActivitiesAsync(int userId);

}

using inzynierka.Models.Entities;
using inzynierka.Models.Enums;

namespace inzynierka.Services;

public record ParsedFitData(
    string Title,
    SportType SportType,
    DateTime StartTime,
    int DurationSeconds,
    int MovingTimeSeconds,
    decimal DistanceMeters,
    int TotalElevationGain,
    string FileHash,
    int? AvgHeartRate,
    int? MaxHeartRate,
    List<ActivityRecord> Records
);

public interface IFitParserService
{
    Task<ParsedFitData> ParseAsync(Stream fitStream, string fileName);
}
using inzynierka.Models.ViewModels;

namespace inzynierka.Services;

public record CogganCalculationResult(
    int? NormalizedPower,
    decimal? IntensityFactor,
    decimal? TrainingStressScore,
    int? WorkKj,
    int? AvgPower,
    int? MaxPower
);

public record PowerPeakDto(int DurationSeconds, string Label, int Watts, decimal WattsPerKg);

public interface ICogganCalculator
{
    CogganCalculationResult Calculate(IReadOnlyList<int?> powerRecords, int userFtp, int durationSeconds);
    PowerZonesResult CalculateZones(IReadOnlyList<int?> powerRecords, int userFtp);
    List<PowerPeakDto> CalculatePowerPeaks(IReadOnlyList<int?> powerRecords, decimal? userWeightKg);
    List<int> CalculateFullPowerCurve(IReadOnlyList<int?> powerRecords);
    PhenotypeViewModel ClassifyPhenotype(double wkg5s, double wkg1m, double wkg5m, double ftpWkg);
}
namespace inzynierka.Services;

public record ZoneSummary(string Name, string RangeDesc, int Seconds, double Percent);

public record PowerZonesResult(
    List<ZoneSummary> Zones
);
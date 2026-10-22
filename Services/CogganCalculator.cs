using inzynierka.Models.ViewModels;

namespace inzynierka.Services;

public class CogganCalculator : ICogganCalculator
{
    public CogganCalculationResult Calculate(IReadOnlyList<int?> powerRecords, int userFtp)
    {
        // Wyciągamy sekundy z poprawnym odczytem mocy (brak odczytu/pauza = 0W)
        var cleanPower = powerRecords.Select(p => p ?? 0).ToList();

        if (cleanPower.Count == 0 || cleanPower.All(p => p == 0))
        {
            return new CogganCalculationResult(null, null, null, null, null, null);
        }

        int avgPower = (int)Math.Round(cleanPower.Average());
        int maxPower = cleanPower.Max();

        // Wykonana praca: suma watów przez sekundy = dżule, dzielimy przez 1000 na kJ
        int workKj = (int)Math.Round(cleanPower.Sum() / 1000.0);

        // 1. Obliczanie średniej kroczącej 30s
        if (cleanPower.Count < 30)
        {
            // Trening za krótki na NP
            return new CogganCalculationResult(avgPower, null, null, workKj, avgPower, maxPower);
        }

        var rollingAverages30s = new List<double>();
        double currentSum = cleanPower.Take(30).Sum();
        rollingAverages30s.Add(currentSum / 30.0);

        for (int i = 30; i < cleanPower.Count; i++)
        {
            currentSum += cleanPower[i] - cleanPower[i - 30];
            rollingAverages30s.Add(currentSum / 30.0);
        }

        // 2. Podnoszenie średnich do 4. potęgi, uśrednienie i pierwiastek 4. stopnia
        double sumPow4 = rollingAverages30s.Sum(val => Math.Pow(val, 4));
        double avgPow4 = sumPow4 / rollingAverages30s.Count;
        int np = (int)Math.Round(Math.Pow(avgPow4, 0.25));

        // 3. Obliczanie IF oraz TSS (wymaga FTP > 0)
        decimal? intensityFactor = null;
        decimal? tss = null;

        if (userFtp > 0)
        {
            double ifVal = (double)np / userFtp;
            intensityFactor = Math.Round((decimal)ifVal, 3);

            // Wzór: TSS = (s * NP * IF) / (FTP * 3600) * 100
            double durationSec = cleanPower.Count;
            double tssVal = (durationSec * np * ifVal) / (userFtp * 3600.0) * 100.0;
            tss = Math.Round((decimal)tssVal, 1);
        }

        return new CogganCalculationResult(np, intensityFactor, tss, workKj, avgPower, maxPower);
    }

    public PowerZonesResult CalculateZones(IReadOnlyList<int?> powerRecords, int userFtp)
    {
        var validWatts = powerRecords.Select(p => p ?? 0).ToList();
        int totalSeconds = validWatts.Count;

        if (totalSeconds == 0 || userFtp <= 0)
        {
            return new PowerZonesResult(new List<ZoneSummary>());
        }

        int z1 = 0, z2 = 0, z3 = 0, z4 = 0, z5 = 0, z6 = 0, z7 = 0;

        int z1Limit = (int)Math.Round(userFtp * 0.55);
        int z2Limit = (int)Math.Round(userFtp * 0.75);
        int z3Limit = (int)Math.Round(userFtp * 0.90);
        int z4Limit = (int)Math.Round(userFtp * 1.05);
        int z5Limit = (int)Math.Round(userFtp * 1.20);
        int z6Limit = (int)Math.Round(userFtp * 1.50);

        foreach (var w in validWatts)
        {
            if (w < z1Limit) z1++;
            else if (w <= z2Limit) z2++;
            else if (w <= z3Limit) z3++;
            else if (w <= z4Limit) z4++;
            else if (w <= z5Limit) z5++;
            else if (w <= z6Limit) z6++;
            else z7++;
        }

        ZoneSummary CreateZone(string name, string range, int sec) =>
            new ZoneSummary(name, range, sec, Math.Round((double)sec / totalSeconds * 100.0, 1));

        var zones = new List<ZoneSummary>
    {
        CreateZone("Z1 - Aktywna regeneracja", $"< {z1Limit} W", z1),
        CreateZone("Z2 - Wytrzymałość tlenowa", $"{z1Limit + 1} - {z2Limit} W", z2),
        CreateZone("Z3 - Tempo", $"{z2Limit + 1} - {z3Limit} W", z3),
        CreateZone("Z4 - Próg FTP", $"{z3Limit + 1} - {z4Limit} W", z4),
        CreateZone("Z5 - VO2 Max", $"{z4Limit + 1} - {z5Limit} W", z5),
        CreateZone("Z6 - Beztlenowa", $"{z5Limit + 1} - {z6Limit} W", z6),
        CreateZone("Z7 - Moc neuromięśniowa", $"> {z6Limit} W", z7)
    };

        return new PowerZonesResult(zones);
    }

    public List<PowerPeakDto> CalculatePowerPeaks(IReadOnlyList<int?> powerRecords, decimal? userWeightKg)
    {
        var watts = powerRecords.Select(p => p ?? 0).ToArray();
        int n = watts.Length;

        var targetDurations = new (int Seconds, string Label)[]
        {
        (5, "5s"),
        (15, "15s"),
        (30, "30s"),
        (60, "1 min"),
        (300, "5 min"),
        (600, "10 min"),
        (1200, "20 min"),
        (3600, "60 min")
        };

        var results = new List<PowerPeakDto>();

        foreach (var (duration, label) in targetDurations)
        {
            if (n < duration) continue;

            // Inicjalizacja sumy dla pierwszego okna
            long currentWindowSum = 0;
            for (int i = 0; i < duration; i++)
            {
                currentWindowSum += watts[i];
            }

            long maxWindowSum = currentWindowSum;

            // Przesuwanie okna w O(N)
            for (int i = duration; i < n; i++)
            {
                currentWindowSum += watts[i] - watts[i - duration];
                if (currentWindowSum > maxWindowSum)
                {
                    maxWindowSum = currentWindowSum;
                }
            }

            int maxAvgPower = (int)Math.Round((double)maxWindowSum / duration);
            decimal wKg = (userWeightKg.HasValue && userWeightKg.Value > 0)
                ? Math.Round(maxAvgPower / userWeightKg.Value, 2)
                : 0m;

            results.Add(new PowerPeakDto(duration, label, maxAvgPower, wKg));
        }

        return results;
    }

    public List<int> CalculateFullPowerCurve(IReadOnlyList<int?> powerRecords)
    {
        var watts = powerRecords.Select(p => p ?? 0).ToArray();
        int n = watts.Length;
        var curve = new List<int>();

        // Dokładnie te same 13 punktów co w ActivityController
        int[] keyDurations = { 5, 15, 30, 60, 120, 300, 420, 600, 900, 1200, 1800, 2400, 3600 };

        foreach (var d in keyDurations)
        {
            if (n < d) break;

            long currentSum = 0;
            for (int i = 0; i < d; i++) currentSum += watts[i];
            long maxSum = currentSum;

            for (int i = d; i < n; i++)
            {
                currentSum += watts[i] - watts[i - d];
                if (currentSum > maxSum) maxSum = currentSum;
            }

            curve.Add((int)Math.Round((double)maxSum / d));
        }

        return curve;
    }

    public PhenotypeViewModel ClassifyPhenotype(double wkg5s, double wkg1m, double wkg5m, double ftpWkg)
    {
        // Standaryzacja punktowa (1 = Początkujący, 5 = Elita/World Class)
        // Oparta na tabelach Power Profiling Coggana dla mężczyzn
        int Score(double val, double min, double max)
        {
            if (val <= min) return 1;
            if (val >= max) return 5;
            return (int)Math.Round(1 + 4 * (val - min) / (max - min));
        }

        int sprint = Score(wkg5s, 11.0, 23.0);
        int anaerobic = Score(wkg1m, 6.0, 11.5);
        int vo2 = Score(wkg5m, 3.2, 7.0);
        int threshold = Score(ftpWkg, 2.5, 6.0);

        string name;
        string desc;
        string badge;

        // Reguły fenotypów na podstawie kształtu krzywej (profil Coggana):
        // 1. Sprinter: dominacja 5s i 1m nad FTP
        if (sprint >= threshold + 2 && sprint >= 3)
        {
            name = "Sprinter";
            desc = "Wybitna moc neuromięśniowa (5s) i beztlenowa. Twoją siłą są dynamiczne zrywy i finisze z płaskich końcówek.";
            badge = "danger";
        }
        // 2. Pursuiter / Puncheur: dominacja 1m i 5m nad progiem i sprintem
        else if (anaerobic >= threshold + 1 && vo2 >= threshold)
        {
            name = "Puncheur / Pursuiter";
            desc = "Wysoka tolerancja kwasu mlekowego i mocne VO2max (1-5 min). Idealny profil na krótkie, strome ścianki i dynamiczne ataki.";
            badge = "warning text-dark";
        }
        // 3. Time Trialist / Climber: dominacja progu tlenowego (FTP) nad sprintem
        else if (threshold >= sprint + 1 || (threshold >= 3 && sprint <= threshold))
        {
            name = "Czasowiec / Góral (TT / Climber)";
            desc = "Wysoki próg FTP i zdolność do wielominutowej, jednostajnej jazdy na wysokim procencie VO2max. Słabszy finisz z kreski, ale wysoki silnik tlenowy.";
            badge = "success";
        }
        // 4. All-Rounder: zrównoważony profil na wszystkich odcinkach
        else
        {
            name = "Wszechstronny (All-Rounder)";
            desc = "Zbalansowany profil mocy bez widocznych braków. Radzisz sobie zarówno na płaskim, krótkich pagórkach, jak i w ucieczkach.";
            badge = "primary";
        }

        return new PhenotypeViewModel
        {
            PhenotypeName = name,
            Description = desc,
            BadgeColor = badge,
            Peak5sWkg = Math.Round(wkg5s, 2),
            Peak1mWkg = Math.Round(wkg1m, 2),
            Peak5mWkg = Math.Round(wkg5m, 2),
            FtpWkg = Math.Round(ftpWkg, 2),
            SprintScore = sprint,
            AnaerobicScore = anaerobic,
            Vo2MaxScore = vo2,
            ThresholdScore = threshold
        };
    }

}
using inzynierka.Data;
using inzynierka.Models.Entities;
using Microsoft.EntityFrameworkCore;
// restored original ActivityService behavior (synchronous bulk processing)

namespace inzynierka.Services;

public class ActivityService : IActivityService
{
    private readonly AppDbContext _context;
    private readonly IFitParserService _fitParser;
    private readonly ICogganCalculator _cogganCalculator;
    private readonly IPmcService _pmcService;
    public ActivityService(
        AppDbContext context,
        IFitParserService fitParser,
        ICogganCalculator cogganCalculator,
        IPmcService pmcService)
    {
        _context = context;
        _fitParser = fitParser;
        _cogganCalculator = cogganCalculator;
        _pmcService = pmcService;
    }

    public async Task<Activity> ProcessAndSaveFitFileAsync(Stream fileStream, string fileName, int userId)
    {
        // 1. Parsowanie pliku binarnego
        var parsedData = await _fitParser.ParseAsync(fileStream, fileName);

        // 2. Walidacja unikalności (ochrona przed dublowaniem tego samego treningu)
        var isDuplicate = await _context.Activities
    .AnyAsync(a => a.UserId == userId && a.FileHash == parsedData.FileHash);

        if (isDuplicate)
        {
            throw new InvalidOperationException("Ten plik treningowy został już wcześniej przez Ciebie wgrany.");
        }

        // 3. Pobranie FTP obowiązującego w momencie wykonywania treningu
        var effectiveFtp = await _context.UserFtpHistories
            .Where(f => f.UserId == userId && f.EffectiveDate <= parsedData.StartTime)
            .OrderByDescending(f => f.EffectiveDate)
            .Select(f => f.FtpValue)
            .FirstOrDefaultAsync();

        // 4. Wyliczenie metryk Coggana (NP, IF, TSS)
        var powerStream = parsedData.Records.Select(r => r.Power).ToList();
        var cogganResult = _cogganCalculator.Calculate(powerStream, effectiveFtp);

        // Pobieramy wagę użytkownika
        var user = await _context.Users.FindAsync(userId);
        var userWeight = user?.WeightKg;

        // Wyliczenie rekordów mocy
        var calculatedPeaks = _cogganCalculator.CalculatePowerPeaks(powerStream, userWeight);

        var activity = new Activity
        {
            UserId = userId,
            Title = parsedData.Title,
            SportType = parsedData.SportType,
            StartTime = parsedData.StartTime,
            DurationSeconds = parsedData.DurationSeconds,
            MovingTimeSeconds = parsedData.MovingTimeSeconds,
            DistanceMeters = parsedData.DistanceMeters,
            TotalElevationGain = parsedData.TotalElevationGain,
            FileHash = parsedData.FileHash,
            CreatedAt = DateTime.UtcNow,
            CogganMetrics = new ActivityCogganMetrics
            {
                NormalizedPower = cogganResult.NormalizedPower,
                IntensityFactor = cogganResult.IntensityFactor,
                TrainingStressScore = cogganResult.TrainingStressScore,
                WorkKj = cogganResult.WorkKj,
                AvgPower = cogganResult.AvgPower,
                MaxPower = cogganResult.MaxPower,
                AvgHeartRate = parsedData.AvgHeartRate,
                MaxHeartRate = parsedData.MaxHeartRate
            },
            PowerPeaks = calculatedPeaks.Select(p => new ActivityPowerPeak
            {
                DurationSeconds = p.DurationSeconds,
                Watts = p.Watts,
                WattsPerKg = p.WattsPerKg
            }).ToList(),
            Records = parsedData.Records
        };

        // 6. Zapis całości w jednej transakcji
        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();
        await _pmcService.RecalculatePmcAsync(userId);

        return activity;
    }

    public async Task<Activity?> GetActivityDetailsAsync(int activityId)
    {
        return await _context.Activities
            .Include(a => a.User)
            .Include(a => a.CogganMetrics)
            .Include(a => a.PowerPeaks)
            .FirstOrDefaultAsync(a => a.Id == activityId);
    }

    public async Task<List<Activity>> GetRecentActivitiesAsync(int userId)
    {
        return await _context.Activities
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.StartTime)
            .Include(a => a.CogganMetrics)
            .ToListAsync();
    }

    public async Task<BulkUploadResult> BulkUploadAsync(IEnumerable<(Stream fileStream, string fileName)> files, int userId)
    {
        int successCount = 0;
        int skippedCount = 0;
        var messages = new List<string>();

        var user = await _context.Users.FindAsync(userId);
        var userWeight = user?.WeightKg;

        // Pobranie historii FTP użytkownika do pamięci podręcznej
        var ftpHistory = await _context.UserFtpHistories
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.EffectiveDate)
            .ToListAsync();

        var fallbackFtp = ftpHistory.LastOrDefault()?.FtpValue ?? 200;

        // Zmienna pomocnicza do kontroli duplikatów w obrębie tej paczki
        var processedHashes = new HashSet<string>();

        // Batching: grupujemy zapisy do bazy, żeby nie wywoływać SaveChangesAsync() dla każdego pliku
        const int BatchSize = 50;
        var pendingActivities = new List<(Activity activity, string fileName)>();

        foreach (var (fileStream, fileName) in files)
        {
            if (fileStream == null || fileStream.Length == 0 || !fileName.EndsWith(".fit", StringComparison.OrdinalIgnoreCase))
            {
                skippedCount++;
                messages.Add($"{fileName}: Niepoprawny plik.");
                continue;
            }

            try
            {
                if (fileStream.CanSeek)
                {
                    fileStream.Position = 0;
                }

                var parsedData = await _fitParser.ParseAsync(fileStream, fileName);

                // Sprawdzenie duplikatu dla tego użytkownika (w DB) oraz w obrębie obecnej paczki
                var isDuplicateInDb = await _context.Activities
                    .AnyAsync(a => a.UserId == userId && a.FileHash == parsedData.FileHash);

                if (isDuplicateInDb || processedHashes.Contains(parsedData.FileHash))
                {
                    skippedCount++;
                    messages.Add($"{fileName}: Plik był już wcześniej wgrany.");
                    continue;
                }

                // Wyznaczenie właściwego FTP dla daty treningu
                var effectiveFtp = ftpHistory
                    .FirstOrDefault(f => f.EffectiveDate <= parsedData.StartTime)?.FtpValue ?? fallbackFtp;

                // Obliczenia Coggana
                var powerStream = parsedData.Records.Select(r => r.Power).ToList();
                var cogganResult = _cogganCalculator.Calculate(powerStream, effectiveFtp);
                var calculatedPeaks = _cogganCalculator.CalculatePowerPeaks(powerStream, userWeight);

                var activity = new Activity
                {
                    UserId = userId,
                    Title = parsedData.Title,
                    SportType = parsedData.SportType,
                    StartTime = parsedData.StartTime,
                    DurationSeconds = parsedData.DurationSeconds,
                    MovingTimeSeconds = parsedData.MovingTimeSeconds,
                    DistanceMeters = parsedData.DistanceMeters,
                    TotalElevationGain = parsedData.TotalElevationGain,
                    FileHash = parsedData.FileHash,
                    CreatedAt = DateTime.UtcNow,
                    CogganMetrics = new ActivityCogganMetrics
                    {
                        NormalizedPower = cogganResult.NormalizedPower,
                        IntensityFactor = cogganResult.IntensityFactor,
                        TrainingStressScore = cogganResult.TrainingStressScore,
                        WorkKj = cogganResult.WorkKj,
                        AvgPower = cogganResult.AvgPower,
                        MaxPower = cogganResult.MaxPower,
                        AvgHeartRate = parsedData.AvgHeartRate,
                        MaxHeartRate = parsedData.MaxHeartRate
                    },
                    PowerPeaks = calculatedPeaks.Select(p => new ActivityPowerPeak
                    {
                        DurationSeconds = p.DurationSeconds,
                        Watts = p.Watts,
                        WattsPerKg = p.WattsPerKg
                    }).ToList(),
                    Records = parsedData.Records
                };

                // Dodajemy do listy oczekujących zapisów
                pendingActivities.Add((activity, fileName));
                processedHashes.Add(parsedData.FileHash);

                // Gdy osiągniemy wielkość batcha - zapiszemy wszystko na raz
                if (pendingActivities.Count >= BatchSize)
                {
                    try
                    {
                        _context.Activities.AddRange(pendingActivities.Select(p => p.activity));
                        await _context.SaveChangesAsync();
                        successCount += pendingActivities.Count;
                        pendingActivities.Clear();
                    }
                    catch (Exception ex)
                    {
                        // W przypadku błędu zapisu oznaczamy pliki jako pominięte i czyścimy kontekst
                        foreach (var (act, fName) in pendingActivities)
                        {
                            skippedCount++;
                            messages.Add($"{fName}: Błąd zapisu ({ex.Message}).");
                        }

                        // Odłącz wszystkie dodane encje, żeby czyścić stan kontekstu
                        foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
                        {
                            entry.State = EntityState.Detached;
                        }

                        pendingActivities.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                skippedCount++;
                messages.Add($"{fileName}: Błąd parsowania ({ex.Message}).");
            }
        }

        // Zapisz pozostałe oczekujące aktywności (jeśli są)
        if (pendingActivities.Count > 0)
        {
            try
            {
                _context.Activities.AddRange(pendingActivities.Select(p => p.activity));
                await _context.SaveChangesAsync();
                successCount += pendingActivities.Count;
            }
            catch (Exception ex)
            {
                foreach (var (act, fName) in pendingActivities)
                {
                    skippedCount++;
                    messages.Add($"{fName}: Błąd zapisu ({ex.Message}).");
                }

                foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }
        }

        if (successCount > 0)
        {
            await _pmcService.RecalculatePmcAsync(userId);
        }

        return new BulkUploadResult(successCount, skippedCount, messages);
    }
}
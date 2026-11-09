using System.Collections.Concurrent;
using inzynierka.Data;
using inzynierka.Models.Entities;
using Microsoft.EntityFrameworkCore;

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

    public async Task<Activity> ProcessAndSaveFitFileAsync(Stream fileStream, string fileName, int userId, bool recalculatePmc = true)
    {
        if (fileStream.CanSeek && fileStream.Length > FitUploadLimits.MaxFileSizeBytes)
        {
            throw new InvalidOperationException("Plik FIT może mieć maksymalnie 50 MiB.");
        }

        var parsedData = await _fitParser.ParseAsync(fileStream, fileName);

        var isDuplicate = await _context.Activities
            .AnyAsync(a => a.UserId == userId && a.FileHash == parsedData.FileHash);

        if (isDuplicate)
        {
            throw new InvalidOperationException("Ten plik treningowy został już wcześniej przez Ciebie wgrany.");
        }

        // Pobranie historii progów FTP
        var ftpHistory = await _context.UserFtpHistories
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.EffectiveDate)
            .ToListAsync();

        var fallbackFtp = ftpHistory.LastOrDefault()?.FtpValue ?? 250;
        var effectiveFtp = ftpHistory
            .FirstOrDefault(f => f.EffectiveDate <= parsedData.StartTime)?.FtpValue ?? fallbackFtp;

        if (effectiveFtp <= 0) effectiveFtp = fallbackFtp;

        // Pobranie historii masy ciała dla właściwej daty treningu
        var weightHistory = await _context.UserWeightHistories
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.EffectiveDate)
            .ToListAsync();

        var fallbackWeight = weightHistory.LastOrDefault()?.WeightKg ?? 70m;
        var effectiveWeight = weightHistory
            .FirstOrDefault(w => w.EffectiveDate <= parsedData.StartTime)?.WeightKg ?? fallbackWeight;

        if (effectiveWeight <= 0) effectiveWeight = fallbackWeight;

        var powerStream = parsedData.Records.Select(r => r.Power).ToList();
        int durationForTss = parsedData.MovingTimeSeconds > 0 ? parsedData.MovingTimeSeconds : parsedData.DurationSeconds;
        var cogganResult = _cogganCalculator.Calculate(powerStream, effectiveFtp, durationForTss);
        var calculatedPeaks = _cogganCalculator.CalculatePowerPeaks(powerStream, effectiveWeight);

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
            TelemetryBlob = TelemetryCompressor.Compress(parsedData.Records),
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
            }).ToList()
        };

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        if (recalculatePmc)
        {
            await _pmcService.RecalculatePmcAsync(userId);
        }

        return activity;
    }

    public async Task<Activity?> GetActivityDetailsAsync(int activityId)
    {
        return await _context.Activities
            .AsNoTracking()
            .Include(a => a.User)
            .Include(a => a.CogganMetrics)
            .Include(a => a.PowerPeaks)
            .FirstOrDefaultAsync(a => a.Id == activityId);
    }

    public async Task<List<Activity>> GetRecentActivitiesAsync(int userId)
    {
        return await _context.Activities
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.StartTime)
            .Include(a => a.CogganMetrics)
            .ToListAsync();
    }

    public async Task<BulkUploadResult> BulkUploadAsync(IEnumerable<(Stream fileStream, string fileName)> files, int userId)
    {
        var messages = new ConcurrentBag<string>();
        int skippedCount = 0;

        var weightHistory = await _context.UserWeightHistories
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.EffectiveDate)
            .ToListAsync();

        var fallbackWeight = weightHistory.LastOrDefault()?.WeightKg ?? 70m;

        var ftpHistory = await _context.UserFtpHistories
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.EffectiveDate)
            .ToListAsync();

        var fallbackFtp = ftpHistory.LastOrDefault()?.FtpValue ?? 250;

        var existingHashes = await _context.Activities
            .Where(a => a.UserId == userId)
            .Select(a => a.FileHash)
            .ToHashSetAsync();

        var fileList = files.ToList();
        var validFiles = fileList
            .Where(f => f.fileStream != null && f.fileStream.Length > 0 && f.fileStream.Length <= FitUploadLimits.MaxFileSizeBytes && f.fileName.EndsWith(".fit", StringComparison.OrdinalIgnoreCase))
            .ToList();

        skippedCount += fileList.Count - validFiles.Count;

        var parsedActivities = new ConcurrentBag<(Activity activity, string fileName)>();
        var batchHashes = new ConcurrentDictionary<string, byte>();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = FitUploadLimits.MaxParallelFiles
        };

        await Parallel.ForEachAsync(validFiles, parallelOptions, async (fileItem, cancellationToken) =>
        {
            var (fileStream, fileName) = fileItem;
            try
            {
                if (fileStream.CanSeek)
                {
                    fileStream.Position = 0;
                }

                var parsedData = await _fitParser.ParseAsync(fileStream, fileName);

                if (existingHashes.Contains(parsedData.FileHash) || !batchHashes.TryAdd(parsedData.FileHash, 0))
                {
                    Interlocked.Increment(ref skippedCount);
                    messages.Add($"{fileName}: Plik był już wcześniej wgrany.");
                    return;
                }

                var effectiveFtp = ftpHistory
                    .FirstOrDefault(f => f.EffectiveDate <= parsedData.StartTime)?.FtpValue ?? fallbackFtp;
                if (effectiveFtp <= 0) effectiveFtp = fallbackFtp;

                var effectiveWeight = weightHistory
                    .FirstOrDefault(w => w.EffectiveDate <= parsedData.StartTime)?.WeightKg ?? fallbackWeight;
                if (effectiveWeight <= 0) effectiveWeight = fallbackWeight;

                var powerStream = parsedData.Records.Select(r => r.Power).ToList();
                int durationForTss = parsedData.MovingTimeSeconds > 0 ? parsedData.MovingTimeSeconds : parsedData.DurationSeconds;
                var cogganResult = _cogganCalculator.Calculate(powerStream, effectiveFtp, durationForTss);
                var calculatedPeaks = _cogganCalculator.CalculatePowerPeaks(powerStream, effectiveWeight);

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
                    TelemetryBlob = TelemetryCompressor.Compress(parsedData.Records),
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
                    }).ToList()
                };

                parsedActivities.Add((activity, fileName));
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref skippedCount);
                messages.Add($"{fileName}: Błąd parsowania ({ex.Message}).");
            }
        });

        int successCount = 0;
        const int BatchSize = 50;
        var queue = parsedActivities.ToList();

        for (int i = 0; i < queue.Count; i += BatchSize)
        {
            var chunk = queue.Skip(i).Take(BatchSize).ToList();
            try
            {
                _context.Activities.AddRange(chunk.Select(c => c.activity));
                await _context.SaveChangesAsync();
                _context.ChangeTracker.Clear();
                successCount += chunk.Count;
            }
            catch (Exception ex)
            {
                foreach (var item in chunk)
                {
                    skippedCount++;
                    messages.Add($"{item.fileName}: Błąd zapisu ({ex.Message}).");
                }
                _context.ChangeTracker.Clear();
            }
        }

        if (successCount > 0)
        {
            await _pmcService.RecalculatePmcAsync(userId);
        }

        return new BulkUploadResult(successCount, skippedCount, messages.ToList());
    }

    public async Task RecalculateWeightHistoryAsync(int userId)
    {
        var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
        if (!userExists) return;

        var weightHistory = await _context.UserWeightHistories
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.EffectiveDate)
            .ToListAsync();

        var fallbackWeight = weightHistory.LastOrDefault()?.WeightKg ?? 70m;

        var activities = await _context.Activities
            .Where(a => a.UserId == userId)
            .Include(a => a.PowerPeaks)
            .ToListAsync();

        foreach (var activity in activities)
        {
            var effectiveWeight = weightHistory
                .FirstOrDefault(w => w.EffectiveDate <= activity.StartTime)?.WeightKg ?? fallbackWeight;

            if (effectiveWeight <= 0) effectiveWeight = fallbackWeight;

            foreach (var peak in activity.PowerPeaks)
            {
                peak.WattsPerKg = Math.Round((decimal)peak.Watts / effectiveWeight, 2);
            }
        }

        await _context.SaveChangesAsync();
    }
}
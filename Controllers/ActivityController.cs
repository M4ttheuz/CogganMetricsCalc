using System.Security.Claims;
using inzynierka.Data;
using inzynierka.Models.Entities;
using inzynierka.Models.ViewModels;
using inzynierka.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace inzynierka.Controllers;

[Authorize]
public class ActivityController : Controller
{
    private int CurrentUserId
    {
        get
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idClaim, out var id) ? id : 0;
        }
    }

    private readonly IActivityService _activityService;
    private readonly AppDbContext _context;
    private readonly IPmcService _pmcService;

    public ActivityController(
        IActivityService activityService,
        AppDbContext context,
        IPmcService pmcService)
    {
        _activityService = activityService;
        _context = context;
        _pmcService = pmcService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(MultipartBodyLengthLimit = 524288000)]
    [RequestSizeLimit(524288000)]
    public async Task<IActionResult> UploadBackground([FromForm] List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
        {
            return Json(new { success = false, message = "Brak plików." });
        }

        var streams = files.Select(f => (fileStream: f.OpenReadStream(), fileName: f.FileName)).ToList();
        try
        {
            await _activityService.BulkUploadAsync(streams, CurrentUserId);
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
        finally
        {
            foreach (var s in streams)
            {
                try { s.fileStream.Dispose(); } catch { }
            }
        }
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var latestMetric = await _context.DailyMetrics
            .AsNoTracking()
            .Where(d => d.UserId == CurrentUserId)
            .OrderByDescending(d => d.Date)
            .FirstOrDefaultAsync();

        decimal ctl = latestMetric?.Ctl ?? 0m;
        decimal atl = latestMetric?.Atl ?? 0m;
        decimal tsb = latestMetric?.Tsb ?? 0m;

        string formStatus;
        string badgeClass;

        if (tsb > 15) { formStatus = "Świeżość / Roztrenowanie"; badgeClass = "bg-secondary"; }
        else if (tsb >= -10) { formStatus = "Optymalna strefa formy"; badgeClass = "bg-success"; }
        else if (tsb >= -30) { formStatus = "Produktywny blok"; badgeClass = "bg-warning text-dark"; }
        else { formStatus = "Ryzyko przeciążenia"; badgeClass = "bg-danger"; }

        var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-7);
        var weekStats = await _context.Activities
            .AsNoTracking()
            .Where(a => a.UserId == CurrentUserId && a.StartTime >= sevenDaysAgo)
            .GroupBy(a => 1)
            .Select(g => new
            {
                WeeklyTss = g.Sum(a => a.CogganMetrics != null ? a.CogganMetrics.TrainingStressScore ?? 0m : 0m),
                WeeklyDistanceKm = g.Sum(a => a.DistanceMeters) / 1000m,
                WeeklyDuration = g.Sum(a => a.DurationSeconds)
            })
            .FirstOrDefaultAsync();

        // Pobieramy ostatnie aktywności z pominięciem kolumny TelemetryBlob
        var recent = await _context.Activities
            .AsNoTracking()
            .Where(a => a.UserId == CurrentUserId)
            .OrderByDescending(a => a.StartTime)
            .Take(5)
            .Select(a => new Activity
            {
                Id = a.Id,
                UserId = a.UserId,
                Title = a.Title,
                SportType = a.SportType,
                StartTime = a.StartTime,
                DurationSeconds = a.DurationSeconds,
                MovingTimeSeconds = a.MovingTimeSeconds,
                DistanceMeters = a.DistanceMeters,
                TotalElevationGain = a.TotalElevationGain,
                CogganMetrics = a.CogganMetrics
            })
            .ToListAsync();

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId);

        var currentFtp = await _context.UserFtpHistories
            .AsNoTracking()
            .Where(f => f.UserId == CurrentUserId)
            .OrderByDescending(f => f.EffectiveDate)
            .Select(f => f.FtpValue)
            .FirstOrDefaultAsync();

        var vm = new DashboardViewModel
        {
            CurrentFtp = currentFtp,
            WeightKg = user?.WeightKg ?? 0m,
            TodayCtl = ctl,
            TodayAtl = atl,
            TodayTsb = tsb,
            FormStatus = formStatus,
            FormBadgeClass = badgeClass,
            WeeklyTss = Math.Round(weekStats?.WeeklyTss ?? 0m, 1),
            WeeklyDistanceKm = Math.Round(weekStats?.WeeklyDistanceKm ?? 0m, 1),
            WeeklyDurationSeconds = weekStats?.WeeklyDuration ?? 0,
            RecentActivities = recent
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult Upload()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadSingleChunk(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return Json(new { success = false, message = "Pusty plik." });
        }

        try
        {
            using var stream = file.OpenReadStream();
            var activity = await _activityService.ProcessAndSaveFitFileAsync(stream, file.FileName, CurrentUserId);
            return Json(new { success = true, title = activity.Title });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinishBulkUpload()
    {
        await _pmcService.RecalculatePmcAsync(CurrentUserId);
        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var activity = await _activityService.GetActivityDetailsAsync(id);
        if (activity == null || activity.UserId != CurrentUserId)
        {
            return NotFound();
        }

        return View(activity);
    }

    [HttpGet]
    public async Task<IActionResult> GetPowerZones(int id, [FromServices] ICogganCalculator cogganCalculator)
    {
        var activity = await _context.Activities
            .AsNoTracking()
            .Where(a => a.Id == id && a.UserId == CurrentUserId)
            .Select(a => new { a.UserId, a.StartTime, a.TelemetryBlob })
            .FirstOrDefaultAsync();

        if (activity == null) return NotFound();

        var ftp = await _context.UserFtpHistories
            .AsNoTracking()
            .Where(f => f.UserId == activity.UserId && f.EffectiveDate <= activity.StartTime)
            .OrderByDescending(f => f.EffectiveDate)
            .Select(f => f.FtpValue)
            .FirstOrDefaultAsync();

        if (ftp <= 0)
        {
            ftp = await _context.UserFtpHistories
                .AsNoTracking()
                .Where(f => f.UserId == activity.UserId)
                .OrderBy(f => f.EffectiveDate)
                .Select(f => f.FtpValue)
                .FirstOrDefaultAsync();
        }

        if (ftp <= 0) ftp = 250;

        var records = TelemetryCompressor.Decompress(activity.TelemetryBlob);
        var powerRecords = records.Select(r => r.Power).ToList();

        var result = cogganCalculator.CalculateZones(powerRecords, ftp);
        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetPowerCurve(int id, [FromServices] ICogganCalculator cogganCalculator)
    {
        var blob = await _context.Activities
            .AsNoTracking()
            .Where(a => a.Id == id && a.UserId == CurrentUserId)
            .Select(a => a.TelemetryBlob)
            .FirstOrDefaultAsync();

        if (blob == null || blob.Length == 0) return NotFound();

        var records = TelemetryCompressor.Decompress(blob);
        var rawPower = records.Select(r => r.Power).ToList();

        if (rawPower.Count == 0) return NotFound();

        int[] durations = { 5, 15, 30, 60, 120, 300, 420, 600, 900, 1200, 1800, 2400, 3600 };
        var curveValues = cogganCalculator.CalculateFullPowerCurve(rawPower);

        var labels = new List<string>();
        for (int i = 0; i < curveValues.Count; i++)
        {
            int sec = durations[i];
            labels.Add(sec < 60 ? $"{sec}s" : $"{sec / 60}m");
        }

        return Json(new { labels, watts = curveValues });
    }

    [HttpGet]
    public async Task<IActionResult> GetTelemetry(int id)
    {
        var blob = await _context.Activities
            .AsNoTracking()
            .Where(a => a.Id == id && a.UserId == CurrentUserId)
            .Select(a => a.TelemetryBlob)
            .FirstOrDefaultAsync();

        if (blob == null || blob.Length == 0) return NotFound();

        var rawRecords = TelemetryCompressor.Decompress(blob);
        if (rawRecords.Count == 0) return NotFound();

        var startTime = rawRecords[0].Timestamp;
        int totalCount = rawRecords.Count;
        int step = Math.Max(3, (int)Math.Ceiling(totalCount / 1200.0));

        var sampledData = new List<object>((totalCount / step) + 2);

        for (int i = 0; i < totalCount; i += step)
        {
            var item = rawRecords[i];
            int elapsedSec = (int)(item.Timestamp - startTime).TotalSeconds;

            sampledData.Add(new
            {
                Time = TimeSpan.FromSeconds(elapsedSec).ToString(@"hh\:mm\:ss"),
                Power = item.Power ?? 0,
                HeartRate = item.HeartRate ?? 0,
                Altitude = (double)(item.AltitudeMeters ?? 0m),
                Lat = item.Latitude,
                Lng = item.Longitude
            });
        }

        if ((totalCount - 1) % step != 0)
        {
            var last = rawRecords[^1];
            int elapsedSec = (int)(last.Timestamp - startTime).TotalSeconds;

            sampledData.Add(new
            {
                Time = TimeSpan.FromSeconds(elapsedSec).ToString(@"hh\:mm\:ss"),
                Power = last.Power ?? 0,
                HeartRate = last.HeartRate ?? 0,
                Altitude = (double)(last.AltitudeMeters ?? 0m),
                Lat = last.Latitude,
                Lng = last.Longitude
            });
        }

        return Json(sampledData);
    }

    [HttpGet]
    public async Task<IActionResult> Phenotype([FromServices] ICogganCalculator calculator)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == CurrentUserId);

        var weight = user?.WeightKg ?? 70m;

        var currentFtp = await _context.UserFtpHistories
            .AsNoTracking()
            .Where(f => f.UserId == CurrentUserId)
            .OrderByDescending(f => f.EffectiveDate)
            .Select(f => f.FtpValue)
            .FirstOrDefaultAsync();

        if (currentFtp == 0) currentFtp = 250;

        var peaks = await _context.ActivityPowerPeaks
            .AsNoTracking()
            .Where(p => p.Activity.UserId == CurrentUserId && (p.DurationSeconds == 5 || p.DurationSeconds == 60 || p.DurationSeconds == 300))
            .GroupBy(p => p.DurationSeconds)
            .Select(g => new { Duration = g.Key, MaxWatts = g.Max(p => p.Watts) })
            .ToListAsync();

        double max5s = peaks.FirstOrDefault(p => p.Duration == 5)?.MaxWatts ?? 0;
        double max1m = peaks.FirstOrDefault(p => p.Duration == 60)?.MaxWatts ?? 0;
        double max5m = peaks.FirstOrDefault(p => p.Duration == 300)?.MaxWatts ?? 0;

        double wkg5s = weight > 0 ? max5s / (double)weight : 0;
        double wkg1m = weight > 0 ? max1m / (double)weight : 0;
        double wkg5m = weight > 0 ? max5m / (double)weight : 0;
        double ftpWkg = weight > 0 ? (double)currentFtp / (double)weight : 0;

        var vm = calculator.ClassifyPhenotype(wkg5s, wkg1m, wkg5m, ftpWkg);
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> All(int page = 1, int pageSize = 15)
    {
        if (page < 1) page = 1;

        var query = _context.Activities
            .AsNoTracking()
            .Where(a => a.UserId == CurrentUserId)
            .OrderByDescending(a => a.StartTime)
            .Select(a => new Activity
            {
                Id = a.Id,
                UserId = a.UserId,
                Title = a.Title,
                SportType = a.SportType,
                StartTime = a.StartTime,
                DurationSeconds = a.DurationSeconds,
                MovingTimeSeconds = a.MovingTimeSeconds,
                DistanceMeters = a.DistanceMeters,
                TotalElevationGain = a.TotalElevationGain,
                CogganMetrics = a.CogganMetrics
            });

        int totalItems = await query.CountAsync();
        var activities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.TotalCount = totalItems;

        return View(activities);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == CurrentUserId);

        if (activity == null)
        {
            return NotFound();
        }

        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();

        await _pmcService.RecalculatePmcAsync(CurrentUserId);

        TempData["Success"] = "Trening został pomyślnie usunięty.";
        return RedirectToAction(nameof(All));
    }
}
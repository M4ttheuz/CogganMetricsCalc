using Dynastream.Fit;
using inzynierka.Data;
using inzynierka.Models.Entities;
using inzynierka.Models.ViewModels;
using inzynierka.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using DateTime = System.DateTime;

namespace inzynierka.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly AppDbContext _context;
    private readonly IPmcService _pmcService;
    private readonly IActivityService _activityService;

    private int CurrentUserId
    {
        get
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : 0;
        }
    }

    public ProfileController(
        AppDbContext context,
        IPmcService pmcService,
        IActivityService activityService)
    {
        _context = context;
        _pmcService = pmcService;
        _activityService = activityService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return NotFound();

        var ftpHistory = await _context.UserFtpHistories
            .Where(f => f.UserId == CurrentUserId)
            .OrderByDescending(f => f.EffectiveDate)
            .ToListAsync();

        var weightHistory = await _context.UserWeightHistories
            .Where(w => w.UserId == CurrentUserId)
            .OrderByDescending(w => w.EffectiveDate)
            .ToListAsync();

        int currentFtp = ftpHistory.FirstOrDefault()?.FtpValue ?? 0;
        decimal currentWeight = weightHistory.FirstOrDefault()?.WeightKg ?? 70m;

        var model = new ProfileViewModel
        {
            UserId = user.Id,
            Email = user.Email,
            WeightKg = currentWeight,
            CurrentFtp = currentFtp,
            FtpHistory = ftpHistory,
            WeightHistory = weightHistory,
            NewFtpValue = currentFtp > 0 ? currentFtp : 250,
            NewFtpEffectiveDate = DateTime.Today
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddWeight(decimal weightKg, DateTime effectiveDate)
    {
        var userId = CurrentUserId;
        if (userId == 0) return RedirectToAction("Login", "Account");

        if (weightKg < 35 || weightKg > 200)
        {
            TempData["Error"] = "Waga musi mieścić się w przedziale 35 - 200 kg.";
            return RedirectToAction(nameof(Index));
        }

        if (effectiveDate.Date > DateTime.Today)
        {
            TempData["Error"] = "Data pomiaru masy ciała nie może być z przyszłości.";
            return RedirectToAction(nameof(Index));
        }

        var entry = new UserWeightHistory
        {
            UserId = userId,
            WeightKg = Math.Round(weightKg, 1),
            EffectiveDate = effectiveDate.Date
        };

        _context.UserWeightHistories.Add(entry);
        await _context.SaveChangesAsync();

        await _activityService.RecalculateWeightHistoryAsync(userId);

        TempData["Success"] = "Zapisano pomiar masy ciała i zaktualizowano szczyty mocy.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteWeight(int id)
    {
        var userId = CurrentUserId;
        if (userId == 0) return RedirectToAction("Login", "Account");

        var count = await _context.UserWeightHistories.CountAsync(w => w.UserId == userId);
        if (count <= 1)
        {
            TempData["Error"] = "Nie można usunąć ostatniego wpisu masy ciała. W profilu musi pozostać przynajmniej jedna wartość.";
            return RedirectToAction(nameof(Index));
        }

        var entry = await _context.UserWeightHistories.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);
        if (entry != null)
        {
            _context.UserWeightHistories.Remove(entry);
            await _context.SaveChangesAsync();

            await _activityService.RecalculateWeightHistoryAsync(userId);
            TempData["Success"] = "Pomyślnie usunięto wpis masy ciała.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFtp(int ftpValue, DateTime effectiveDate)
    {
        if (ftpValue < 50 || ftpValue > 600)
        {
            TempData["Error"] = "Wartość FTP musi być w przedziale 50 - 600 W.";
            return RedirectToAction(nameof(Index));
        }

        if (effectiveDate.Date > DateTime.Today)
        {
            TempData["Error"] = "Data obowiązywania progu FTP nie może być z przyszłości.";
            return RedirectToAction(nameof(Index));
        }

        var entry = new UserFtpHistory
        {
            UserId = CurrentUserId,
            FtpValue = ftpValue,
            EffectiveDate = effectiveDate.Date
        };

        _context.UserFtpHistories.Add(entry);
        await _context.SaveChangesAsync();

        await FastRecalculateTrainingStressAsync(CurrentUserId);

        TempData["Success"] = "Nowy próg FTP zapisany. Wszystkie metryki TSS i krzywe PMC zostały zaktualizowane!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFtp(int id)
    {
        var count = await _context.UserFtpHistories.CountAsync(f => f.UserId == CurrentUserId);
        if (count <= 1)
        {
            TempData["Error"] = "Nie możesz usunąć jedynego wpisu FTP. Zawsze musi istnieć co najmniej jedna wartość progowa.";
            return RedirectToAction(nameof(Index));
        }

        var entry = await _context.UserFtpHistories.FirstOrDefaultAsync(f => f.Id == id && f.UserId == CurrentUserId);
        if (entry != null)
        {
            _context.UserFtpHistories.Remove(entry);
            await _context.SaveChangesAsync();

            await FastRecalculateTrainingStressAsync(CurrentUserId);
            TempData["Success"] = "Wpis FTP usunięty. Zaktualizowano wykresy obciążenia.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task FastRecalculateTrainingStressAsync(int userId)
    {
        var ftpEntries = await _context.UserFtpHistories
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.EffectiveDate)
            .ToListAsync();

        var fallbackFtp = ftpEntries.LastOrDefault()?.FtpValue ?? 250;

        var activities = await _context.Activities
            .Include(a => a.CogganMetrics)
            .Where(a => a.UserId == userId)
            .ToListAsync();

        foreach (var act in activities)
        {
            if (act.CogganMetrics?.NormalizedPower == null || act.CogganMetrics.NormalizedPower <= 0)
                continue;

            var effectiveFtp = ftpEntries
                .FirstOrDefault(f => f.EffectiveDate.Date <= act.StartTime.Date)?.FtpValue ?? fallbackFtp;

            if (effectiveFtp <= 0) effectiveFtp = fallbackFtp;

            int np = act.CogganMetrics.NormalizedPower.Value;
            decimal ifFactor = Math.Round((decimal)np / effectiveFtp, 3);

            int durationSec = act.MovingTimeSeconds > 0 ? act.MovingTimeSeconds : act.DurationSeconds;
            decimal tss = (durationSec * np * ifFactor) / (effectiveFtp * 3600m) * 100m;

            act.CogganMetrics.IntensityFactor = ifFactor;
            act.CogganMetrics.TrainingStressScore = Math.Round(tss, 1);
        }

        await _context.SaveChangesAsync();
        await _pmcService.RecalculatePmcAsync(userId);
    }
}
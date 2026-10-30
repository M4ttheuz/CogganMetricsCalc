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
public class ProfileController : Controller
{
    private readonly AppDbContext _context;
    private readonly IPmcService _pmcService;

    private int CurrentUserId
    {
        get
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : 0;
        }
    }

    public ProfileController(AppDbContext context, IPmcService pmcService)
    {
        _context = context;
        _pmcService = pmcService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user == null) return NotFound();

        var history = await _context.UserFtpHistories
            .Where(f => f.UserId == CurrentUserId)
            .OrderByDescending(f => f.EffectiveDate)
            .ToListAsync();

        int currentFtp = history.FirstOrDefault()?.FtpValue ?? 0;

        var model = new ProfileViewModel
        {
            UserId = user.Id,
            Email = user.Email,
            WeightKg = user.WeightKg,
            CurrentFtp = currentFtp,
            FtpHistory = history,
            NewFtpValue = currentFtp > 0 ? currentFtp : 250,
            NewFtpEffectiveDate = DateTime.Today
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateWeight(decimal weightKg)
    {
        if (weightKg <= 30 || weightKg >= 250)
        {
            TempData["Error"] = "Podaj realistyczną wagę w kg (30 - 250).";
            return RedirectToAction(nameof(Index));
        }

        var user = await _context.Users.FindAsync(CurrentUserId);
        if (user != null)
        {
            user.WeightKg = Math.Round(weightKg, 1);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Waga zaktualizowana pomyślnie.";
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
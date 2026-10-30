using inzynierka.Data;
using inzynierka.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace inzynierka.Services;

public class PmcService : IPmcService
{
    private readonly AppDbContext _context;

    private const double CtlTimeConstant = 42.0;
    private const double AtlTimeConstant = 7.0;

    public PmcService(AppDbContext context)
    {
        _context = context;
    }

    public async Task RecalculatePmcAsync(int userId)
    {
        var activities = await _context.Activities
            .Where(a => a.UserId == userId && a.CogganMetrics != null && a.CogganMetrics.TrainingStressScore != null)
            .Select(a => new
            {
                Date = DateOnly.FromDateTime(a.StartTime),
                Tss = a.CogganMetrics!.TrainingStressScore!.Value
            })
            .ToListAsync();

        var existingMetrics = await _context.DailyMetrics
            .Where(d => d.UserId == userId)
            .ToListAsync();
        _context.DailyMetrics.RemoveRange(existingMetrics);

        if (!activities.Any())
        {
            await _context.SaveChangesAsync();
            return;
        }

        var dailyTssMap = activities
            .GroupBy(a => a.Date)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Tss));

        var startDate = activities.Min(a => a.Date);
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        double currentCtl = 0.0;
        double currentAtl = 0.0;

        double ctlFactor = 1.0 - Math.Exp(-1.0 / CtlTimeConstant);
        double atlFactor = 1.0 - Math.Exp(-1.0 / AtlTimeConstant);

        var newMetrics = new List<DailyMetric>();

        for (var d = startDate; d <= endDate; d = d.AddDays(1))
        {
            decimal dayTss = dailyTssMap.TryGetValue(d, out var tss) ? tss : 0m;

            currentCtl += ((double)dayTss - currentCtl) * ctlFactor;
            currentAtl += ((double)dayTss - currentAtl) * atlFactor;
            double currentTsb = currentCtl - currentAtl;

            newMetrics.Add(new DailyMetric
            {
                UserId = userId,
                Date = d,
                TotalTss = dayTss,
                Ctl = Math.Round((decimal)currentCtl, 2),
                Atl = Math.Round((decimal)currentAtl, 2),
                Tsb = Math.Round((decimal)currentTsb, 2)
            });
        }

        await _context.DailyMetrics.AddRangeAsync(newMetrics);
        await _context.SaveChangesAsync();
    }

    public async Task<List<PmcDailyDto>> GetPmcDataAsync(int userId, int daysBack = 90)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-daysBack));

        return await _context.DailyMetrics
            .Where(d => d.UserId == userId && d.Date >= cutoff)
            .OrderBy(d => d.Date)
            .Select(d => new PmcDailyDto(
                d.Date.ToString("yyyy-MM-dd"),
                d.TotalTss,
                d.Ctl,
                d.Atl,
                d.Tsb
            ))
            .ToListAsync();
    }
}
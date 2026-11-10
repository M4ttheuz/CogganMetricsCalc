using FluentAssertions;
using inzynierka.Data;
using inzynierka.Models.Entities;
using inzynierka.Models.Enums;
using inzynierka.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace inzynierka.Tests;

public class PmcServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly PmcService _service;

    public PmcServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=False;");
        _connection.Open();

        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = OFF;";
            command.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();

        _service = new PmcService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RecalculatePmcAsync_SingleHardWorkout_CalculatesExponentialFatigueAndNegativeForm()
    {
        // Arrange: 1 mocny trening (TSS = 120) wykonany 2 dni temu
        int userId = 1;
        var workoutDate = DateTime.UtcNow.AddDays(-2);

        var activity = new Activity
        {
            UserId = userId,
            Title = "Interwały VO2Max",
            SportType = SportType.Road,
            StartTime = workoutDate,
            FileHash = "hash123",
            DurationSeconds = 3600,
            MovingTimeSeconds = 3600,
            DistanceMeters = 35000m,
            TotalElevationGain = 450,
            CogganMetrics = new ActivityCogganMetrics
            {
                TrainingStressScore = 120m,
                AvgPower = 240,
                NormalizedPower = 280
            }
        };

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        // Act
        await _service.RecalculatePmcAsync(userId);

        // Assert: Powinny powstać wpisy od dnia treningu do dzisiaj
        var metrics = await _context.DailyMetrics
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Date)
            .ToListAsync();

        metrics.Should().NotBeEmpty();

        // Dzień 1 (dzień treningu):
        var dayOfWorkout = metrics.First();
        dayOfWorkout.TotalTss.Should().Be(120m);
        dayOfWorkout.Atl.Should().BeGreaterThan(dayOfWorkout.Ctl, "Zmęczenie (7 dni) musi wzrosnąć szybciej niż fitness (42 dni)");
        dayOfWorkout.Tsb.Should().Be(dayOfWorkout.Ctl - dayOfWorkout.Atl, "TSB musi być równe CTL - ATL");
        dayOfWorkout.Tsb.Should().BeNegative("Po ciężkim treningu wskaźnik formy musi spaść poniżej zera");

        // Ostatni dzień (dzisiaj, po 2 dniach odpoczynku TSS = 0):
        var today = metrics.Last();
        today.TotalTss.Should().Be(0m);
        today.Atl.Should().BeLessThan(dayOfWorkout.Atl, "Zmęczenie musi opadać w dniach wolnych");
        today.Tsb.Should().BeGreaterThan(dayOfWorkout.Tsb, "Forma (świeżość) musi rosnąć wraz z odpoczynkiem");
    }

    [Fact]
    public async Task RecalculatePmcAsync_MultipleActivitiesOnSameDay_SumsTssCorrectly()
    {
        // Arrange: Dwa treningi tego samego dnia (np. dojazd + wyścig)
        int userId = 2;
        var date = DateTime.UtcNow.AddDays(-1);

        _context.Activities.AddRange(
            new Activity
            {
                UserId = userId,
                Title = "Rozgrzewka",
                SportType = SportType.Road,
                StartTime = date.AddHours(9),
                FileHash = "h1",
                DurationSeconds = 1800,
                MovingTimeSeconds = 1800,
                DistanceMeters = 15000m,
                TotalElevationGain = 100,
                CogganMetrics = new ActivityCogganMetrics { TrainingStressScore = 30m }
            },
            new Activity
            {
                UserId = userId,
                Title = "Wyścig",
                SportType = SportType.Road,
                StartTime = date.AddHours(14),
                FileHash = "h2",
                DurationSeconds = 5400,
                MovingTimeSeconds = 5400,
                DistanceMeters = 60000m,
                TotalElevationGain = 800,
                CogganMetrics = new ActivityCogganMetrics { TrainingStressScore = 120m }
            }
        );
        await _context.SaveChangesAsync();

        // Act
        await _service.RecalculatePmcAsync(userId);

        // Assert
        var workoutDayMetric = await _context.DailyMetrics
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Date == DateOnly.FromDateTime(date));

        workoutDayMetric.Should().NotBeNull();
        workoutDayMetric!.TotalTss.Should().Be(150m, "TSS z dwóch aktywności w ciągu jednego dnia musi zostać zsumowany");
    }

    [Fact]
    public async Task RecalculatePmcAsync_NoActivities_ClearsExistingMetrics()
    {
        // Arrange: Użytkownik ma stare metryki, ale usunął wszystkie aktywności
        int userId = 3;
        _context.DailyMetrics.Add(new DailyMetric
        {
            UserId = userId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            TotalTss = 50m,
            Ctl = 40m,
            Atl = 50m,
            Tsb = -10m
        });
        await _context.SaveChangesAsync();

        // Act
        await _service.RecalculatePmcAsync(userId);

        // Assert
        var remainingMetrics = await _context.DailyMetrics
            .Where(m => m.UserId == userId)
            .ToListAsync();

        remainingMetrics.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPmcDataAsync_FiltersByCutoffDate()
    {
        // Arrange: Metryki z wczoraj i sprzed 100 dni
        int userId = 4;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        _context.DailyMetrics.AddRange(
            new DailyMetric { UserId = userId, Date = today.AddDays(-100), TotalTss = 80m, Ctl = 20m, Atl = 30m, Tsb = -10m },
            new DailyMetric { UserId = userId, Date = today.AddDays(-10), TotalTss = 60m, Ctl = 25m, Atl = 28m, Tsb = -3m },
            new DailyMetric { UserId = userId, Date = today, TotalTss = 0m, Ctl = 24m, Atl = 24m, Tsb = 0m }
        );
        await _context.SaveChangesAsync();

        // Act: Domyślne pobranie ostatnich 90 dni
        var result = await _service.GetPmcDataAsync(userId, daysBack: 90);

        // Assert
        result.Should().HaveCount(2);
        result.Should().NotContain(d => d.Date == today.AddDays(-100).ToString("yyyy-MM-dd"));
    }
}
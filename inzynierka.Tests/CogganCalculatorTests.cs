using FluentAssertions;
using inzynierka.Services;
using Xunit;

namespace inzynierka.Tests;

public class CogganCalculatorTests
{
    private readonly CogganCalculator _calculator = new();

    [Fact]
    public void Calculate_OneHourAtFtp_NormalizedPowerEqualsAvgPowerAndTssEquals100()
    {
        // Arrange: 1 godzina (3600 s) ze stałą mocą 250 W przy FTP 250 W
        IReadOnlyList<int?> powerStream = Enumerable.Repeat((int?)250, 3600).ToList();
        int ftp = 250;
        int durationSeconds = 3600;

        // Act
        var result = _calculator.Calculate(powerStream, ftp, durationSeconds);

        // Assert: 1h na FTP daje dokładnie IF = 1.0 i TSS = 100
        result.AvgPower.Should().Be(250);
        result.NormalizedPower.Should().Be(250);
        result.IntensityFactor.Should().Be(1.0m);
        result.TrainingStressScore.Should().Be(100.0m);
        result.WorkKj.Should().Be(900);
    }

    [Fact]
    public void Calculate_EmptyStream_ReturnsNullOrZeroMetrics()
    {
        // Arrange
        IReadOnlyList<int?> powerStream = new List<int?>();

        // Act
        var result = _calculator.Calculate(powerStream, 250, 0);

        // Assert: Brak danych mocy powinien skutkować null w NP i AvgPower oraz 0 w TSS
        result.AvgPower.Should().BeNull();
        result.NormalizedPower.Should().BeNull();
        result.IntensityFactor.Should().BeNull();
        result.TrainingStressScore.Should().BeNull();
        result.WorkKj.Should().BeNull();
    }

    [Fact]
    public void Calculate_ShortIntervals_NormalizedPowerShouldBeHigherThanAveragePower()
    {
        // Arrange: 30 minut interwałów (30s 400 W / 30s 100 W)
        var streamList = new List<int?>();
        for (int i = 0; i < 30; i++)
        {
            streamList.AddRange(Enumerable.Repeat((int?)400, 30));
            streamList.AddRange(Enumerable.Repeat((int?)100, 30));
        }
        IReadOnlyList<int?> powerStream = streamList;

        // Act
        var result = _calculator.Calculate(powerStream, 250, 1800);

        // Assert: NP musi być wyższe niż średnia moc (250 W)
        result.AvgPower.Should().Be(250);
        result.NormalizedPower.Should().BeGreaterThan(result.AvgPower.GetValueOrDefault());
    }

    [Fact]
    public void CalculatePowerPeaks_FindsCorrectPeaksAndWattsPerKg()
    {
        // Arrange: 5 s sprintu 600 W, reszta 200 W
        IReadOnlyList<int?> powerStream = Enumerable.Repeat((int?)600, 5)
            .Concat(Enumerable.Repeat((int?)200, 55))
            .ToList();
        decimal athleteWeight = 75m;

        // Act
        var peaks = _calculator.CalculatePowerPeaks(powerStream, athleteWeight);

        // Assert
        var peak5s = peaks.FirstOrDefault(p => p.DurationSeconds == 5);
        peak5s.Should().NotBeNull();
        peak5s!.Watts.Should().Be(600);
        peak5s.WattsPerKg.Should().Be(8.0m);

        var peak60s = peaks.FirstOrDefault(p => p.DurationSeconds == 60);
        peak60s.Should().NotBeNull();
        peak60s!.Watts.Should().Be(233);
        peak60s.WattsPerKg.Should().Be(3.11m);
    }

    [Fact]
    public void Calculate_SimulatedSmartRecording_InterpolatesEnergyCorrectly()
    {
        // Arrange: Trening 300 sekund na stałej mocy 200 W.
        // Prawdziwa wykonana praca to: 300 s * 200 W = 60 000 J = 60 kJ.
        //
        // Symulacja Smart Recordingu: Garmin zapisał tylko 60 punktów (co 5 sekund),
        // zamiast pełnych 300 próbek sekundowych:
        var rawSparseStream = Enumerable.Repeat((int?)200, 60).ToList();

        // Act
        // 1. Gdybyśmy zsumowali bez wiedzy o czasie: 60 * 200 / 1000 = 12 kJ (błąd!)
        // 2. Twój kalkulator uwzględniający durationSeconds (300 s):
        var result = _calculator.Calculate(rawSparseStream, userFtp: 250, durationSeconds: 300);

        // Assert
        result.WorkKj.Should().Be(60); // 200 W * 300 s / 1000 = 60 kJ
    }

    [Fact]
    public void ClassifyPhenotype_DominantSprint_ClassifiedAsSprinter()
    {
        // Arrange: Wybitna moc 5s (22 W/kg -> Score 5) przy przeciętnym progu FTP (3.0 W/kg -> Score 2)
        double wkg5s = 22.0;
        double wkg1m = 7.5;
        double wkg5m = 3.8;
        double ftpWkg = 3.0;

        // Act
        var result = _calculator.ClassifyPhenotype(wkg5s, wkg1m, wkg5m, ftpWkg);

        // Assert
        result.PhenotypeName.Should().Be("Sprinter");
        result.BadgeColor.Should().Be("danger");
        result.SprintScore.Should().Be(5);
    }

    [Fact]
    public void ClassifyPhenotype_DominantThreshold_ClassifiedAsClimberOrTimeTrialist()
    {
        // Arrange: Bardzo wysoki próg FTP (5.2 W/kg -> Score 4) przy niskim sprincie (12.0 W/kg -> Score 1)
        double wkg5s = 12.0;
        double wkg1m = 7.0;
        double wkg5m = 5.0;
        double ftpWkg = 5.2;

        // Act
        var result = _calculator.ClassifyPhenotype(wkg5s, wkg1m, wkg5m, ftpWkg);

        // Assert
        result.PhenotypeName.Should().Be("Czasowiec / Góral (TT / Climber)");
        result.BadgeColor.Should().Be("success");
    }

    [Fact]
    public void ClassifyPhenotype_StrongOneAndFiveMinutePower_ClassifiedAsPuncheur()
    {
        // Arrange: Wybitne 1 min (Score 4) i 5 min (Score 3) z umiarkowanym progiem (Score 2) i sprintem (Score 2)
        double wkg5s = 13.0;
        double wkg1m = 9.8;
        double wkg5m = 5.0;
        double ftpWkg = 3.2;

        // Act
        var result = _calculator.ClassifyPhenotype(wkg5s, wkg1m, wkg5m, ftpWkg);

        // Assert
        result.PhenotypeName.Should().Be("Puncheur / Pursuiter");
        result.BadgeColor.Should().Be("warning text-dark");
    }

    [Fact]
    public void ClassifyPhenotype_BalancedProfile_ClassifiedAsAllRounder()
    {
        // Arrange: Zbalansowane parametry bez skrajnych dysproporcji
        double wkg5s = 15.0;
        double wkg1m = 7.5;
        double wkg5m = 4.0;
        double ftpWkg = 3.2;

        // Act
        var result = _calculator.ClassifyPhenotype(wkg5s, wkg1m, wkg5m, ftpWkg);

        // Assert
        result.PhenotypeName.Should().Be("Wszechstronny (All-Rounder)");
        result.BadgeColor.Should().Be("primary");
    }

    [Fact]
    public void Calculate_UserFtpIsZero_DoesNotThrowDivideByZeroAndReturnsNullTssAndIf()
    {
        // Arrange: Użytkownik dopiero założył konto i nie wpisał jeszcze FTP (wartość 0)
        IReadOnlyList<int?> powerStream = Enumerable.Repeat((int?)200, 1200).ToList();

        // Act
        var result = _calculator.Calculate(powerStream, userFtp: 0, durationSeconds: 1200);

        // Assert: Brak błędu dzielenia przez zero; NP i moc wyliczone, a IF i TSS to null
        result.NormalizedPower.Should().Be(200);
        result.IntensityFactor.Should().BeNull();
        result.TrainingStressScore.Should().BeNull();
    }
}
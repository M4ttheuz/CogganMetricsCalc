using FluentAssertions;
using inzynierka.Models.Entities;
using inzynierka.Services;
using Xunit;

namespace inzynierka.Tests;

public class TelemetryCompressorTests
{
    [Fact]
    public void CompressAndDecompress_ValidRecords_RestoresExactSameData()
    {
        // Arrange: Kilka sekund telemetrii z pełnym zestawem metryk i wartościami null
        var baseTime = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
        var originalRecords = new List<ActivityRecord>
        {
            new()
            {
                Timestamp = baseTime,
                Power = 250,
                HeartRate = 145,
                Cadence = 90,
                SpeedKmh = 32.5m,
                AltitudeMeters = 350.2m,
                DistanceMeters = 0.0m,
                Latitude = 49.8223,
                Longitude = 19.0441
            },
            new()
            {
                // Rekord z wartościami null (np. chwila bez kadencji / tętna)
                Timestamp = baseTime.AddSeconds(1),
                Power = 0,
                HeartRate = null,
                Cadence = null,
                SpeedKmh = 33.1m,
                AltitudeMeters = 350.5m,
                DistanceMeters = 9.2m,
                Latitude = 49.8224,
                Longitude = 19.0442
            },
            new()
            {
                Timestamp = baseTime.AddSeconds(2),
                Power = 420,
                HeartRate = 168,
                Cadence = 98,
                SpeedKmh = 35.0m,
                AltitudeMeters = 351.0m,
                DistanceMeters = 19.0m,
                Latitude = 49.8226,
                Longitude = 19.0444
            }
        };

        // Act: Kompresja do bloba bajtów i ponowna dekompresja
        byte[] compressedBytes = TelemetryCompressor.Compress(originalRecords);
        var decompressedRecords = TelemetryCompressor.Decompress(compressedBytes);

        // Assert: Dane po dekompresji muszą być identyczne z oryginałem
        compressedBytes.Should().NotBeNullOrEmpty();
        decompressedRecords.Should().HaveCount(originalRecords.Count);
        decompressedRecords.Should().BeEquivalentTo(originalRecords, options => options.WithStrictOrdering());
    }

    [Fact]
    public void CompressAndDecompress_EmptyList_ReturnsEmptyList()
    {
        // Arrange
        var emptyList = new List<ActivityRecord>();

        // Act
        byte[] compressedBytes = TelemetryCompressor.Compress(emptyList);
        var decompressedRecords = TelemetryCompressor.Decompress(compressedBytes);

        // Assert
        decompressedRecords.Should().BeEmpty();
    }

    [Fact]
    public void Compress_LargeStream_SignificantlyReducesSize()
    {
        // Arrange: 3600 rekordów (1 godzina jazdy)
        var baseTime = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var records = new List<ActivityRecord>();

        for (int i = 0; i < 3600; i++)
        {
            records.Add(new ActivityRecord
            {
                Timestamp = baseTime.AddSeconds(i),
                Power = 200 + (i % 50),
                HeartRate = 140 + (i % 20),
                Cadence = 88,
                SpeedKmh = 30.0m,
                AltitudeMeters = 400.0m + i * 0.1m,
                DistanceMeters = i * 8.3m,
                Latitude = 49.8000 + i * 0.00001,
                Longitude = 19.0000 + i * 0.00001
            });
        }

        // Act
        byte[] compressedBytes = TelemetryCompressor.Compress(records);

        // Szacunkowy rozmiar JSON-a w pamięci dla 3600 rekordów to ~700-900 KB
        // Dobrze skompresowany strumień Brotli dla telemetrii powinien ważyć < 100 KB
        compressedBytes.Length.Should().BeLessThan(100 * 1024);
    }
}
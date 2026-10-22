using System.Security.Cryptography;
using Dynastream.Fit;
using inzynierka.Models.Entities;
using inzynierka.Models.Enums;
using DateTime = System.DateTime;

namespace inzynierka.Services;

public class FitParserService : IFitParserService
{
    public async Task<ParsedFitData> ParseAsync(Stream fitStream, string fileName)
    {
        byte[] fileBytes;
        using (var memoryStream = new MemoryStream())
        {
            await fitStream.CopyToAsync(memoryStream);
            fileBytes = memoryStream.ToArray();
        }

        string fileHash;
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(fileBytes);
            fileHash = Convert.ToHexString(hashBytes);
        }

        var records = new List<ActivityRecord>();
        var startTime = DateTime.UtcNow;
        int totalTimerTime = 0;
        decimal totalDistance = 0;
        int totalAscent = 0;
        int? sessionAvgHr = null;
        int? sessionMaxHr = null;
        var detectedSport = SportType.Road;
        bool isCyclingSession = false;
        string rawSportName = "Nieznany";

        using var decodeStream = new MemoryStream(fileBytes);
        var decode = new Decode();
        var mesgBroadcaster = new MesgBroadcaster();
        decode.MesgEvent += mesgBroadcaster.OnMesg;

        // 1. Obsługa 1-sekundowych rekordów (RecordMesg)
        mesgBroadcaster.RecordMesgEvent += (sender, e) =>
        {
            var recordMesg = new RecordMesg(e.mesg);

            var fitTime = recordMesg.GetTimestamp();
            if (fitTime == null) return;

            var timestamp = fitTime.GetDateTime();

            double? lat = null;
            double? lng = null;
            var rawLat = recordMesg.GetPositionLat();
            var rawLng = recordMesg.GetPositionLong();

            if (rawLat.HasValue && rawLng.HasValue)
            {
                lat = rawLat.Value * (180.0 / 2147483648.0);
                lng = rawLng.Value * (180.0 / 2147483648.0);
            }

            decimal? speedKmh = null;
            var speedMs = recordMesg.GetSpeed();
            if (speedMs.HasValue)
            {
                speedKmh = Math.Round((decimal)(speedMs.Value * 3.6f), 2);
            }

            var record = new ActivityRecord
            {
                Timestamp = timestamp,
                Power = recordMesg.GetPower(),
                HeartRate = recordMesg.GetHeartRate(),
                Cadence = recordMesg.GetCadence(),
                SpeedKmh = speedKmh,
                AltitudeMeters = recordMesg.GetAltitude().HasValue ? (decimal?)Math.Round((decimal)recordMesg.GetAltitude()!.Value, 1) : null,
                DistanceMeters = recordMesg.GetDistance().HasValue ? (decimal?)Math.Round((decimal)recordMesg.GetDistance()!.Value, 1) : null,
                Latitude = lat,
                Longitude = lng
            };

            records.Add(record);
        };

        // 2. Obsługa podsumowania sesji (SessionMesg)
        mesgBroadcaster.SessionMesgEvent += (sender, e) =>
        {
            var sessionMesg = new SessionMesg(e.mesg);

            var sessionStart = sessionMesg.GetStartTime();
            if (sessionStart != null)
            {
                startTime = sessionStart.GetDateTime();
            }

            if (sessionMesg.GetTotalTimerTime().HasValue)
            {
                totalTimerTime = (int)Math.Round(sessionMesg.GetTotalTimerTime()!.Value);
            }

            if (sessionMesg.GetTotalDistance().HasValue)
            {
                totalDistance = (decimal)Math.Round(sessionMesg.GetTotalDistance()!.Value, 2);
            }

            if (sessionMesg.GetTotalAscent().HasValue)
            {
                totalAscent = (int)sessionMesg.GetTotalAscent()!.Value;
            }

            sessionAvgHr = sessionMesg.GetAvgHeartRate();
            sessionMaxHr = sessionMesg.GetMaxHeartRate();

            var fitSport = sessionMesg.GetSport();
            var fitSubSport = sessionMesg.GetSubSport();

            rawSportName = fitSport.ToString();

            if (fitSport == Sport.Cycling)
            {
                isCyclingSession = true;
                detectedSport = fitSubSport switch
                {
                    SubSport.Mountain => SportType.Mtb,
                    SubSport.GravelCycling => SportType.Gravel,
                    SubSport.IndoorCycling => SportType.Trainer,
                    _ => SportType.Road
                };
            }
        };

        // 3. Odczyt strumienia FIT
        decode.Read(decodeStream);

        // Fallback: jeśli nagłówek sesji nie miał ustawionego Sport.Cycling, ale rekordy mają moc (power meter)
        if (!isCyclingSession && records.Any(r => r.Power.HasValue && r.Power.Value > 0))
        {
            isCyclingSession = true;
            detectedSport = SportType.Road;
        }

        // 4. Blokada innych dyscyplin (bieganie, spacer, pływanie, siłownia)
        if (!isCyclingSession)
        {
            throw new InvalidOperationException($"Plik nie zawiera aktywności kolarskiej (wykryto: {rawSportName}). VeloAnalytics obsługuje wyłącznie sesje rowerowe.");
        }

        if (records.Count > 0)
        {
            if (totalTimerTime == 0)
            {
                totalTimerTime = (int)(records[^1].Timestamp - records[0].Timestamp).TotalSeconds;
            }
            if (startTime == DateTime.UtcNow)
            {
                startTime = records[0].Timestamp;
            }
            if (totalDistance == 0 && records[^1].DistanceMeters.HasValue)
            {
                totalDistance = records[^1].DistanceMeters!.Value;
            }
        }

        string defaultTitle = $"{detectedSport} - {startTime:yyyy-MM-dd HH:mm}";

        return new ParsedFitData(
            Title: defaultTitle,
            SportType: detectedSport,
            StartTime: startTime,
            DurationSeconds: totalTimerTime,
            MovingTimeSeconds: totalTimerTime,
            DistanceMeters: totalDistance,
            TotalElevationGain: totalAscent,
            FileHash: fileHash,
            AvgHeartRate: sessionAvgHr,
            MaxHeartRate: sessionMaxHr,
            Records: records
        );
    }
}
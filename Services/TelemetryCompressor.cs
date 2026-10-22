using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using inzynierka.Models.Entities;

namespace inzynierka.Services;

public static class TelemetryCompressor
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static byte[] Compress(List<ActivityRecord> records)
    {
        if (records == null || records.Count == 0)
            return Array.Empty<byte>();

        using var outputStream = new MemoryStream();
        using (var brotliStream = new BrotliStream(outputStream, CompressionLevel.Fastest))
        {
            JsonSerializer.Serialize(brotliStream, records, Options);
        }
        return outputStream.ToArray();
    }

    public static List<ActivityRecord> Decompress(byte[]? compressedData)
    {
        if (compressedData == null || compressedData.Length == 0)
            return new List<ActivityRecord>();

        using var inputStream = new MemoryStream(compressedData);
        using var brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<List<ActivityRecord>>(brotliStream, Options) ?? new List<ActivityRecord>();
    }
}
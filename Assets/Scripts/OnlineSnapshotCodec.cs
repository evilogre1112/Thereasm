using System;
using System.IO;
using System.IO.Compression;
using System.Text;

// Compress only transport snapshots, not user input or game-state calculations.
// Encoding is lossless; both peers must support the compressed format.
public static class OnlineSnapshotCodec
{
    private const string Prefix = "gz:";
    private const int MaxDecodedBytes = 1024 * 1024;

    public static string Encode(string json)
    {
        if (string.IsNullOrEmpty(json) || json.Length < 1024) return json;
        byte[] data = Encoding.UTF8.GetBytes(json);
        using (var output = new MemoryStream())
        {
            using (var zip = new GZipStream(output, CompressionLevel.Fastest, true))
                zip.Write(data, 0, data.Length);
            string packed = Prefix + Convert.ToBase64String(output.ToArray());
            // Keep raw JSON if compression doesn't actually reduce payload size.
            return packed.Length < json.Length ? packed : json;
        }
    }

    public static string Decode(string payload)
    {
        if (string.IsNullOrEmpty(payload) || !payload.StartsWith(Prefix, StringComparison.Ordinal)) return payload;
        byte[] compressed = Convert.FromBase64String(payload.Substring(Prefix.Length));
        using (var input = new MemoryStream(compressed))
        using (var zip = new GZipStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            byte[] buffer = new byte[8192];
            int count;
            while ((count = zip.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + count > MaxDecodedBytes)
                    throw new InvalidDataException("Online snapshot exceeds the allowed limit.");
                output.Write(buffer, 0, count);
            }
            return Encoding.UTF8.GetString(output.ToArray());
        }
    }
}

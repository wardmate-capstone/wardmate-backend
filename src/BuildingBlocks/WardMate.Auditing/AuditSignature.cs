using System.Security.Cryptography;
using System.Text;

namespace WardMate.Auditing;

public static class AuditSignature
{
    public static string Sign(string key, string source, string timestamp, string body) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(source + "\n" + timestamp + "\n" + body)));
    public static bool Verify(string key, string source, string timestamp, string body, string signature, DateTimeOffset now)
    {
        if (Encoding.UTF8.GetByteCount(key) < 32 || !long.TryParse(timestamp, out var seconds) ||
            seconds < now.ToUnixTimeSeconds()-300 || seconds > now.ToUnixTimeSeconds()+300 || signature.Length != 64) return false;
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Sign(key,source,timestamp,body)),Convert.FromHexString(signature)); }
        catch (FormatException) { return false; }
    }
}

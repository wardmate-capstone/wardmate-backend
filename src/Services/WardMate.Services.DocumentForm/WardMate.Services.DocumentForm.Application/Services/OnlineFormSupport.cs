using System.Text.Json;
using System.Security.Cryptography;
using WardMate.Services.DocumentForm.Domain.Models;
namespace WardMate.Services.DocumentForm.Application.Services;

public static class OnlineFormSupport
{
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string BlobName(string url)
    {
        var path = new Uri(url).AbsolutePath.TrimStart('/');
        if (path.StartsWith("devstoreaccount1/", StringComparison.Ordinal)) path = path[17..];
        return Uri.UnescapeDataString(path[(path.IndexOf('/') + 1)..]);
    }
    public static IReadOnlyList<DocxFieldMapping> Mappings(string json) =>
        JsonSerializer.Deserialize<List<DocxFieldMapping>>(json) ?? [];
    public static Dictionary<string, string> Values(JsonElement data) => data.EnumerateObject()
        .ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.Null ? "" :
            p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText());
}

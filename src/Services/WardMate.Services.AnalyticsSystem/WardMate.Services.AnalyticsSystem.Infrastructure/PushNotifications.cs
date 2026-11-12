using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

// PUSH-01: thông báo đẩy tới app Mobile qua Firebase Cloud Messaging (FCM HTTP v1, miễn phí).
// Tắt mặc định (Push:Enabled=false). Bật cần Push:Fcm:ProjectId, Push:Fcm:ClientEmail, Push:Fcm:PrivateKey (khóa PEM của service account).
public sealed class PushDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = "";
    public string Platform { get; set; } = "";
    public string? AppVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
}

public sealed record PushDeviceInput(string? Token, string? Platform, string? AppVersion);

public sealed class PushDeviceException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public sealed class PushDevices(NotificationDbContext db, TimeProvider clock)
{
    public const int MaxDevicesPerUser = 10;
    public static readonly string[] Platforms = ["ANDROID", "IOS", "WEB"];

    public static (string Token, string Platform, string? AppVersion) Validate(PushDeviceInput input)
    {
        var token = input.Token?.Trim() ?? "";
        var platform = input.Platform?.Trim().ToUpperInvariant() ?? "";
        var version = string.IsNullOrWhiteSpace(input.AppVersion) ? null : input.AppVersion.Trim();
        if (token.Length is < 20 or > 1024 || token.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new PushDeviceException(400, "push.invalid_token", "Token thiết bị không hợp lệ.");
        if (!Platforms.Contains(platform))
            throw new PushDeviceException(400, "push.invalid_platform", "platform phải là ANDROID, IOS hoặc WEB.");
        if (version is { Length: > 50 })
            throw new PushDeviceException(400, "push.invalid_version", "appVersion tối đa 50 ký tự.");
        return (token, platform, version);
    }

    /// <summary>Gắn token cho người đang đăng nhập. Token đã thuộc người khác (đổi tài khoản trên cùng máy) được chuyển sang người này.</summary>
    public async Task Register(Guid userId, PushDeviceInput input, CancellationToken ct)
    {
        var (token, platform, version) = Validate(input);
        var now = clock.GetUtcNow().UtcDateTime;
        var device = await db.PushDevices.SingleOrDefaultAsync(x => x.Token == token, ct);
        if (device is null) { device = new PushDevice { Token = token, CreatedAt = now }; db.PushDevices.Add(device); }
        device.UserId = userId; device.Platform = platform; device.AppVersion = version; device.LastSeenAt = now;
        await db.SaveChangesAsync(ct);
        // Giữ tối đa 10 thiết bị mới nhất mỗi người; thiết bị cũ không còn dùng sẽ bị bỏ.
        var stale = await db.PushDevices.Where(x => x.UserId == userId).OrderByDescending(x => x.LastSeenAt)
            .Skip(MaxDevicesPerUser).Select(x => x.Id).ToListAsync(ct);
        if (stale.Count > 0) await db.PushDevices.Where(x => stale.Contains(x.Id)).ExecuteDeleteAsync(ct);
    }

    /// <summary>Gỡ token khi đăng xuất. Chỉ gỡ token của chính mình; không có cũng trả thành công.</summary>
    public Task Unregister(Guid userId, string? token, CancellationToken ct)
    {
        var value = token?.Trim() ?? "";
        if (value.Length is 0 or > 1024) throw new PushDeviceException(400, "push.invalid_token", "Token thiết bị không hợp lệ.");
        return db.PushDevices.Where(x => x.UserId == userId && x.Token == value).ExecuteDeleteAsync(ct);
    }
}

/// <summary>Gửi FCM HTTP v1. Access token OAuth2 ký RS256 bằng khóa service account, lưu đệm tới gần hết hạn.</summary>
public sealed class FcmPushSender(HttpClient http, IConfiguration configuration, TimeProvider clock)
{
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static (string Token, DateTimeOffset Expires)? cached;

    public bool Configured => configuration.GetValue<bool>("Push:Enabled")
        && !string.IsNullOrWhiteSpace(configuration["Push:Fcm:ProjectId"])
        && !string.IsNullOrWhiteSpace(configuration["Push:Fcm:ClientEmail"])
        && !string.IsNullOrWhiteSpace(configuration["Push:Fcm:PrivateKey"]);

    public enum Outcome { Sent, InvalidToken, Retry }

    public static object Message(string token, Notification n) => new
    {
        message = new
        {
            token,
            notification = new { title = Clip(n.Title, 120), body = Clip(n.Content, 240) },
            // data chỉ chứa chuỗi; app dùng actionUrl để mở đúng màn (deep link).
            data = new Dictionary<string, string>
            {
                ["notificationId"] = n.Id.ToString(), ["type"] = n.Type, ["actionUrl"] = n.ActionUrl ?? "",
                ["applicationId"] = n.ApplicationId?.ToString() ?? ""
            },
            android = new { priority = "HIGH", notification = new { channel_id = "wardmate_default" } },
            apns = new { payload = new { aps = new { sound = "default" } } }
        }
    };

    public async Task<Outcome> Send(string token, Notification n, CancellationToken ct)
    {
        var project = configuration["Push:Fcm:ProjectId"]!;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://fcm.googleapis.com/v1/projects/{Uri.EscapeDataString(project)}/messages:send")
        { Content = JsonContent.Create(Message(token, n)) };
        request.Headers.Authorization = new("Bearer", await AccessToken(ct));
        using var response = await http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return Outcome.Sent;
        // 404 UNREGISTERED: app đã gỡ/token hết hạn; 400 INVALID_ARGUMENT với token sai → xóa token.
        if (response.StatusCode == HttpStatusCode.NotFound) return Outcome.InvalidToken;
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return body.Contains("registration token", StringComparison.OrdinalIgnoreCase) || body.Contains("UNREGISTERED", StringComparison.Ordinal)
                ? Outcome.InvalidToken : throw new NotificationSendException("push.bad_request");
        }
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        { cached = null; throw new NotificationSendException("push.auth_failed"); }
        return Outcome.Retry;
    }

    private async Task<string> AccessToken(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (cached is { } c && c.Expires > now.AddMinutes(5)) return c.Token;
        await TokenLock.WaitAsync(ct);
        try
        {
            if (cached is { } again && again.Expires > now.AddMinutes(5)) return again.Token;
            var assertion = Assertion(configuration["Push:Fcm:ClientEmail"]!, configuration["Push:Fcm:PrivateKey"]!, now);
            using var response = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer", ["assertion"] = assertion
            }), ct);
            if (!response.IsSuccessStatusCode) throw new NotificationSendException("push.auth_failed");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var token = json.RootElement.GetProperty("access_token").GetString() ?? throw new NotificationSendException("push.auth_failed");
            var seconds = json.RootElement.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var s) ? s : 3600;
            cached = (token, now.AddSeconds(seconds));
            return token;
        }
        finally { TokenLock.Release(); }
    }

    /// <summary>JWT RS256 theo chuẩn service account của Google (scope firebase.messaging, hạn 1 giờ).</summary>
    public static string Assertion(string clientEmail, string privateKeyPem, DateTimeOffset now)
    {
        static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = B64(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = clientEmail, scope = "https://www.googleapis.com/auth/firebase.messaging",
            aud = "https://oauth2.googleapis.com/token", iat = now.ToUnixTimeSeconds(), exp = now.AddHours(1).ToUnixTimeSeconds()
        }));
        using var rsa = RSA.Create();
        // Biến môi trường thường lưu khóa với "\n" dạng chữ; đổi lại thành xuống dòng thật.
        rsa.ImportFromPem(privateKeyPem.Replace("\\n", "\n"));
        var signature = rsa.SignData(Encoding.ASCII.GetBytes(header + "." + payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return header + "." + payload + "." + B64(signature);
    }

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}

/// <summary>Xử lý một dòng giao kênh PUSH: gửi tới mọi thiết bị của người nhận, xóa token chết.</summary>
public sealed class PushDeliverySender(NotificationDbContext db, FcmPushSender fcm)
{
    public async Task<string> Send(Notification n, CancellationToken ct)
    {
        if (!fcm.Configured) return "Blocked";
        var devices = await db.PushDevices.Where(x => x.UserId == n.RecipientUserId).ToListAsync(ct);
        if (devices.Count == 0) return "Suppressed";
        var sent = 0; var retry = 0;
        foreach (var device in devices)
        {
            switch (await fcm.Send(device.Token, n, ct))
            {
                case FcmPushSender.Outcome.Sent: sent++; break;
                case FcmPushSender.Outcome.InvalidToken: db.PushDevices.Remove(device); break;
                default: retry++; break;
            }
        }
        if (sent > 0) return "Sent";
        if (retry > 0) throw new NotificationSendException("push.provider_unavailable");
        return "Suppressed"; // mọi token đều chết và đã bị xóa
    }
}

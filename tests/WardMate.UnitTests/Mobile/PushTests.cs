using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using WardMate.Services.AnalyticsSystem.Domain;
using WardMate.Services.AnalyticsSystem.Infrastructure;

namespace WardMate.UnitTests.Mobile;

// PUSH-01: đăng ký token thiết bị, ký JWT service account cho FCM, nội dung tin đẩy và điều kiện xếp hàng gửi.
public sealed class PushTests
{
    [Fact]
    public void Device_input_is_normalized_and_validated()
    {
        var (token, platform, version) = PushDevices.Validate(new(" " + new string('a', 150) + " ", "android", " 1.2.0 "));
        Assert.Equal(150, token.Length);
        Assert.Equal("ANDROID", platform);
        Assert.Equal("1.2.0", version);
        Assert.Equal("push.invalid_token", Assert.Throws<PushDeviceException>(() => PushDevices.Validate(new("short", "IOS", null))).Code);
        Assert.Equal("push.invalid_token", Assert.Throws<PushDeviceException>(() => PushDevices.Validate(new(new string('a', 30) + " x", "IOS", null))).Code);
        Assert.Equal("push.invalid_platform", Assert.Throws<PushDeviceException>(() => PushDevices.Validate(new(new string('a', 30), "SYMBIAN", null))).Code);
    }

    [Fact]
    public void Service_account_assertion_is_valid_rs256_jwt()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportPkcs8PrivateKeyPem().Replace("\n", "\\n"); // dạng thường gặp khi đặt trong biến môi trường
        var now = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
        var jwt = FcmPushSender.Assertion("push@wardmate-test.iam.gserviceaccount.com", pem, now);
        var parts = jwt.Split('.');
        Assert.Equal(3, parts.Length);
        static byte[] Decode(string s) => Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/').PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
        Assert.True(rsa.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        using var payload = JsonDocument.Parse(Decode(parts[1]));
        Assert.Equal("https://www.googleapis.com/auth/firebase.messaging", payload.RootElement.GetProperty("scope").GetString());
        Assert.Equal(now.AddHours(1).ToUnixTimeSeconds(), payload.RootElement.GetProperty("exp").GetInt64());
    }

    [Fact]
    public void Message_carries_deep_link_data_and_short_text()
    {
        var n = new Notification
        {
            Id = Guid.NewGuid(), ApplicationId = Guid.NewGuid(), Title = "Hồ sơ yêu cầu bổ sung", Content = new string('x', 500),
            Type = "APPLICATION_STATUS_CHANGED", ActionUrl = "/citizen/applications/1"
        };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(FcmPushSender.Message("tok", n)));
        var message = json.RootElement.GetProperty("message");
        Assert.Equal("tok", message.GetProperty("token").GetString());
        Assert.Equal(240, message.GetProperty("notification").GetProperty("body").GetString()!.Length);
        var data = message.GetProperty("data");
        Assert.Equal("/citizen/applications/1", data.GetProperty("actionUrl").GetString());
        Assert.All(data.EnumerateObject(), p => Assert.Equal(JsonValueKind.String, p.Value.ValueKind));
    }

    [Fact]
    public void Push_is_queued_only_when_enabled_for_recent_in_app_notifications()
    {
        IConfiguration Config(bool on) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Push:Enabled"] = on.ToString() }).Build();
        var fresh = new Notification { Channel = "IN_APP", CreatedAt = DateTime.UtcNow };
        Assert.True(NotificationDeliveryWorker.ShouldQueuePush(Config(true), fresh));
        Assert.False(NotificationDeliveryWorker.ShouldQueuePush(Config(false), fresh));
        Assert.False(NotificationDeliveryWorker.ShouldQueuePush(Config(true), new Notification { Channel = "IN_APP", CreatedAt = DateTime.UtcNow.AddDays(-2) }));
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Interfaces;
namespace WardMate.Services.IAM.Infrastructure.Security;

public sealed class IamSmsSender(HttpClient http, IConfiguration config, ILogger<IamSmsSender> logger) : IIamSmsSender
{
    public bool Enabled => config.GetValue<bool>("Sms:Enabled")
        && string.Equals(config["Sms:Provider"], "eSMS", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(config["Sms:ApiKey"])
        && !string.IsNullOrWhiteSpace(config["Sms:SecretKey"])
        && Regex.IsMatch(config["Sms:Brandname"] ?? "", "^[a-zA-Z0-9 ]{1,32}$")
        && config["Sms:SmsType"] == "2" && config["Sms:Sandbox"] is "0" or "1";
    public static string? NormalizePhone(string phone)
    {
        var value = Regex.Replace(phone.Trim(), @"[\s().-]", "");
        if (value.StartsWith("+84", StringComparison.Ordinal)) value = "0" + value[3..];
        else if (value.StartsWith("84", StringComparison.Ordinal)) value = "0" + value[2..];
        return Regex.IsMatch(value, @"^0[35789][0-9]{8}$") ? value : null;
    }
    private static Result<bool> Failed() => Result<bool>.Failure(new("iam.sms_delivery_failed",
        "Không thể gửi SMS lúc này. Vui lòng đợi 60 giây hoặc chọn Email.", ErrorKind.ServiceUnavailable));
    public async Task<Result<bool>> SendOtp(string phoneNumber, string otpCode, CancellationToken ct)
    {
        if (!Enabled) return Failed();
        var phone = NormalizePhone(phoneNumber);
        if (phone is null || !Regex.IsMatch(otpCode, "^[0-9]{6}$")) return Failed();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await http.PostAsJsonAsync(
                "https://rest.esms.vn/MainService.svc/json/SendMultipleMessage_V4_post_json/",
                new { ApiKey = config["Sms:ApiKey"], SecretKey = config["Sms:SecretKey"], Phone = phone,
                    Content = $"{otpCode} la ma xac minh dang ky {config["Sms:Brandname"]} cua ban",
                    Brandname = config["Sms:Brandname"], SmsType = "2", IsUnicode = "0", Sandbox = config["Sms:Sandbox"] },
                new JsonSerializerOptions { PropertyNamingPolicy = null }, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("IAM eSMS HTTP failure: {StatusCode}", (int)response.StatusCode);
                return Failed();
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var code = json.RootElement.TryGetProperty("CodeResult", out var result) ? result.ToString() : "";
            if (code == "100") return Result<bool>.Success(true);
            // Provider messages may echo credentials/content. Log only a short numeric result code.
            logger.LogWarning("IAM eSMS rejected request: {ProviderCode}", Regex.IsMatch(code, "^[0-9]{1,4}$") ? code : "invalid-response");
            return Failed();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException or InvalidOperationException)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogWarning("IAM eSMS transport/response failure: {FailureType}", ex.GetType().Name);
            return Failed();
        }
    }
}

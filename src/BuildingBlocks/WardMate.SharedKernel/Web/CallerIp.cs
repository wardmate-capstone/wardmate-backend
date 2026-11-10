using System.Net;
using Microsoft.AspNetCore.Http;

namespace WardMate.SharedKernel.Web;

/// <summary>
/// Địa chỉ người gọi dùng làm khóa giới hạn số lần gọi (rate limit).
/// Gateway đặt X-Forwarded-For bằng người gọi mà nó thấy (gateway tin một hop ingress); ingress của service
/// có thể nối thêm hop phía sau nên phần tử ĐẦU là người gọi. Nếu service đã chạy UseForwardedHeaders,
/// phần tử bị tiêu thụ được bỏ khỏi header và nằm ở RemoteIpAddress — trường hợp đó dùng RemoteIpAddress.
/// Client gọi thẳng service (không qua gateway) có thể giả header này; giới hạn theo IP chỉ là lớp bảo vệ phụ.
/// </summary>
public static class CallerIp
{
    public static string Of(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        var first = forwarded.Split(',', 2)[0].Trim();
        if (TryParse(first, out var parsed)) return Normalize(parsed);
        return context.Connection.RemoteIpAddress is { } remote ? Normalize(remote) : "unknown";
    }

    private static bool TryParse(string value, out IPAddress address)
    {
        if (IPAddress.TryParse(value, out var ip)) { address = ip; return true; }
        if (IPEndPoint.TryParse(value, out var endpoint)) { address = endpoint.Address; return true; }
        address = IPAddress.None;
        return false;
    }

    private static string Normalize(IPAddress address) => (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
}

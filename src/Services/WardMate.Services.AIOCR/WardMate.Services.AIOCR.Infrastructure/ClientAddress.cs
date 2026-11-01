using System.Net;
using Microsoft.AspNetCore.Http;

namespace WardMate.Services.AIOCR.Infrastructure;

// Address used to limit anonymous chat. The gateway sets X-Forwarded-For to the caller it saw (its own forwarded-headers
// step trusts one ingress hop), and the AIOCR ingress appends hops after it, so the first entry is the caller.
// A client that reaches AIOCR without the gateway can forge this header; the daily guest pool still caps the cost.
public static class ClientAddress
{
    public static string Of(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        var first = forwarded.Split(',', 2)[0].Trim();
        if (IPAddress.TryParse(first, out var parsed)) return Normalize(parsed);
        return context.Connection.RemoteIpAddress is { } remote ? Normalize(remote) : "unknown";
    }

    private static string Normalize(IPAddress address) => (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
}

using System.Net;
using Microsoft.AspNetCore.Http;
using WardMate.SharedKernel.Web;

namespace WardMate.UnitTests.SharedKernel;

// IAM-01a / DOC-QR-06a: rate limit phải khóa theo người gọi thật, không theo IP gateway/ingress.
public sealed class CallerIpTests
{
    private static DefaultHttpContext Context(string? forwarded, string remote = "10.0.0.9")
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        if (forwarded is not null) context.Request.Headers["X-Forwarded-For"] = forwarded;
        return context;
    }

    [Fact]
    public void Uses_first_forwarded_entry_as_caller() =>
        Assert.Equal("203.0.113.5", CallerIp.Of(Context("203.0.113.5, 10.0.0.1")));

    [Fact]
    public void Falls_back_to_remote_address_without_header() =>
        Assert.Equal("10.0.0.9", CallerIp.Of(Context(null)));

    [Fact]
    public void Ignores_unparseable_header() =>
        Assert.Equal("10.0.0.9", CallerIp.Of(Context("not-an-ip")));

    [Fact]
    public void Strips_port_from_forwarded_entry() =>
        Assert.Equal("198.51.100.7", CallerIp.Of(Context("198.51.100.7:5050")));

    [Fact]
    public void Normalizes_ipv4_mapped_ipv6_remote_address() =>
        Assert.Equal("192.0.2.4", CallerIp.Of(Context(null, "::ffff:192.0.2.4")));
}

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;

namespace WardMate.Auditing;

public static class AuditTrustedProxy
{
    public static IApplicationBuilder UseAuditTrustedProxy(this IApplicationBuilder app,IConfiguration config)
    {
        var proxies=config.GetSection("Audit:TrustedProxies").Get<string[]>() ?? [];
        if(proxies.Length==0) return app;
        var options=new ForwardedHeadersOptions { ForwardedHeaders=ForwardedHeaders.XForwardedFor, ForwardLimit=1 };
        options.KnownNetworks.Clear(); options.KnownProxies.Clear();
        foreach(var proxy in proxies)
        {
            if(!IPAddress.TryParse(proxy,out var ip)) throw new InvalidOperationException("Audit:TrustedProxies must contain exact IP addresses.");
            options.KnownProxies.Add(ip);
        }
        return app.UseForwardedHeaders(options);
    }
}

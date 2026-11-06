using Microsoft.Extensions.Configuration;
using WardMate.Services.ApplicationWorkflow.Application;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed record LeaveTemplateSettings(LeaveTemplateDescriptor? Template, bool WordRequired)
{
    public static LeaveTemplateSettings Read(IConfiguration config, string wardCode)
    {
        var shared = config.GetSection("WorkSchedule:SharedLeaveTemplate");
        var legacy = config.GetSection($"WorkSchedule:Wards:{wardCode}");
        // Select the pair together. A partially configured shared template must never use a legacy hash.
        var source = shared["TemplateVersionId"] is not null || shared["TemplateSha256"] is not null ? shared : legacy;
        var hash = source["TemplateSha256"];
        var template = Guid.TryParse(source["TemplateVersionId"], out var id) && id != Guid.Empty &&
            hash is { Length: 64 } && hash.All(Uri.IsHexDigit)
            ? new LeaveTemplateDescriptor(id, hash.ToUpperInvariant()) : null;
        return new(template, shared.GetValue<bool?>("RequireWord") ?? legacy.GetValue<bool>("RequireWord"));
    }
}

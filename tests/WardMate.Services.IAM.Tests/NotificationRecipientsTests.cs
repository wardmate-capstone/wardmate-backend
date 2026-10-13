using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WardMate.Services.IAM.Application.Accounts;
using WardMate.Services.IAM.Infrastructure.Persistence;
using Xunit;

namespace WardMate.Services.IAM.Tests;

public sealed class NotificationRecipientsTests(IamFixture f) : IClassFixture<IamFixture>
{
    [Fact]
    public async Task ServiceKeyIsRequiredAndOnlyActiveScopedRecipientsAreReturned()
    {
        var code = "N-" + Guid.NewGuid().ToString("N"); var ward = await f.Ward(code);
        var officer = await f.User(2, ward); var foreign = await f.User(2); var citizen = await f.User(1, ward);
        using var client = f.Factory.CreateClient(); var body = new NotificationRecipientRequest([citizen], code);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/internal/v1/notification-recipients", body)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Notification-Service-Key", f.NotificationKey);
        var response = await client.PostAsJsonAsync("/internal/v1/notification-recipients", body); response.EnsureSuccessStatusCode();
        var recipients = (await response.Content.ReadFromJsonAsync<NotificationRecipient[]>())!;
        Assert.Contains(recipients, x => x.UserId == officer && x.IsOfficer); Assert.Contains(recipients, x => x.UserId == citizen);
        Assert.DoesNotContain(recipients, x => x.UserId == foreign);
        await using (var scope = f.Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<IamDbContext>().Users.Where(x => x.Id == officer).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        response = await client.PostAsJsonAsync("/internal/v1/notification-recipients", body);
        Assert.DoesNotContain((await response.Content.ReadFromJsonAsync<NotificationRecipient[]>())!, x => x.UserId == officer);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/internal/v1/notification-recipients", new { userIds = new[] { Guid.Empty }, page = 0 })).StatusCode);
    }
}

using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Queries;
using WardMate.Services.DocumentForm.Domain.Entities;
using WardMate.Services.DocumentForm.Infrastructure.Persistence;

namespace WardMate.UnitTests.DocumentForm;

// DOC-QR-06a (khớp chính xác) và DOC-QR-06b (không nói phê duyệt).
public sealed class QrLookupQueryTests
{
    private static readonly Guid ApplicationId = Guid.Parse("b1ff2814-ba88-4dd8-b539-4dcb05e9c2e9");
    private const string QrValue = "https://gateway.test/api/v1/public/applications/b1ff2814-ba88-4dd8-b539-4dcb05e9c2e9/pdf-redirect";

    private static async Task<QrLookupQueryHandler> Handler()
    {
        var db = new DocumentDbContext(new DbContextOptionsBuilder<DocumentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.ApprovedQrs.Add(new ApprovedQr(ApplicationId, null, QrValue, [1]));
        await db.SaveChangesAsync();
        return new QrLookupQueryHandler(db);
    }

    [Theory]
    [InlineData("https")]
    [InlineData("gateway.test")]
    [InlineData("b1ff2814")]
    [InlineData("pdf-redirect")]
    public async Task Substring_does_not_match(string token)
    {
        var result = await (await Handler()).Handle(new QrLookupQuery(token), default);
        Assert.False(result.IsSuccess);
        Assert.Equal("qr.not_found", result.Error.Code);
    }

    [Theory]
    [InlineData("b1ff2814-ba88-4dd8-b539-4dcb05e9c2e9")]
    [InlineData(QrValue)]
    [InlineData("https://old.test/api/v1/public/qr-lookup?applicationId=b1ff2814-ba88-4dd8-b539-4dcb05e9c2e9")]
    [InlineData("https://old.test/api/v1/applications/b1ff2814-ba88-4dd8-b539-4dcb05e9c2e9")]
    public async Task Exact_id_or_url_matches(string token)
    {
        var result = await (await Handler()).Handle(new QrLookupQuery(token), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(ApplicationId, result.Value.ApplicationId);
    }

    [Fact]
    public async Task Result_does_not_claim_approval()
    {
        var result = await (await Handler()).Handle(new QrLookupQuery(ApplicationId.ToString()), default);
        Assert.Equal("DOCUMENTS_READY", result.Value.Status);
        Assert.NotEqual("APPROVED", result.Value.Status);
        Assert.Equal(QrLookupQueryHandler.QrValidMessage, result.Value.Message);
        Assert.DoesNotContain("đóng dấu", result.Value.Message);
    }

    [Fact]
    public async Task Rejects_overlong_token()
    {
        var result = await (await Handler()).Handle(new QrLookupQuery(new string('a', 2049)), default);
        Assert.Equal("qr.token_too_long", result.Error.Code);
    }

    [Fact]
    public async Task Unknown_guid_is_not_found()
    {
        var result = await (await Handler()).Handle(new QrLookupQuery(Guid.NewGuid().ToString()), default);
        Assert.Equal("qr.not_found", result.Error.Code);
    }
}

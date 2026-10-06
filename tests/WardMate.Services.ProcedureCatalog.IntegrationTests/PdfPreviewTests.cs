using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WardMate.Services.AIOCR.Infrastructure.Extraction;
using WardMate.Services.ProcedureCatalog.Application.Drafts;
using Xunit;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class PdfPreviewTests(ProcedureFixture fixture) : IClassFixture<ProcedureFixture>
{
    [Fact]
    public async Task PreviewRequiresManagerAndReturnsNativeTextWithoutBlobConfiguration()
    {
        const string route = "/api/v1/procedure-manager/drafts/extract-preview";
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["AzureBlob:ConnectionString"] = "" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProcedureExtractor>(); services.AddSingleton<IProcedureExtractor, LocalExtractor>();
            });
        });
        using var manager = fixture.ManagerClient(); using var client = factory.CreateClient();
        byte[] pdf = TextPdfExtractionTests.Pdf("Procedure title: Test application. Required documents: original identity document.");
        MultipartFormDataContent File()
        {
            var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(pdf), "file", "sample.pdf"); return form;
        }
        using (var file = File()) Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(route, file)).StatusCode);
        using var citizen = fixture.ManagerClient("REGISTERED_CITIZEN");
        client.DefaultRequestHeaders.Authorization = citizen.DefaultRequestHeaders.Authorization;
        using (var file = File()) Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync(route, file)).StatusCode);
        client.DefaultRequestHeaders.Authorization = manager.DefaultRequestHeaders.Authorization;
        using var upload = File(); using var response = await client.PostAsync(route, upload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var result = (await response.Content.ReadFromJsonAsync<ExtractionResult>())!;
        Assert.Contains("Test application", result.ExtractedText);
        Assert.Contains(result.Warnings, x => x.Contains("Chưa bật AI"));
    }
    private sealed class LocalExtractor : IProcedureExtractor
    {
        public async Task<ExtractionResult> Extract(Stream pdf, CancellationToken ct)
        {
            using var http = new HttpClient(); var config = new ConfigurationBuilder().Build();
            var native = new TextFirstProcedureExtractor(new AzureProcedureDocumentExtractor(http, config), config);
            var result = await native.Extract(pdf, ct);
            return new(result.Payload, result.ExtractedText, result.Warnings);
        }
    }
}

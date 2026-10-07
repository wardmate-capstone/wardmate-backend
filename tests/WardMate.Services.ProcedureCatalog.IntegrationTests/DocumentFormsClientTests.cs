using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Infrastructure;
using Xunit;

namespace WardMate.Services.ProcedureCatalog.IntegrationTests;

public sealed class DocumentFormsClientTests
{
    private sealed class Handler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? Requested { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requested = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
    [Fact]
    public async Task MapsCurrentDocumentFormContractAndEscapesSearch()
    {
        var id = Guid.NewGuid();
        using var handler = new Handler(HttpStatusCode.OK,
            $$"""{"items":[{"id":"{{id}}","code":"CT01","title":"Tờ khai","isActive":true}],"totalCount":1} """);
        using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DocumentForm:BaseUrl"] = "http://document-form:8080/" }).Build();
        var result = await new DocumentFormsClient(http, config).List(new(2, 20, "A&B"), default);
        Assert.True(result.IsSuccess); var item = Assert.Single(result.Value!);
        Assert.Equal(id, item.Id); Assert.Equal("CT01", item.FormCode); Assert.Equal("DOCX_TEMPLATE", item.FormType);
        Assert.Contains("page=2", handler.Requested!.Query); Assert.Contains("A%26B", handler.Requested.Query);
    }
    [Theory]
    [InlineData(HttpStatusCode.OK, "{}", 502)]
    [InlineData(HttpStatusCode.OK, "not-json", 502)]
    [InlineData(HttpStatusCode.InternalServerError, "", 503)]
    public async Task UpstreamFailureIsProblemResult(HttpStatusCode status, string body, int expected)
    {
        using var handler = new Handler(status, body); using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["DocumentForm:BaseUrl"] = "http://document-form:8080/" }).Build();
        var result = await new DocumentFormsClient(http, config).List(new(), default);
        Assert.Equal(expected, result.Error!.Status);
    }
}

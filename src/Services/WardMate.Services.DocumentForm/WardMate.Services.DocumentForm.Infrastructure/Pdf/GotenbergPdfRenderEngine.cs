using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using WardMate.Services.DocumentForm.Application.Interfaces;

namespace WardMate.Services.DocumentForm.Infrastructure.Pdf;

// A failed conversion must never produce a placeholder that looks like an official result.
public sealed class GotenbergPdfRenderEngine(HttpClient http, IConfiguration configuration) : IPdfRenderEngine
{
    public async Task<Stream> RenderDocxToPdfAsync(Stream docxStream, string fileName = "document.docx",
        CancellationToken cancellationToken = default)
    {
        var root = configuration["Gotenberg:Url"];
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("Chưa cấu hình dịch vụ xuất PDF Gotenberg.");
        using var content = new MultipartFormDataContent();
        var file = new StreamContent(docxStream);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        content.Add(file, "files", Path.GetFileName(fileName));
        using var response = await http.PostAsync(uri.AbsoluteUri.TrimEnd('/') + "/forms/libreoffice/convert", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new InvalidOperationException("Dịch vụ xuất PDF trả về nội dung không hợp lệ.");
        return new MemoryStream(bytes, writable: false);
    }

    public async Task<Stream> MergePdfsAsync(
        IReadOnlyList<(Stream Stream, string FileName)> pdfStreams,
        CancellationToken cancellationToken = default)
    {
        if (pdfStreams.Count == 0)
            throw new ArgumentException("Danh sách file PDF để gộp không được rỗng.", nameof(pdfStreams));
        if (pdfStreams.Count == 1)
            return pdfStreams[0].Stream;

        var root = configuration["Gotenberg:Url"];
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("Chưa cấu hình dịch vụ xuất PDF Gotenberg.");

        using var content = new MultipartFormDataContent();
        for (int i = 0; i < pdfStreams.Count; i++)
        {
            var (stream, fileName) = pdfStreams[i];
            var file = new StreamContent(stream);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            // Gotenberg merge files theo thứ tự alphabet của tên file trong form data (001_..., 002_...)
            var orderedName = $"{i + 1:D3}_{Path.GetFileName(fileName)}";
            content.Add(file, "files", orderedName);
        }

        using var response = await http.PostAsync(uri.AbsoluteUri.TrimEnd('/') + "/forms/pdfengines/merge", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new InvalidOperationException("Dịch vụ gộp PDF trả về nội dung không hợp lệ.");
        return new MemoryStream(bytes, writable: false);
    }
}

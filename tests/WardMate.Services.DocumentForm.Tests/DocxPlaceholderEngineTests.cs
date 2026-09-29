using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WardMate.Services.DocumentForm.Application.Models;
using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.Services.DocumentForm.Infrastructure.OpenXml;
using Xunit;

namespace WardMate.Services.DocumentForm.Tests;

public sealed class DocxPlaceholderEngineTests
{
    private readonly DocxPlaceholderEngine _engine = new();

    private static byte[] CreateSampleDocx(string textContent)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());
            var paragraph = body.AppendChild(new Paragraph());
            var run = paragraph.AppendChild(new Run());
            run.AppendChild(new Text(textContent));
            mainPart.Document.Save();
        }
        return ms.ToArray();
    }

    [Fact]
    public void ExtractPlaceholders_EmptyBytes_ReturnsEmptyFileError()
    {
        var result = _engine.ExtractPlaceholders([]);

        Assert.True(result.IsFailure);
        Assert.Equal("document.empty_file", result.Error.Code);
    }

    [Fact]
    public void ExtractPlaceholders_ValidDocxWithPlaceholders_ExtractsNormalizedNames()
    {
        var content = "Họ và tên: {{ho_va_ten}}, Ngày sinh: {{ngay_sinh}}. CCCD: {{so_cccd}}. Xác nhận: {{ho_va_ten}}.";
        var docxBytes = CreateSampleDocx(content);

        var result = _engine.ExtractPlaceholders(docxBytes);

        Assert.True(result.IsSuccess);
        var placeholders = result.Value;
        Assert.Equal(3, placeholders.Count);

        var namePlaceholder = placeholders.First(p => p.Name == "ho_va_ten");
        Assert.Equal(2, namePlaceholder.Occurrences);
        Assert.Equal("{{ho_va_ten}}", namePlaceholder.RawTag);

        Assert.Contains(placeholders, p => p.Name == "ngay_sinh" && p.Occurrences == 1);
        Assert.Contains(placeholders, p => p.Name == "so_cccd" && p.Occurrences == 1);
    }

    [Fact]
    public void MatchPlaceholdersWithSchema_CorrectlyIdentifiesMatchesAndDiscrepancies()
    {
        var placeholders = new List<DocxPlaceholder>
        {
            new() { Name = "ho_ten", RawTag = "{{ho_ten}}" },
            new() { Name = "ngay_sinh", RawTag = "{{ngay_sinh}}" },
            new() { Name = "placeholder_only", RawTag = "{{placeholder_only}}" }
        };

        var schema = new FormSchemaDefinition
        {
            Title = "Test Schema",
            Sections =
            [
                new()
                {
                    SectionId = "sec1",
                    Title = "Section 1",
                    Fields =
                    [
                        new() { FieldId = "ho_ten", Label = "Họ và tên", Type = FormFieldType.Text },
                        new() { FieldId = "ngay_sinh", Label = "Ngày sinh", Type = FormFieldType.Date },
                        new() { FieldId = "schema_only", Label = "Chỉ có ở schema", Type = FormFieldType.Text }
                    ]
                }
            ]
        };

        var matchResult = _engine.MatchPlaceholdersWithSchema(placeholders, schema);

        Assert.False(matchResult.IsPerfectMatch);
        Assert.Equal(2, matchResult.MatchedCount);
        Assert.Contains("placeholder_only", matchResult.MissingInSchema);
        Assert.Contains("schema_only", matchResult.MissingInDocx);
    }

    [Fact]
    public void GenerateDraftSchemaFromPlaceholders_InfersFieldTypesCorrectly()
    {
        var placeholders = new List<DocxPlaceholder>
        {
            new() { Name = "ho_va_ten", RawTag = "{{ho_va_ten}}" },
            new() { Name = "ngay_sinh", RawTag = "{{ngay_sinh}}" },
            new() { Name = "email_lien_he", RawTag = "{{email_lien_he}}" },
            new() { Name = "so_dien_thoai", RawTag = "{{so_dien_thoai}}" },
            new() { Name = "so_cccd", RawTag = "{{so_cccd}}" },
            new() { Name = "le_phi", RawTag = "{{le_phi}}" },
            new() { Name = "so_luong_ban_sao", RawTag = "{{so_luong_ban_sao}}" }
        };

        var draftJson = _engine.GenerateDraftSchemaFromPlaceholders(placeholders, "Biểu mẫu mẫu");

        Assert.False(string.IsNullOrWhiteSpace(draftJson));
        Assert.Contains("ho_va_ten", draftJson);
        Assert.Contains("date", draftJson);
        Assert.Contains("email", draftJson);
        Assert.Contains("phone_number", draftJson);
        Assert.Contains("national_id", draftJson);
        Assert.Contains("currency", draftJson);
        Assert.Contains("number", draftJson);
    }
}

using System.IO.Packaging;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.Services.DocumentForm.Infrastructure.OpenXml;

namespace WardMate.Services.DocumentForm.Tests;

/// <summary>
/// Tests cho TASK-12: DocxPlaceholderEngine – bóc tách {{placeholder}} từ file Word DOCX,
/// match với schema fields, và sinh schema nháp tự động.
/// </summary>
public sealed class DocxPlaceholderEngineTests
{
    private readonly DocxPlaceholderEngine _engine = new();

    // ─── Helper: tạo file DOCX in-memory với nội dung tùy ý ──────────────────

    private static byte[] CreateDocx(string bodyText)
    {
        using var ms = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new Document(
                new Body(
                    new Paragraph(new Run(new Text(bodyText)))));
            mainPart.Document.Save();
        }
        return ms.ToArray();
    }

    // ══════════════════════════════════════════════════════════════
    // ExtractPlaceholders(byte[]) – trường hợp thành công
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ExtractPlaceholders_SinglePlaceholder_ReturnsOnePlaceholder()
    {
        var docx = CreateDocx("Xin chào {{ho_ten}}, vui lòng điền thông tin.");
        var result = _engine.ExtractPlaceholders(docx);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("ho_ten", result.Value![0].Name);
    }

    [Fact]
    public void ExtractPlaceholders_MultiplePlaceholders_ReturnsAllUnique()
    {
        var docx = CreateDocx("{{ho_ten}} - {{ngay_sinh}} - {{cccd}} - {{email}}");
        var result = _engine.ExtractPlaceholders(docx);

        Assert.True(result.IsSuccess);
        var names = result.Value!.Select(p => p.Name).ToHashSet();
        Assert.Contains("ho_ten", names);
        Assert.Contains("ngay_sinh", names);
        Assert.Contains("cccd", names);
        Assert.Contains("email", names);
        Assert.Equal(4, result.Value!.Count);
    }

    [Fact]
    public void ExtractPlaceholders_DuplicatePlaceholder_CountsOccurrences()
    {
        // {{ho_ten}} xuất hiện 3 lần → chỉ 1 entry nhưng Occurrences = 3
        var docx = CreateDocx("{{ho_ten}} ... {{ho_ten}} ... {{ho_ten}}");
        var result = _engine.ExtractPlaceholders(docx);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal(3, result.Value![0].Occurrences);
    }

    [Fact]
    public void ExtractPlaceholders_PlaceholderWithSpaces_NormalizesName()
    {
        // {{ ho_va_ten }} → name phải là "ho_va_ten" (trim whitespace)
        var docx = CreateDocx("Họ và tên: {{ ho_va_ten }}");
        var result = _engine.ExtractPlaceholders(docx);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("ho_va_ten", result.Value![0].Name);
    }

    [Fact]
    public void ExtractPlaceholders_PlaceholderNameIsLowercase()
    {
        // {{HO_TEN}} → name phải lowercase
        var docx = CreateDocx("{{HO_TEN}} - {{NGAY_SINH}}");
        var result = _engine.ExtractPlaceholders(docx);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!, p => Assert.Equal(p.Name, p.Name.ToLowerInvariant()));
    }

    [Fact]
    public void ExtractPlaceholders_NoPlaceholders_ReturnsEmptyList()
    {
        var docx = CreateDocx("Đây là văn bản không có placeholder nào cả.");
        var result = _engine.ExtractPlaceholders(docx);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public void ExtractPlaceholders_RawTagPreserved()
    {
        var docx = CreateDocx("Họ tên: {{ho_ten}}");
        var result = _engine.ExtractPlaceholders(docx);

        Assert.True(result.IsSuccess);
        Assert.Equal("{{ho_ten}}", result.Value![0].RawTag);
    }

    // ══════════════════════════════════════════════════════════════
    // ExtractPlaceholders – trường hợp lỗi
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ExtractPlaceholders_EmptyByteArray_ReturnsFail()
    {
        var result = _engine.ExtractPlaceholders(Array.Empty<byte>());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ExtractPlaceholders_NotADocxFile_ReturnsFail()
    {
        var garbage = new byte[] { 0x00, 0xFF, 0xAB, 0xCD };
        var result = _engine.ExtractPlaceholders(garbage);

        Assert.False(result.IsSuccess);
    }

    // ══════════════════════════════════════════════════════════════
    // ExtractPlaceholders(Stream) – trường hợp thành công
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ExtractPlaceholders_Stream_WorksIdenticallyToByteArray()
    {
        var docx = CreateDocx("{{dia_chi}} và {{so_dien_thoai}}");
        using var stream = new MemoryStream(docx);

        var byteResult = _engine.ExtractPlaceholders(docx);
        var streamResult = _engine.ExtractPlaceholders(stream);

        Assert.True(byteResult.IsSuccess);
        Assert.True(streamResult.IsSuccess);
        Assert.Equal(byteResult.Value!.Count, streamResult.Value!.Count);
        Assert.Equal(
            byteResult.Value!.Select(p => p.Name).OrderBy(x => x),
            streamResult.Value!.Select(p => p.Name).OrderBy(x => x));
    }

    // ══════════════════════════════════════════════════════════════
    // MatchPlaceholdersWithSchema
    // ══════════════════════════════════════════════════════════════

    private static FormSchemaDefinition BuildSchema(params string[] fieldIds)
    {
        var fields = fieldIds.Select((id, i) => new FormField
        {
            FieldId = id,
            Label = id,
            Type = FormFieldType.Text,
            Order = i + 1
        }).ToList();

        return new FormSchemaDefinition
        {
            Title = "Test Schema",
            Sections =
            [
                new FormSection
                {
                    SectionId = "sec_1",
                    Title = "Thông tin",
                    Order = 1,
                    Fields = fields.AsReadOnly()
                }
            ]
        };
    }

    [Fact]
    public void MatchPlaceholdersWithSchema_AllMatched_NoMissingOrUnused()
    {
        var docx = CreateDocx("{{ho_ten}} {{ngay_sinh}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;
        var schema = BuildSchema("ho_ten", "ngay_sinh");

        var matchResult = _engine.MatchPlaceholdersWithSchema(placeholders, schema);

        Assert.Equal(2, matchResult.MatchedFields.Count);
        Assert.Empty(matchResult.MissingInSchema);   // tất cả {{}} đều có trong schema
        Assert.Empty(matchResult.UnusedInDocx);       // tất cả field schema đều có trong docx
    }

    [Fact]
    public void MatchPlaceholdersWithSchema_DocxHasExtraTag_ReportsMissingInSchema()
    {
        // DOCX có {{extra_field}} mà schema không định nghĩa
        var docx = CreateDocx("{{ho_ten}} {{extra_field}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;
        var schema = BuildSchema("ho_ten");

        var matchResult = _engine.MatchPlaceholdersWithSchema(placeholders, schema);

        Assert.Single(matchResult.MissingInSchema);
        Assert.Contains("extra_field", matchResult.MissingInSchema);
    }

    [Fact]
    public void MatchPlaceholdersWithSchema_SchemaHasExtraField_ReportsUnusedInDocx()
    {
        // Schema định nghĩa "dia_chi" nhưng DOCX không dùng
        var docx = CreateDocx("{{ho_ten}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;
        var schema = BuildSchema("ho_ten", "dia_chi");

        var matchResult = _engine.MatchPlaceholdersWithSchema(placeholders, schema);

        Assert.Single(matchResult.UnusedInDocx);
        Assert.Contains("dia_chi", matchResult.UnusedInDocx);
    }

    [Fact]
    public void MatchPlaceholdersWithSchema_CaseInsensitiveMatch()
    {
        // {{HO_TEN}} trong DOCX phải khớp với "ho_ten" trong schema
        var docx = CreateDocx("{{HO_TEN}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;
        var schema = BuildSchema("ho_ten");

        var matchResult = _engine.MatchPlaceholdersWithSchema(placeholders, schema);

        Assert.Single(matchResult.MatchedFields);
        Assert.Empty(matchResult.MissingInSchema);
    }

    // ══════════════════════════════════════════════════════════════
    // GenerateDraftSchema
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void GenerateDraftSchema_FromPlaceholders_GeneratesCorrectFieldCount()
    {
        var docx = CreateDocx("{{ho_ten}} {{ngay_sinh}} {{cccd}} {{email}} {{dien_thoai}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;

        var draft = _engine.GenerateDraftSchema(placeholders, "Phiếu thông tin bệnh nhân");

        Assert.Equal("Phiếu thông tin bệnh nhân", draft.Title);
        Assert.Equal(5, draft.GetAllFields().Count());
    }

    [Fact]
    public void GenerateDraftSchema_InfersDateType_ForDateLikeFieldId()
    {
        var docx = CreateDocx("{{ngay_nhap_vien}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;

        var draft = _engine.GenerateDraftSchema(placeholders, "Test");
        var field = draft.GetAllFields().First();

        Assert.Equal(FormFieldType.Date, field.Type);
    }

    [Fact]
    public void GenerateDraftSchema_InfersEmailType()
    {
        var docx = CreateDocx("{{email_lien_he}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;

        var draft = _engine.GenerateDraftSchema(placeholders, "Test");
        Assert.Equal(FormFieldType.Email, draft.GetAllFields().First().Type);
    }

    [Fact]
    public void GenerateDraftSchema_InfersPhoneType()
    {
        var docx = CreateDocx("{{so_dien_thoai}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;

        var draft = _engine.GenerateDraftSchema(placeholders, "Test");
        Assert.Equal(FormFieldType.PhoneNumber, draft.GetAllFields().First().Type);
    }

    [Fact]
    public void GenerateDraftSchema_InfersNationalIdType()
    {
        var docx = CreateDocx("{{so_cccd}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;

        var draft = _engine.GenerateDraftSchema(placeholders, "Test");
        Assert.Equal(FormFieldType.NationalId, draft.GetAllFields().First().Type);
    }

    [Fact]
    public void GenerateDraftSchema_InfersCurrencyType()
    {
        var docx = CreateDocx("{{so_tien_le_phi}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;

        var draft = _engine.GenerateDraftSchema(placeholders, "Test");
        Assert.Equal(FormFieldType.Currency, draft.GetAllFields().First().Type);
    }

    [Fact]
    public void GenerateDraftSchema_DefaultsToText_ForUnknownFieldId()
    {
        var docx = CreateDocx("{{ghi_chu_them}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;

        var draft = _engine.GenerateDraftSchema(placeholders, "Test");
        Assert.Equal(FormFieldType.Text, draft.GetAllFields().First().Type);
    }

    [Fact]
    public void GenerateDraftSchema_FieldsAreRequiredByDefault()
    {
        var docx = CreateDocx("{{ho_ten}} {{dia_chi}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;

        var draft = _engine.GenerateDraftSchema(placeholders, "Test");
        Assert.All(draft.GetAllFields(), f => Assert.True(f.IsRequired));
    }

    [Fact]
    public void GenerateDraftSchema_FormatsLabelFromSnakeCaseFieldId()
    {
        var docx = CreateDocx("{{ho_va_ten}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;

        var draft = _engine.GenerateDraftSchema(placeholders, "Test");
        var label = draft.GetAllFields().First().Label;

        // "ho_va_ten" → "Ho Va Ten"
        Assert.Equal("Ho Va Ten", label);
    }

    [Fact]
    public void GenerateDraftSchema_EmptyTemplateTitle_ThrowsArgumentException()
    {
        var docx = CreateDocx("{{ho_ten}}");
        var placeholders = _engine.ExtractPlaceholders(docx).Value!;

        Assert.Throws<ArgumentException>(() => _engine.GenerateDraftSchema(placeholders, "   "));
    }
}

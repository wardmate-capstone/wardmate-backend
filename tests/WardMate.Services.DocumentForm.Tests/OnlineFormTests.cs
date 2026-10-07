using System.IO.Compression;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WardMate.Services.DocumentForm.Application.Services;
using WardMate.Services.DocumentForm.Domain.Entities;
using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.Services.DocumentForm.Infrastructure.OpenXml;

namespace WardMate.Services.DocumentForm.Tests;

public sealed class OnlineFormTests
{
    private static byte[] Original()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(
                new Paragraph(new Run(new Text("Họ tên: ")),
                    new Run(new RunProperties(new Bold()), new Text("...")),
                    new Run(new Text("...; Ngày: ......"))),
                new Table(new TableProperties(), new TableGrid(new GridColumn()), new TableRow(new TableCell(new Paragraph(new Run(new Text("Nội dung: ......"))))))));
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new Header(new Paragraph(new Run(new Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM"))));
            main.Document.Body!.Append(new SectionProperties(new HeaderReference
                { Id = main.GetIdOfPart(header), Type = HeaderFooterValues.Default }));
        }
        return stream.ToArray();
    }

    [Fact]
    public async Task Upload_PreservesAllBytesAndRejectsInvalidFiles()
    {
        var bytes = Original();
        var result = await DocxUpload.ReadAsync(new MemoryStream(bytes), "official.docx", bytes.Length, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(bytes, result.Value);
        Assert.False((await DocxUpload.ReadAsync(new MemoryStream(bytes), "official.txt", bytes.Length, default)).IsSuccess);
        Assert.False((await DocxUpload.ReadAsync(new MemoryStream([1, 2, 3]), "bad.docx", 3, default)).IsSuccess);
        Assert.False((await DocxUpload.ReadAsync(new MemoryStream(), "empty.docx", 0, default)).IsSuccess);
        Assert.False((await DocxUpload.ReadAsync(new MemoryStream(bytes), "large.docx", DocxUpload.MaxBytes + 1, default)).IsSuccess);
    }

    [Fact]
    public void Fill_AppendsDataCorrectly()
    {
        var original = Original();
        var before = original.ToArray();
        var engine = new DocxFormEngine();
        var schema = "{\"sections\":[{\"fields\":[{\"field_id\":\"ho_ten\",\"label\":\"Họ tên\"},{\"field_id\":\"ngay_sinh\",\"label\":\"Ngày sinh\"}]}]}";
        
        var output = engine.Fill(original, schema, new Dictionary<string, string>
        { ["ho_ten"] = "Nguyễn Văn A", ["ngay_sinh"] = "2000-01-02", ["noi_dung"] = "Dòng một\nDòng hai" });
        
        Assert.Equal(before, original); // Doesn't mutate original array
        
        using var doc = WordprocessingDocument.Open(new MemoryStream(output), false);
        var body = doc.MainDocumentPart!.Document.Body!;
        
        var allText = string.Join("", body.Descendants<Text>().Select(t => t.Text));
        Assert.Contains("DỮ LIỆU KHAI BÁO TRỰC TUYẾN", allText);
        Assert.Contains("Họ tên: Nguyễn Văn A", allText);
        Assert.Contains("Ngày sinh: 2000-01-02", allText);
        Assert.Contains("noi_dung: Dòng một", allText);
        Assert.IsType<SectionProperties>(body.LastChild);
        Assert.Contains(body.Descendants<Break>(), b => b.Type is null);
        Assert.Empty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(doc));
    }

    [Fact]
    public void Draft_AllowsMissingRequiredButSubmitRejectsIt()
    {
        var engine = new FormSchemaEngine();
        var schema = new FormSchemaDefinition { Title = "Đơn", Sections = [new FormSection
        { SectionId = "main", Title = "Thông tin", Fields = [new FormField
        { FieldId = "ho_ten", Label = "Họ tên", IsRequired = true }] }] };
        Assert.Empty(engine.ValidateFormData(schema, "{}", requireComplete: false).Value);
        Assert.Single(engine.ValidateFormData(schema, "{}").Value);
        Assert.False(engine.ValidateFormData(schema, "{\"unknown\":1}", false).IsSuccess);
        Assert.False(engine.ValidateFormData(schema, "{\"ho_ten\":{},\"ho_ten\":1}", false).IsSuccess);
    }

    [Fact]
    public void Submission_PinsVersionAndRejectsEditsAfterSubmit()
    {
        var draft = new UserSubmission(Guid.NewGuid(), Guid.NewGuid(), "https://example.test/a.docx", "a.docx", 10);
        var version = Guid.NewGuid();
        draft.SetOnlineData(version, "{\"ho_ten\":\"A\"}");
        Assert.Throws<InvalidOperationException>(() => draft.SetOnlineData(Guid.NewGuid(), "{}"));
        draft.Submit();
        Assert.Throws<InvalidOperationException>(() => draft.SetOnlineData(version, "{}"));
    }

    [Fact]
    public void BlobName_WorksForAzuriteAndAzure()
    {
        Assert.Equal("form-templates/a.docx", OnlineFormSupport.BlobName("http://localhost:10000/devstoreaccount1/form-templates/form-templates/a.docx"));
        Assert.Equal("form-templates/a.docx", OnlineFormSupport.BlobName("https://example.blob.core.windows.net/form-templates/form-templates/a.docx"));
    }
}

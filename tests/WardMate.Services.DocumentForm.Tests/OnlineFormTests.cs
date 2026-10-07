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
                new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("Nội dung: ......"))))))));
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new Header(new Paragraph(new Run(new Text("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM"))));
            main.Document.Body!.Append(new SectionProperties(new HeaderReference
                { Id = main.GetIdOfPart(header), Type = HeaderFooterValues.Default }));
        }
        return stream.ToArray();
    }

    private static DocxFieldMapping NameMapping => new()
    { FieldId = "ho_ten", ParagraphIndex = 0, Start = 8, Length = 6, ExpectedText = "......" };

    [Fact]
    public void Fill_PreservesOriginalPackageAndFormattingAcrossRunsAndTable()
    {
        var original = Original();
        var before = original.ToArray();
        var engine = new DocxFormEngine();
        var mapping = new[] { NameMapping,
            new DocxFieldMapping { FieldId = "ngay_sinh", ParagraphIndex = 0, Start = 22, Length = 6, ExpectedText = "......" },
            new DocxFieldMapping { FieldId = "noi_dung", ParagraphIndex = 1, Start = 10, Length = 6, ExpectedText = "......" } };
        var output = engine.Fill(original, mapping, new Dictionary<string, string>
        { ["ho_ten"] = "Nguyễn Văn A", ["ngay_sinh"] = "2000-01-02", ["noi_dung"] = "Dòng một\nDòng hai" });
        Assert.Equal(before, original);
        Assert.Equal("Họ tên: Nguyễn Văn A; Ngày: 2000-01-02", engine.Inspect(output)[0].Text);
        Assert.Equal("Nội dung: Dòng một\nDòng hai", engine.Inspect(output)[1].Text);
        using var doc = WordprocessingDocument.Open(new MemoryStream(output), false);
        Assert.Single(doc.MainDocumentPart!.Document.Descendants<Table>());
        Assert.Contains(doc.MainDocumentPart.Document.Descendants<Run>(),
            r => r.InnerText.Contains("Nguyễn Văn A") && r.RunProperties?.Bold is not null);
        using var sourceZip = new ZipArchive(new MemoryStream(original));
        using var outputZip = new ZipArchive(new MemoryStream(output));
        Assert.Equal(sourceZip.Entries.Select(e => e.FullName).Order(), outputZip.Entries.Select(e => e.FullName).Order());
        foreach (var entry in sourceZip.Entries.Where(e => e.FullName != "word/document.xml"))
        {
            using var first = new MemoryStream(); using var second = new MemoryStream();
            using var a = entry.Open(); using var b = outputZip.GetEntry(entry.FullName)!.Open();
            a.CopyTo(first); b.CopyTo(second);
            Assert.Equal(first.ToArray(), second.ToArray());
        }
    }

    [Fact]
    public void Fill_RejectsStaleAndOverlappingMappings()
    {
        var engine = new DocxFormEngine();
        Assert.Throws<InvalidDataException>(() => engine.Fill(Original(),
            [NameMapping with { ExpectedText = "wrong!" }], new Dictionary<string, string>()));
        Assert.Throws<InvalidDataException>(() => engine.Fill(Original(),
            [NameMapping, NameMapping], new Dictionary<string, string>()));
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

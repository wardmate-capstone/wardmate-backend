using System.IO.Compression;
using System.Text;
using WardMate.Services.DocumentForm.Application.Services;

namespace WardMate.UnitTests.DocumentForm;

public sealed class DocxUploadTests
{
    private static byte[] CreateValidDocx()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            // [Content_Types].xml
            var contentTypes = zip.CreateEntry("[Content_Types].xml");
            using (var stream = contentTypes.Open())
            {
                var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                    "</Types>";
                stream.Write(Encoding.UTF8.GetBytes(xml));
            }

            // _rels/.rels
            var rels = zip.CreateEntry("_rels/.rels");
            using (var stream = rels.Open())
            {
                var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>" +
                    "</Relationships>";
                stream.Write(Encoding.UTF8.GetBytes(xml));
            }

            // word/_rels/document.xml.rels
            var wordRels = zip.CreateEntry("word/_rels/document.xml.rels");
            using (var stream = wordRels.Open())
            {
                var xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "</Relationships>";
                stream.Write(Encoding.UTF8.GetBytes(xml));
            }

            // word/document.xml
            var doc = zip.CreateEntry("word/document.xml");
            using (var stream = doc.Open())
            {
                var xml = "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>" +
                    "<w:document xmlns:wpc=\"http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas\" " +
                    "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
                    "<w:body><w:p><w:r><w:t>Biểu mẫu test</w:t></w:r></w:p>" +
                    "<w:sectPr/></w:body></w:document>";
                stream.Write(Encoding.UTF8.GetBytes(xml));
            }
        }
        return ms.ToArray();
    }

    private static byte[] CreateValidDoc()
    {
        var bytes = new byte[512];
        byte[] header = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
        Array.Copy(header, bytes, header.Length);
        byte[] wordDoc = Encoding.Unicode.GetBytes("WordDocument");
        Array.Copy(wordDoc, 0, bytes, 64, wordDoc.Length);
        return bytes;
    }

    private static byte[] CreateDocWithMacro()
    {
        var bytes = CreateValidDoc();
        byte[] vba = Encoding.Unicode.GetBytes("_VBA_PROJECT");
        Array.Copy(vba, 0, bytes, 128, vba.Length);
        return bytes;
    }

    [Fact]
    public async Task ReadAsync_ValidDocx_Succeeds()
    {
        var bytes = CreateValidDocx();
        using var stream = new MemoryStream(bytes);
        var result = await DocxUpload.ReadAsync(stream, "mau.docx", bytes.Length, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(bytes.Length, result.Value.Length);
    }

    [Fact]
    public async Task ReadAsync_ValidDoc_Succeeds()
    {
        var bytes = CreateValidDoc();
        using var stream = new MemoryStream(bytes);
        var result = await DocxUpload.ReadAsync(stream, "mau.doc", bytes.Length, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(bytes.Length, result.Value.Length);
    }

    [Fact]
    public async Task ReadAsync_DocWithMacro_FailsValidation()
    {
        var bytes = CreateDocWithMacro();
        using var stream = new MemoryStream(bytes);
        var result = await DocxUpload.ReadAsync(stream, "mau-macro.doc", bytes.Length, default);
        Assert.False(result.IsSuccess);
        Assert.Equal("document.invalid_docx_file", result.Error.Code);
    }

    [Fact]
    public async Task ReadAsync_CorruptedDoc_FailsValidation()
    {
        var bytes = Encoding.UTF8.GetBytes("Not a valid OLE2 doc file content.");
        using var stream = new MemoryStream(bytes);
        var result = await DocxUpload.ReadAsync(stream, "corrupt.doc", bytes.Length, default);
        Assert.False(result.IsSuccess);
        Assert.Equal("document.invalid_docx_file", result.Error.Code);
    }

    [Fact]
    public async Task ReadAsync_UnsupportedExtension_FailsValidation()
    {
        var bytes = new byte[] { 1, 2, 3 };
        using var stream = new MemoryStream(bytes);
        var result = await DocxUpload.ReadAsync(stream, "test.pdf", bytes.Length, default);
        Assert.False(result.IsSuccess);
        Assert.Equal("document.invalid_docx_file", result.Error.Code);
        Assert.Contains(".doc", result.Error.Description);
    }

    [Fact]
    public async Task ReadAsync_EmptyFile_ReturnsEmptyFileError()
    {
        using var stream = new MemoryStream();
        var result = await DocxUpload.ReadAsync(stream, "empty.doc", 0, default);
        Assert.False(result.IsSuccess);
        Assert.Equal("document.empty_file", result.Error.Code);
    }
}

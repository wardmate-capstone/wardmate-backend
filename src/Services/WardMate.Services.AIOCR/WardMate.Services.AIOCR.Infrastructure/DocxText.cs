using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace WardMate.Services.AIOCR.Infrastructure;

// Plain text of a filled DOCX form for the dossier review: paragraphs line by line, table rows as "a | b | c",
// content controls unwrapped. Comments, headers and footers are left out. Returns null when the file is not a DOCX.
public static class DocxText
{
    public const int MaxChars = 30000;

    public static string? Extract(byte[] bytes, out bool truncated)
    {
        truncated = false;
        if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4B || bytes[2] != 3 || bytes[3] != 4) return null;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var document = WordprocessingDocument.Open(stream, false);
            if (document.MainDocumentPart?.Document?.Body is not { } body) return null;
            var text = new StringBuilder();
            Append(body, text);
            if (text.Length > MaxChars) { truncated = true; text.Length = MaxChars; }
            return text.ToString().Trim();
        }
        catch (Exception error) when (error is OpenXmlPackageException or InvalidDataException or FileFormatException or InvalidOperationException)
        {
            return null;
        }
    }

    private static void Append(OpenXmlElement container, StringBuilder text)
    {
        foreach (var element in container.Elements())
        {
            if (text.Length > MaxChars) return;
            switch (element)
            {
                case Paragraph paragraph:
                    var line = paragraph.InnerText.Trim();
                    if (line.Length > 0) text.AppendLine(line);
                    break;
                case Table table:
                    foreach (var row in table.Elements<TableRow>())
                    {
                        var cells = row.Elements<TableCell>().Select(c => string.Join(' ', c.Elements<Paragraph>().Select(p => p.InnerText.Trim())).Trim());
                        var joined = string.Join(" | ", cells);
                        if (joined.Replace("|", "").Trim().Length > 0) text.AppendLine(joined);
                    }
                    break;
                case SdtBlock block when block.SdtContentBlock is { } content:
                    Append(content, text);
                    break;
            }
        }
    }
}

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Models;

namespace WardMate.Services.DocumentForm.Infrastructure.OpenXml;

public sealed class DocxFormEngine : IDocxFormEngine
{
    private static void CheckPackage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, false);
        using var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        if (zip.Entries.Count > 5000 || zip.Entries.Sum(e => e.Length) > 100L * 1024 * 1024)
            throw new InvalidDataException("DOCX package is too large when expanded.");
    }
    private static List<OpenXmlElement> Tokens(Paragraph p) => p.Descendants()
        .Where(t => (t is Text or TabChar or Break) && t.Ancestors<Paragraph>().First() == p).ToList();
    private static string TokenText(OpenXmlElement e) => e is Text t ? t.Text : e is TabChar ? "\t" : "\n";
    private static List<Paragraph> Paragraphs(WordprocessingDocument doc)
    {
        var body = doc.MainDocumentPart?.Document.Body
            ?? throw new InvalidDataException("DOCX must contain a document body.");
        return body.Descendants<Paragraph>().ToList();
    }

    public IReadOnlyList<DocxParagraph> Inspect(byte[] original)
    {
        CheckPackage(original);
        using var stream = new MemoryStream(original, writable: false);
        using var doc = WordprocessingDocument.Open(stream, false);
        return Paragraphs(doc).Select((p, i) => new DocxParagraph(i,
            string.Concat(Tokens(p).Select(TokenText)))).ToList();
    }

    public byte[] Fill(byte[] original, IReadOnlyList<DocxFieldMapping> mappings,
        IReadOnlyDictionary<string, string> values)
    {
        CheckPackage(original);
        using var stream = new MemoryStream();
        stream.Write(original);
        stream.Position = 0;
        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            var paragraphs = Paragraphs(doc);
            foreach (var group in mappings.GroupBy(m => m.ParagraphIndex))
            {
                if (group.Key < 0 || group.Key >= paragraphs.Count)
                    throw new InvalidDataException("Mapping paragraph does not exist.");
                var paragraph = paragraphs[group.Key];
                // Reject mappings across non-text Word constructs rather than silently corrupting them.
                if (paragraph.Descendants<FieldChar>().Any() || paragraph.Descendants<SimpleField>().Any()
                    || paragraph.Descendants<DeletedText>().Any() || paragraph.Descendants<Paragraph>().Any())
                    throw new InvalidDataException("Mapped paragraph contains fields, revisions or nested paragraphs; choose a plain-text paragraph.");
                var text = string.Concat(Tokens(paragraph).Select(TokenText));
                var end = -1;
                foreach (var m in group.OrderBy(m => m.Start))
                {
                    if (m.Length <= 0 || m.Start < 0 || m.Start > text.Length - m.Length
                        || m.Start < end || m.ExpectedText.Contains('\t') || m.ExpectedText.Contains('\n') || m.ExpectedText != text.Substring(m.Start, m.Length))
                        throw new InvalidDataException("Mapping is overlapping or does not match the original DOCX text.");
                    end = m.Start + m.Length;
                }
                // Descending offsets keep earlier mappings stable even when replacement length differs.
                foreach (var m in group.OrderByDescending(m => m.Start))
                {
                    if (!values.TryGetValue(m.FieldId, out var value) || string.IsNullOrEmpty(value))
                        continue; // Incomplete drafts retain original blank/dotted regions.
                    var nodes = Tokens(paragraph);
                    var offset = 0;
                    var inserted = false;
                    foreach (var token in nodes)
                    {
                        var old = TokenText(token);
                        if (token is not Text node) { offset += old.Length; continue; }
                        var from = Math.Max(m.Start - offset, 0);
                        var to = Math.Min(m.Start + m.Length - offset, old.Length);
                        if (from < to)
                        {
                            var replacement = (old[..from] + (inserted ? "" : value) + old[to..]).Replace("\r\n", "\n").Replace('\r', '\n');
                            var lines = replacement.Split('\n');
                            node.Text = lines[0];
                            OpenXmlElement last = node;
                            foreach (var line in lines.Skip(1))
                            {
                                var br = new Break(); last.InsertAfterSelf(br);
                                var next = new Text(line) { Space = SpaceProcessingModeValues.Preserve };
                                br.InsertAfterSelf(next); last = next;
                            }
                            node.Space = SpaceProcessingModeValues.Preserve;
                            inserted = true;
                        }
                        offset += old.Length;
                    }
                }
            }
            doc.MainDocumentPart!.Document.Save();
        }
        return stream.ToArray();
    }
}

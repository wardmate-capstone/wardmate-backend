using DocumentFormat.OpenXml;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Application.Models;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.Infrastructure.OpenXml;

/// <summary>
/// Triển khai engine bóc tách placeholder từ file Word DOCX bằng OpenXML SDK.
/// Nhận dạng placeholder theo cú pháp: {{field_id}}
/// </summary>
public sealed class DocxPlaceholderEngine : IDocxPlaceholderEngine
{
    // Nhận diện dạng {{ho_va_ten}} hoặc {{ ho_va_ten }}
    private static readonly Regex PlaceholderRegex =
        new(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}", RegexOptions.Compiled | RegexOptions.Multiline);

    public Result<IReadOnlyList<DocxPlaceholder>> ExtractPlaceholders(Stream docxStream)
    {
        try
        {
            using var ms = new MemoryStream();
            docxStream.CopyTo(ms);
            return ExtractPlaceholders(ms.ToArray());
        }
        catch (Exception ex)
        {
            return DocumentFormErrors.InvalidDocxFile($"Failed to read DOCX stream: {ex.Message}");
        }
    }

    public Result<IReadOnlyList<DocxPlaceholder>> ExtractPlaceholders(byte[] docxBytes)
    {
        if (docxBytes.Length == 0)
        {
            return DocumentFormErrors.EmptyFile;
        }

        try
        {
            using var ms = new MemoryStream(docxBytes, writable: false);
            using var wordDoc = WordprocessingDocument.Open(ms, isEditable: false);

            var body = wordDoc.MainDocumentPart?.Document?.Body;
            if (body is null)
            {
                return DocumentFormErrors.InvalidDocxFile("Cannot read document body. The file may be corrupted or password-protected.");
            }

            // Ghép toàn bộ văn bản trong document body (kể cả text bị phân mảnh bởi định dạng)
            var allText = ExtractFullText(body);

            // Ghép thêm text từ headers và footers nếu có
            var headersFootersText = ExtractHeadersFootersText(wordDoc.MainDocumentPart);

            var combined = allText + "\n" + headersFootersText;
            var result = ParsePlaceholders(combined);

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            return DocumentFormErrors.InvalidDocxFile($"Cannot parse DOCX file: {ex.Message}");
        }
    }

    public PlaceholderValidationResult MatchPlaceholdersWithSchema(
        IReadOnlyList<DocxPlaceholder> placeholders,
        FormSchemaDefinition schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var schemaFieldIds = schema.GetAllFields()
            .Select(f => f.FieldId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var docxFieldIds = placeholders
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var matched = schemaFieldIds.Intersect(docxFieldIds, StringComparer.OrdinalIgnoreCase).ToList();
        var missingInSchema = docxFieldIds.Except(schemaFieldIds, StringComparer.OrdinalIgnoreCase).ToList();
        var unusedInDocx = schemaFieldIds.Except(docxFieldIds, StringComparer.OrdinalIgnoreCase).ToList();

        return new PlaceholderValidationResult
        {
            MatchedFields = matched.AsReadOnly(),
            MissingInSchema = missingInSchema.AsReadOnly(),
            UnusedInDocx = unusedInDocx.AsReadOnly()
        };
    }

    public FormSchemaDefinition GenerateDraftSchema(IReadOnlyList<DocxPlaceholder> placeholders, string templateTitle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateTitle);

        var fields = placeholders
            .OrderBy(p => p.Name)
            .Select((p, index) => new FormField
            {
                FieldId = p.Name,
                Label = FormatLabelFromFieldId(p.Name),
                Type = InferFieldType(p.Name),
                IsRequired = true,
                Order = index + 1,
                HelpText = $"Extracted from DOCX tag: {p.RawTag}"
            }).ToList();

        var section = new FormSection
        {
            SectionId = "section_1",
            Title = "Thông tin chính",
            Order = 1,
            Fields = fields.AsReadOnly()
        };

        return new FormSchemaDefinition
        {
            Title = templateTitle,
            Description = $"Schema tự động sinh từ file DOCX — {placeholders.Count} trường",
            Version = 1,
            Sections = [section]
        };
    }

    // ─── Fill placeholders ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Stream FillPlaceholders(Stream templateStream, IReadOnlyDictionary<string, string> formData)
    {
        ArgumentNullException.ThrowIfNull(templateStream);
        ArgumentNullException.ThrowIfNull(formData);

        // Copy template into a writable MemoryStream
        var ms = new MemoryStream();
        templateStream.CopyTo(ms);
        ms.Position = 0;

        using (var wordDoc = WordprocessingDocument.Open(ms, isEditable: true))
        {
            var mainPart = wordDoc.MainDocumentPart
                ?? throw new InvalidOperationException("DOCX has no main document part.");

            // Fill body
            FillInElement(mainPart.Document.Body!, formData);

            // Fill headers and footers
            foreach (var headerPart in mainPart.HeaderParts)
                FillInElement(headerPart.Header, formData);
            foreach (var footerPart in mainPart.FooterParts)
                FillInElement(footerPart.Footer, formData);

            mainPart.Document.Save();
        }

        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// Duyệt qua tất cả Paragraph trong element và điền giá trị vào placeholder.
    /// </summary>
    private static void FillInElement(OpenXmlElement root, IReadOnlyDictionary<string, string> formData)
    {
        foreach (var para in root.Descendants<Paragraph>().ToList())
            NormalizeAndFillParagraph(para, formData);
    }

    /// <summary>
    /// Với mỗi đoạn văn (Paragraph): ghép toàn bộ text từ các Run,
    /// thay thế placeholders trong chuỗi đã ghép, rồi ghi lại vào Run đầu tiên.
    /// Cách này xử lý được trường hợp placeholder bị Word tự phân mảnh sang nhiều Run.
    /// </summary>
    private static void NormalizeAndFillParagraph(Paragraph para, IReadOnlyDictionary<string, string> formData)
    {
        var runs = para.Elements<Run>().ToList();
        if (runs.Count == 0) return;

        // Ghép toàn bộ text trong paragraph
        var fullText = string.Concat(
            runs.SelectMany(r => r.Elements<Text>()).Select(t => t.Text));

        if (!fullText.Contains("{{")) return;

        // Thay thế từng placeholder (case-insensitive)
        foreach (var (key, value) in formData)
        {
            fullText = Regex.Replace(
                fullText,
                @"\{\{\s*" + Regex.Escape(key) + @"\s*\}\}",
                value ?? string.Empty,
                RegexOptions.IgnoreCase);
        }

        // Ghi text đã thay thế vào Run đầu tiên
        var firstRun = runs[0];
        var existingTexts = firstRun.Elements<Text>().ToList();

        Text firstText;
        if (existingTexts.Count > 0)
        {
            firstText = existingTexts[0];
            foreach (var extra in existingTexts.Skip(1)) extra.Remove();
        }
        else
        {
            firstText = new Text();
            firstRun.AppendChild(firstText);
        }

        firstText.Text = fullText;
        firstText.Space = SpaceProcessingModeValues.Preserve;

        // Xoá text trong các Run còn lại (giữ nguyên Run để không mất formatting)
        foreach (var run in runs.Skip(1))
            foreach (var t in run.Elements<Text>().ToList())
                t.Remove();
    }

    // ─── Private helpers ───────────────────────────────────────────────────────


    private static string ExtractFullText(Body body)
    {
        var sb = new StringBuilder();
        foreach (var para in body.Descendants<Paragraph>())
        {
            // Ghép tất cả Run/Text (OpenXML thường phân mảnh text bởi spell check / format markers)
            var runTexts = para.Descendants<Text>().Select(t => t.Text);
            sb.AppendLine(string.Concat(runTexts));
        }
        return sb.ToString();
    }

    private static string ExtractHeadersFootersText(MainDocumentPart? mainPart)
    {
        if (mainPart is null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var headerPart in mainPart.HeaderParts)
        {
            sb.AppendLine(string.Concat(
                headerPart.Header.Descendants<Text>().Select(t => t.Text)));
        }

        foreach (var footerPart in mainPart.FooterParts)
        {
            sb.AppendLine(string.Concat(
                footerPart.Footer.Descendants<Text>().Select(t => t.Text)));
        }

        return sb.ToString();
    }

    private static IReadOnlyList<DocxPlaceholder> ParsePlaceholders(string text)
    {
        var occurrences = new Dictionary<string, (int Count, List<string> Locations, string RawTag)>(
            StringComparer.OrdinalIgnoreCase);

        var lines = text.Split('\n');
        for (var lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];
            foreach (Match match in PlaceholderRegex.Matches(line))
            {
                var rawTag = match.Value;
                var name = match.Groups[1].Value.Trim().ToLowerInvariant();
                var location = $"Line {lineIdx + 1}, Col {match.Index + 1}";

                if (!occurrences.TryGetValue(name, out var existing))
                {
                    occurrences[name] = (1, [location], rawTag);
                }
                else
                {
                    existing.Locations.Add(location);
                    occurrences[name] = (existing.Count + 1, existing.Locations, existing.RawTag);
                }
            }
        }

        return occurrences.Select(kv => new DocxPlaceholder
        {
            Name = kv.Key,
            RawTag = kv.Value.RawTag,
            Occurrences = kv.Value.Count,
            Locations = kv.Value.Locations.AsReadOnly()
        }).OrderBy(p => p.Name).ToList().AsReadOnly();
    }

    private static string FormatLabelFromFieldId(string fieldId)
    {
        // "ho_va_ten" → "Ho Va Ten"
        return string.Join(" ", fieldId.Split('_')
            .Select(w => char.ToUpper(w[0]) + w[1..]));
    }

    private static FormFieldType InferFieldType(string fieldId)
    {
        var lower = fieldId.ToLowerInvariant();

        if (lower.Contains("ngay") || lower.Contains("date") || lower.Contains("sinh"))
        {
            return FormFieldType.Date;
        }

        if (lower.Contains("email"))
        {
            return FormFieldType.Email;
        }

        if (lower.Contains("dien_thoai") || lower.Contains("phone") || lower.Contains("sdt"))
        {
            return FormFieldType.PhoneNumber;
        }

        if (lower.Contains("cccd") || lower.Contains("cmnd") || lower.Contains("can_cuoc") || lower.Contains("so_dinh_danh"))
        {
            return FormFieldType.NationalId;
        }

        if (lower.Contains("so_tien") || lower.Contains("phi") || lower.Contains("le_phi"))
        {
            return FormFieldType.Currency;
        }

        if (lower.Contains("so_luong") || lower.Contains("dien_tich") || lower.Contains("so_tang"))
        {
            return FormFieldType.Number;
        }

        return FormFieldType.Text;
    }
}

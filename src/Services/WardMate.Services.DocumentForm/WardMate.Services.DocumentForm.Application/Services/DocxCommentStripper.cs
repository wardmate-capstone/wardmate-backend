using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace WardMate.Services.DocumentForm.Application.Services;

/// <summary>
/// Xóa toàn bộ comment (Word annotations) khỏi file DOCX trước khi render sang PDF.
/// Giúp file PDF xuất ra cho người dân in và nộp không chứa ghi chú nội bộ của cán bộ.
/// </summary>
public static class DocxCommentStripper
{
    /// <summary>
    /// Nhận một DOCX stream (có thể chứa comment của cán bộ),
    /// trả về MemoryStream của DOCX đã được xóa toàn bộ comment.
    /// Stream đầu ra đã được đặt Position = 0, sẵn sàng để đọc.
    /// </summary>
    public static MemoryStream Strip(Stream docxStream)
    {
        // Copy toàn bộ nội dung vào MemoryStream để WordprocessingDocument có thể ghi
        var ms = new MemoryStream();
        docxStream.CopyTo(ms);
        ms.Position = 0;

        using (var wordDoc = WordprocessingDocument.Open(ms, isEditable: true, new OpenSettings { AutoSave = false }))
        {
            var mainPart = wordDoc.MainDocumentPart;
            if (mainPart is null)
            {
                ms.Position = 0;
                return ms;
            }

            // 1. Xóa toàn bộ phần comments (WordprocessingCommentsPart)
            if (mainPart.WordprocessingCommentsPart is not null)
            {
                mainPart.DeletePart(mainPart.WordprocessingCommentsPart);
            }

            // 2. Xóa các markup inline trong body: CommentRangeStart, CommentRangeEnd, CommentReference
            var body = mainPart.Document?.Body;
            if (body is not null)
            {
                // Xóa CommentRangeStart — đánh dấu đầu vùng được comment
                foreach (var el in body.Descendants<CommentRangeStart>().ToList())
                    el.Remove();

                // Xóa CommentRangeEnd — đánh dấu cuối vùng được comment
                foreach (var el in body.Descendants<CommentRangeEnd>().ToList())
                    el.Remove();

                // Xóa CommentReference — ký hiệu số tham chiếu tới comment
                foreach (var el in body.Descendants<CommentReference>().ToList())
                    el.Remove();

                // 3. Xóa các Run chỉ chứa CommentReference (run rỗng sau khi xóa)
                foreach (var run in body.Descendants<Run>().ToList())
                {
                    if (!run.HasChildren)
                        run.Remove();
                }
            }

            // 4. Xử lý header và footer nếu có comment ở đó
            foreach (var headerPart in mainPart.HeaderParts)
            {
                RemoveCommentsFromBody(headerPart.Header);
            }
            foreach (var footerPart in mainPart.FooterParts)
            {
                RemoveCommentsFromBody(footerPart.Footer);
            }

            wordDoc.Save();
        }

        ms.Position = 0;
        return ms;
    }

    private static void RemoveCommentsFromBody(DocumentFormat.OpenXml.OpenXmlElement? element)
    {
        if (element is null) return;
        foreach (var el in element.Descendants<CommentRangeStart>().ToList()) el.Remove();
        foreach (var el in element.Descendants<CommentRangeEnd>().ToList()) el.Remove();
        foreach (var el in element.Descendants<CommentReference>().ToList()) el.Remove();
    }
}

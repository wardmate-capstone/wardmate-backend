using System.Text.RegularExpressions;

namespace WardMate.Services.AIOCR.Application;

/// <summary>Đoạn của tài liệu khớp nhất, chỉ phần mô tả (chưa có nội dung) để chọn thêm nguồn.</summary>
public sealed record SectionChunk(Guid ChunkId, int ChunkIndex, string? Heading, int CharCount);

/// <summary>
/// CHAT-CONTEXT-01: câu hỏi gộp nhiều ý ("cần giấy tờ gì, lệ phí bao nhiêu, mất bao lâu") làm các đoạn phí/thời hạn chiếm
/// hết TopK nên đoạn "Thành phần hồ sơ" bị loại, hoặc chỉ lấy được nửa sau của danh sách giấy tờ. Sau khi xếp hạng, với tài liệu
/// khớp nhất: (1) lấy đủ mọi đoạn của mục mà kết quả đã chạm tới; (2) thêm mục ứng với từng ý người dân hỏi.
/// Không gọi thêm AI; giới hạn số đoạn và số ký tự thêm vào.
/// </summary>
public static partial class KnowledgeContext
{
    public const int MaxExtraChunks = 6;
    public const int MaxExtraChars = 9000;

    [Flags]
    public enum Aspect { None = 0, Documents = 1, Fee = 2, Time = 4, Steps = 8 }

    public static Aspect Aspects(string query)
    {
        var q = query.ToLowerInvariant();
        var a = Aspect.None;
        if (DocumentsWords().IsMatch(q)) a |= Aspect.Documents;
        if (FeeWords().IsMatch(q)) a |= Aspect.Fee;
        if (TimeWords().IsMatch(q)) a |= Aspect.Time;
        if (StepsWords().IsMatch(q)) a |= Aspect.Steps;
        return a;
    }

    /// <summary>
    /// Trả mã các đoạn cần thêm (theo thứ tự trong tài liệu), không lặp đoạn đã có trong <paramref name="hits"/>.
    /// <paramref name="mainChunks"/> là các đoạn tìm kiếm được của tài liệu xếp đầu (hits[0]).
    /// </summary>
    public static Guid[] Expand(string query, IReadOnlyList<KnowledgeHit> hits, IReadOnlyList<SectionChunk> mainChunks)
    {
        if (hits.Count == 0 || mainChunks.Count == 0) return [];
        var main = hits[0].DocumentId;
        var have = hits.Select(h => h.ChunkId).ToHashSet();
        var sections = hits.Where(h => h.DocumentId == main && !string.IsNullOrWhiteSpace(h.Heading))
            .Select(h => h.Heading!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var aspects = Aspects(query);
        var first = mainChunks.Min(c => c.ChunkIndex);

        bool Wanted(SectionChunk c)
        {
            if (c.Heading is { } h && sections.Contains(h)) return true; // cả mục mà kết quả đã chạm tới
            var heading = c.Heading ?? "";
            if (aspects.HasFlag(Aspect.Documents) && heading.Contains("Thành phần hồ sơ", StringComparison.OrdinalIgnoreCase)) return true;
            if ((aspects & (Aspect.Fee | Aspect.Time)) != 0
                && (c.ChunkIndex == first || heading.Contains("Cách thức thực hiện", StringComparison.OrdinalIgnoreCase))) return true;
            if (aspects.HasFlag(Aspect.Steps) && heading.Contains("Trình tự thực hiện", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        var extra = new List<Guid>();
        var chars = 0;
        foreach (var c in mainChunks.Where(c => !have.Contains(c.ChunkId) && Wanted(c)).OrderBy(c => c.ChunkIndex))
        {
            if (extra.Count >= MaxExtraChunks || chars + c.CharCount > MaxExtraChars) break;
            extra.Add(c.ChunkId); chars += c.CharCount;
        }
        return extra.ToArray();
    }

    [GeneratedRegex(@"giấy tờ|giay to|hồ sơ|ho so|thành phần|thanh phan|chuẩn bị|chuan bi|cần mang|can mang|cần những gì|can nhung gi")] private static partial Regex DocumentsWords();
    [GeneratedRegex(@"lệ phí|le phi|\bphí\b|\bphi\b|bao nhiêu tiền|bao nhieu tien|chi phí|chi phi|mất tiền|mat tien")] private static partial Regex FeeWords();
    [GeneratedRegex(@"bao lâu|bao lau|thời hạn|thoi han|thời gian|thoi gian|mấy ngày|may ngay|khi nào có|khi nao co|có kết quả|co ket qua")] private static partial Regex TimeWords();
    [GeneratedRegex(@"trình tự|trinh tu|các bước|cac buoc|làm thế nào|lam the nao|nộp ở đâu|nop o dau|thực hiện như|thuc hien nhu")] private static partial Regex StepsWords();
}

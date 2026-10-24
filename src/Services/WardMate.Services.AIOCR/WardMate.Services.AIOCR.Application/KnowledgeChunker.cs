using System.Text;
using System.Text.RegularExpressions;

namespace WardMate.Services.AIOCR.Application;

public sealed record ChunkDraft(int Index, string? Heading, string Content);

// Deterministic paragraph-first chunking: same text and version always yield the same chunks.
public static partial class KnowledgeChunker
{
    public const int TargetChars = 1200;
    public const int MaxChars = 1500;
    public const int OverlapChars = 200;
    private const int PieceChars = 900;     // long paragraphs are cut to this size before packing
    private const int MinOwnChars = 300;    // a chunk shorter than this absorbs the next piece instead of being cut
    public const int MaxChunks = 500;
    public const int MaxContentChars = 200_000;
    public const string Version = "v1-paragraph-1200-1500-200-900";

    public static string Normalize(string text)
    {
        var s = text.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n").Replace('\r', '\n');
        var clean = new StringBuilder(s.Length);
        foreach (var c in s) clean.Append(char.IsControl(c) && c is not ('\n' or '\t') ? ' ' : c);
        var lines = clean.ToString().Split('\n').Select(x => x.TrimEnd());
        return BlankRuns().Replace(string.Join('\n', lines), "\n\n").Trim();
    }

    public static IReadOnlyList<ChunkDraft> Split(string normalized)
    {
        var chunks = new List<ChunkDraft>();
        var current = new StringBuilder();
        string? heading = null, currentHeading = null;
        var hasOwnText = false; // the chunk has text that is not only overlap
        var bodyAdded = false;  // the chunk has text other than headings
        var overlapLength = 0;  // length of the overlap prefix carried from the previous chunk
        void Flush()
        {
            if (!hasOwnText) { current.Clear(); overlapLength = 0; return; }
            chunks.Add(new(chunks.Count, currentHeading, current.ToString().Trim()));
            if (chunks.Count > MaxChunks) throw new AiFailure(422, "knowledge.too_many_chunks", $"Tài liệu tạo quá {MaxChunks} đoạn; hãy tách thành nhiều tài liệu.");
            current.Clear(); hasOwnText = bodyAdded = false; overlapLength = 0;
        }
        void Append(string text)
        {
            if (current.Length > 0) current.Append("\n\n");
            current.Append(text); hasOwnText = true;
        }
        foreach (var block in normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IsHeading(block))
            {
                // A heading starts a new chunk and stays attached to the text that follows it.
                // Consecutive headings (section, then sub-section) share one chunk and a combined heading path.
                var title = block.TrimStart('#', ' ').TrimEnd(':', ' ');
                if (bodyAdded || !hasOwnText) { Flush(); heading = title; }
                else heading = heading is null ? title : $"{heading} › {title}";
                if (heading.Length > 500) heading = heading[..500];
                currentHeading = heading;
                Append(block);
                continue;
            }
            foreach (var piece in block.Length > PieceChars ? SplitLong(block) : [block])
            {
                // Pieces are <= 900, overlap <= 200 and a non-cut chunk owns < 300, so every chunk stays <= MaxChars.
                if (bodyAdded && current.Length + 2 + piece.Length > TargetChars && current.Length - overlapLength >= MinOwnChars)
                {
                    var previous = current.ToString();
                    Flush();
                    // Overlap keeps context across a cut inside one section; never carry text into a new heading.
                    current.Append(Tail(previous)); overlapLength = current.Length;
                    currentHeading = heading;
                }
                if (!hasOwnText) currentHeading = heading;
                Append(piece); bodyAdded = true;
            }
        }
        Flush();
        return chunks;
    }

    private static bool IsHeading(string block)
    {
        if (block.Contains('\n') || block.Length > 150) return false;
        if (block.StartsWith('#')) return true;
        if (block.Length <= 100 && block.EndsWith(':')) return true;
        // Numbered/section labels count only when they read as a title, not as a sentence or list item.
        if (block.Length <= 100 && SectionLabel().IsMatch(block) && !block.EndsWith('.') && !block.EndsWith(';') && !block.EndsWith(',')) return true;
        var letters = block.Where(char.IsLetter).ToArray();
        return letters.Length >= 3 && block.Length <= 100 && letters.All(char.IsUpper);
    }

    private static IEnumerable<string> SplitLong(string block)
    {
        var buffer = new StringBuilder();
        foreach (var sentence in SentenceBreak().Split(block).Where(x => x.Length > 0))
            foreach (var part in sentence.Length > PieceChars ? SplitWords(sentence) : [sentence])
            {
                if (buffer.Length > 0 && buffer.Length + 1 + part.Length > PieceChars) { yield return buffer.ToString(); buffer.Clear(); }
                if (buffer.Length > 0) buffer.Append(' ');
                buffer.Append(part);
            }
        if (buffer.Length > 0) yield return buffer.ToString();
    }

    private static IEnumerable<string> SplitWords(string sentence)
    {
        var buffer = new StringBuilder();
        foreach (var word in sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // A single token longer than the target (e.g. a URL) is cut hard rather than dropped.
            for (var start = 0; start < word.Length; start += PieceChars)
            {
                var token = word.Substring(start, Math.Min(PieceChars, word.Length - start));
                if (buffer.Length > 0 && buffer.Length + 1 + token.Length > PieceChars) { yield return buffer.ToString(); buffer.Clear(); }
                if (buffer.Length > 0) buffer.Append(' ');
                buffer.Append(token);
            }
        }
        if (buffer.Length > 0) yield return buffer.ToString();
    }

    private static string Tail(string text)
    {
        if (text.Length <= OverlapChars) return text;
        var tail = text[^OverlapChars..];
        var sentence = SentenceBreak().Match(tail);
        if (sentence.Success && sentence.Index + sentence.Length < tail.Length) return tail[(sentence.Index + sentence.Length)..].Trim();
        var space = tail.IndexOf(' ');
        return space >= 0 && space + 1 < tail.Length ? tail[(space + 1)..].Trim() : tail.Trim();
    }

    [GeneratedRegex(@"\n{3,}")] private static partial Regex BlankRuns();
    [GeneratedRegex(@"(?<=[\.\?\!;])\s+|\n")] private static partial Regex SentenceBreak();
    [GeneratedRegex(@"^((Chương|Mục|Điều|Phần)\s+[\dIVXLC]+|[IVXLC]+\.\s|\d+(\.\d+)*[\.\)]\s)", RegexOptions.IgnoreCase)] private static partial Regex SectionLabel();
}

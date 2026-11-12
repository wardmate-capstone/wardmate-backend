using WardMate.Services.AIOCR.Application;
using WardMate.Services.AIOCR.Infrastructure;

namespace WardMate.UnitTests.Aiocr;

// CHAT-CITE-01: chatbot chỉ trả nguồn được trích trong câu trả lời; không trích nguồn nào thì không có nguồn tham khảo.
public sealed class ChatCitationTests
{
    private static KnowledgeHit Hit(string title, double score) =>
        new(Guid.NewGuid(), Guid.NewGuid(), title, "PROCEDURE", Guid.NewGuid(), "Thành phần hồ sơ", "nội dung", score);

    private static readonly KnowledgeHit[] Sources = [Hit("Cấp thẻ BHYT", 0.6), Hit("Gia hạn thẻ BHYT", 0.58)];

    [Fact]
    public void Answer_without_markers_returns_no_citations()
    {
        var answer = "Hiện chưa đủ thông tin về trợ cấp thất nghiệp. Bạn có thể hỏi cán bộ một cửa tại phường.";
        Assert.Null(ChatService.Cite(answer, Sources));
    }

    [Fact]
    public void Only_cited_sources_are_returned_in_order()
    {
        var citations = ChatService.Cite("Cần tờ khai [2]. Nộp tại phường [2].", Sources);
        var only = Assert.Single(citations!);
        Assert.Equal(2, only.Index);
        Assert.Equal("Gia hạn thẻ BHYT", only.Title);
    }

    [Fact]
    public void Out_of_range_markers_alone_return_no_citations()
    {
        Assert.Null(ChatService.Cite("Xem điều [7] của luật.", Sources));
    }
}

using WardMate.Services.AIOCR.Application;
using WardMate.Services.AIOCR.Infrastructure;

namespace WardMate.UnitTests.Aiocr;

// CHAT-QUALITY-01 / CHAT-FEEDBACK-01: đọc câu trả lời JSON của AI, tìm nguồn theo ngữ cảnh, đánh giá câu trả lời; DOC-HEIC-01 cho CCCD.
public sealed class ChatQualityTests
{
    [Fact]
    public void Reply_is_parsed_and_suggestions_are_cleaned()
    {
        var reply = GeminiAssistant.ParseReply("""
            {"answer":"  Bạn đăng ký cho ai? ","needsClarification":true,
             "suggestedQuestions":["Cho con mới sinh","cho con mới sinh","", "Cho bản thân\nquá hạn", "a", "b", "c"]}
            """);
        Assert.Equal("Bạn đăng ký cho ai?", reply.Answer);
        Assert.True(reply.NeedsClarification);
        Assert.Equal(["Cho con mới sinh", "Cho bản thân quá hạn", "a", "b"], reply.SuggestedQuestions);
    }

    [Theory]
    [InlineData("""{"answer":"","needsClarification":false,"suggestedQuestions":[]}""")]
    [InlineData("""{"answer":"ok","suggestedQuestions":[]}""")]
    [InlineData("""{"answer":"ok","needsClarification":"no","suggestedQuestions":[]}""")]
    public void Invalid_reply_is_rejected(string raw) =>
        Assert.Equal("ai.invalid_response", Assert.Throws<AiFailure>(() => GeminiAssistant.ParseReply(raw)).Code);

    [Fact]
    public void Short_follow_up_is_searched_with_previous_question()
    {
        Assert.Equal("Làm giấy khai sinh cần gì?\ncòn phí thì sao", ChatService.RetrievalQuery("Làm giấy khai sinh cần gì?", "còn phí thì sao"));
        var full = "Tôi muốn đăng ký kết hôn với người nước ngoài thì cần chuẩn bị những giấy tờ gì và nộp ở đâu";
        Assert.Equal(full, ChatService.RetrievalQuery("Làm giấy khai sinh cần gì?", full));
        Assert.Equal("phí", ChatService.RetrievalQuery(null, "phí"));
        Assert.Equal(300 + 1 + 3, ChatService.RetrievalQuery(new string('x', 500), "phí").Length);
    }

    [Fact]
    public void Feedback_reason_is_validated_and_dropped_when_helpful()
    {
        Assert.Equal(("NOT_WHAT_I_ASKED", "Tôi hỏi phí"), ChatService.ValidateFeedback(new(false, "not_what_i_asked", " Tôi hỏi phí ")));
        Assert.Equal((null, null), ChatService.ValidateFeedback(new(true, "WRONG_INFO", null)));
        Assert.Equal("chat.invalid_feedback", Assert.Throws<AiFailure>(() => ChatService.ValidateFeedback(new(false, "BAD", null))).Code);
        Assert.Equal("chat.invalid_feedback", Assert.Throws<AiFailure>(() => ChatService.ValidateFeedback(new(false, null, new string('x', 1001)))).Code);
    }

    [Fact]
    public void Mobile_image_formats_are_recognised_for_gemini()
    {
        byte[] Ftyp(string brand) => [0, 0, 0, 24, .."ftyp"u8.ToArray(), .. System.Text.Encoding.ASCII.GetBytes(brand)];
        Assert.Equal("image/heic", GeminiAssistant.MobileImageMime(Ftyp("heic")));
        Assert.Equal("image/heif", GeminiAssistant.MobileImageMime(Ftyp("mif1")));
        Assert.Equal("image/webp", GeminiAssistant.MobileImageMime([.. "RIFF"u8.ToArray(), 0, 0, 0, 0, .. "WEBP"u8.ToArray()]));
        Assert.Null(GeminiAssistant.MobileImageMime(Ftyp("isom")));
    }
}

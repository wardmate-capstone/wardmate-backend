using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WardMate.Services.IAM.API.Errors;
using WardMate.Services.IAM.Application.Profiles;

namespace WardMate.Services.IAM.API.Controllers;

/// <summary>PROFILE-AVATAR-01: ảnh đại diện (Web và Mobile). Mọi tài khoản đăng nhập tự đổi ảnh của mình.</summary>
[ApiController, Authorize, Route("api/v1/users")]
[ProducesResponseType<ProblemDetails>(400), ProducesResponseType<ProblemDetails>(401), ProducesResponseType<ProblemDetails>(404)]
public sealed class AvatarsController(ISender sender) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);

    /// <summary>Tải ảnh mới (multipart, trường <c>file</c>): JPEG/PNG/WebP/HEIC ≤ 10 MB; server cắt vuông 512px, xóa metadata, lưu JPEG.</summary>
    [HttpPut("me/avatar"), Consumes("multipart/form-data")]
    [RequestSizeLimit(AvatarRules.MaxUploadBytes + 64 * 1024), RequestFormLimits(MultipartBodyLengthLimit = AvatarRules.MaxUploadBytes + 64 * 1024)]
    [ProducesResponseType<AvatarDto>(200)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return Result(AvatarErrors.Empty);
        if (file.Length > AvatarRules.MaxUploadBytes)
            return Result(AvatarErrors.TooLarge);
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        var result = await sender.Send(new SetAvatarCommand(UserId, buffer.ToArray()), ct);
        return result.IsSuccess ? Ok(result.Value) : result.ToProblem(HttpContext);
    }

    /// <summary>Xóa ảnh đại diện; chưa có ảnh vẫn trả 204.</summary>
    [HttpDelete("me/avatar"), ProducesResponseType(204)]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        var result = await sender.Send(new DeleteAvatarCommand(UserId), ct);
        return result.IsSuccess ? NoContent() : result.ToProblem(HttpContext);
    }

    /// <summary>Ảnh JPEG. Công dân chỉ xem ảnh của mình và của cán bộ; không có hoặc không được xem đều trả 404.</summary>
    [HttpGet("{userId:guid}/avatar"), ProducesResponseType(200), ProducesResponseType(304)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct)
    {
        var result = await sender.Send(new GetAvatarQuery(UserId, userId), ct);
        if (!result.IsSuccess) return result.ToProblem(HttpContext);
        var file = result.Value!;
        Response.Headers.ETag = file.ETag;
        // URL có ?v= theo lần đổi ảnh nên cache được lâu; private vì cần đăng nhập.
        Response.Headers.CacheControl = "private, max-age=86400";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (Request.Headers.IfNoneMatch.Contains(file.ETag)) return StatusCode(304);
        return File(file.Content, file.ContentType);
    }

    private ObjectResult Result(WardMate.Services.IAM.Application.Common.Error error) =>
        (ObjectResult)WardMate.Services.IAM.Application.Common.Result<bool>.Failure(error).ToProblem(HttpContext);
}

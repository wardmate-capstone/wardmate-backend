using System.Security.Cryptography;
using MediatR;
using WardMate.Imaging;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Application.Profiles;

// PROFILE-AVATAR-01: ảnh đại diện cho Web và Mobile. Không bắt buộc: chưa tải thì avatarUrl = null.
public sealed record AvatarDto(string AvatarUrl, DateTime UpdatedAt);
public sealed record AvatarFile(byte[] Content, string ContentType, string ETag, DateTime UpdatedAt);
public sealed record SetAvatarCommand(Guid UserId, byte[] Content) : IRequest<Result<AvatarDto>>;
public sealed record DeleteAvatarCommand(Guid UserId) : IRequest<Result<bool>>;
public sealed record GetAvatarQuery(Guid ViewerId, Guid UserId) : IRequest<Result<AvatarFile>>;

public interface IAvatarStore
{
    Task<User?> FindUser(Guid userId, CancellationToken ct);
    Task<UserAvatar?> Find(Guid userId, CancellationToken ct);
    void Add(UserAvatar avatar);
    void Remove(UserAvatar avatar);
    /// <summary>Có vai trò khác REGISTERED_CITIZEN (cán bộ, quản lý, quản trị).</summary>
    Task<bool> IsStaff(Guid userId, CancellationToken ct);
    Task<SaveOutcome> SaveChanges(CancellationToken ct);
}

public static class AvatarRules
{
    public const int MaxUploadBytes = 10 * 1024 * 1024;
    public const int Edge = 512;
    public const int Quality = 85;

    public static string Url(Guid userId, DateTime updatedAt) =>
        $"/api/v1/users/{userId:D}/avatar?v={new DateTimeOffset(DateTime.SpecifyKind(updatedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds()}";

    public static string? Url(User user) => user.AvatarUpdatedAt is { } at ? Url(user.Id, at) : null;

    /// <summary>Tự xem; cán bộ xem mọi người; công dân xem ảnh cán bộ. Công dân không xem ảnh công dân khác.</summary>
    public static bool CanView(Guid viewer, Guid owner, bool viewerIsStaff, bool ownerIsStaff) =>
        viewer == owner || viewerIsStaff || ownerIsStaff;
}

public static class AvatarErrors
{
    public static readonly Error Empty = new("iam.avatar_empty", "Chưa chọn ảnh.", ErrorKind.BadRequest);
    public static readonly Error TooLarge = new("iam.avatar_too_large", "Ảnh tối đa 10 MB.", ErrorKind.BadRequest);
    public static readonly Error NotFound = new("iam.avatar_not_found", "Người dùng chưa có ảnh đại diện.", ErrorKind.NotFound);
    public static Error Invalid(string code, string message) => new(code, message, ErrorKind.BadRequest);
}

public sealed class AvatarHandlers(IAvatarStore store, IImageProcessor images, TimeProvider clock) :
    IRequestHandler<SetAvatarCommand, Result<AvatarDto>>,
    IRequestHandler<DeleteAvatarCommand, Result<bool>>,
    IRequestHandler<GetAvatarQuery, Result<AvatarFile>>
{
    public async Task<Result<AvatarDto>> Handle(SetAvatarCommand request, CancellationToken ct)
    {
        if (request.Content.Length == 0) return Result<AvatarDto>.Failure(AvatarErrors.Empty);
        if (request.Content.Length > AvatarRules.MaxUploadBytes) return Result<AvatarDto>.Failure(AvatarErrors.TooLarge);
        var user = await store.FindUser(request.UserId, ct);
        if (user is null) return Result<AvatarDto>.Failure(ProfileErrors.UserNotFound);
        ProcessedImage image;
        try { image = images.ToJpeg(request.Content, AvatarRules.Edge, AvatarRules.Quality, square: true); }
        catch (ImageProcessingException error) { return Result<AvatarDto>.Failure(AvatarErrors.Invalid("iam." + error.Code.Replace('.', '_'), error.Message)); }

        var now = clock.GetUtcNow().UtcDateTime;
        var avatar = await store.Find(user.Id, ct);
        if (avatar is null) { avatar = new UserAvatar { UserId = user.Id }; store.Add(avatar); }
        avatar.Content = image.Content;
        avatar.ContentType = image.ContentType;
        avatar.Sha256 = Convert.ToHexString(SHA256.HashData(image.Content)).ToLowerInvariant();
        avatar.UpdatedAt = now;
        user.AvatarUpdatedAt = now;
        return await store.SaveChanges(ct) == SaveOutcome.Saved
            ? Result<AvatarDto>.Success(new(AvatarRules.Url(user.Id, now), now))
            : Result<AvatarDto>.Failure(ProfileErrors.ConcurrentUpdate);
    }

    public async Task<Result<bool>> Handle(DeleteAvatarCommand request, CancellationToken ct)
    {
        var user = await store.FindUser(request.UserId, ct);
        if (user is null) return Result<bool>.Failure(ProfileErrors.UserNotFound);
        var avatar = await store.Find(user.Id, ct);
        if (avatar is null && user.AvatarUpdatedAt is null) return Result<bool>.Success(true); // đã không có ảnh: idempotent
        if (avatar is not null) store.Remove(avatar);
        user.AvatarUpdatedAt = null;
        return await store.SaveChanges(ct) == SaveOutcome.Saved ? Result<bool>.Success(true) : Result<bool>.Failure(ProfileErrors.ConcurrentUpdate);
    }

    public async Task<Result<AvatarFile>> Handle(GetAvatarQuery request, CancellationToken ct)
    {
        if (request.ViewerId != request.UserId
            && !AvatarRules.CanView(request.ViewerId, request.UserId, await store.IsStaff(request.ViewerId, ct), await store.IsStaff(request.UserId, ct)))
            return Result<AvatarFile>.Failure(AvatarErrors.NotFound); // không tiết lộ ai có ảnh
        var avatar = await store.Find(request.UserId, ct);
        return avatar is null ? Result<AvatarFile>.Failure(AvatarErrors.NotFound)
            : Result<AvatarFile>.Success(new(avatar.Content, avatar.ContentType, "\"" + avatar.Sha256[..32] + "\"", avatar.UpdatedAt));
    }
}

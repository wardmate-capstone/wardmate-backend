using ImageMagick;
using WardMate.Imaging;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Application.Profiles;
using WardMate.Services.IAM.Domain.Entities;
using WardMate.UnitTests.Support;

namespace WardMate.UnitTests.Mobile;

// PROFILE-AVATAR-01: tải/xóa/xem ảnh đại diện và quyền xem.
public sealed class AvatarTests
{
    private sealed class FakeStore : IAvatarStore
    {
        public readonly Dictionary<Guid, User> Users = [];
        public readonly Dictionary<Guid, UserAvatar> Avatars = [];
        public readonly HashSet<Guid> Staff = [];
        public Task<User?> FindUser(Guid userId, CancellationToken ct) => Task.FromResult(Users.GetValueOrDefault(userId));
        public Task<UserAvatar?> Find(Guid userId, CancellationToken ct) => Task.FromResult(Avatars.GetValueOrDefault(userId));
        public void Add(UserAvatar avatar) => Avatars[avatar.UserId] = avatar;
        public void Remove(UserAvatar avatar) => Avatars.Remove(avatar.UserId);
        public Task<bool> IsStaff(Guid userId, CancellationToken ct) => Task.FromResult(Staff.Contains(userId));
        public Task<SaveOutcome> SaveChanges(CancellationToken ct) => Task.FromResult(SaveOutcome.Saved);
    }

    private static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0, DateTimeKind.Utc);
    private readonly FakeStore store = new();
    private readonly AvatarHandlers handlers;
    private readonly User citizen = new() { Id = Guid.NewGuid() }, other = new() { Id = Guid.NewGuid() }, officer = new() { Id = Guid.NewGuid() };

    public AvatarTests()
    {
        foreach (var u in new[] { citizen, other, officer }) store.Users[u.Id] = u;
        store.Staff.Add(officer.Id);
        handlers = new AvatarHandlers(store, new MagickImageProcessor(), new MutableTime(Now));
    }

    private static byte[] Png() { using var i = new MagickImage(MagickColors.Red, 900, 600) { Format = MagickFormat.Png }; return i.ToByteArray(); }

    [Fact]
    public async Task Upload_stores_square_jpeg_and_sets_versioned_url()
    {
        var result = await handlers.Handle(new SetAvatarCommand(citizen.Id, Png()), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(Now, citizen.AvatarUpdatedAt);
        Assert.Equal($"/api/v1/users/{citizen.Id:D}/avatar?v={new DateTimeOffset(Now).ToUnixTimeMilliseconds()}", result.Value!.AvatarUrl);
        Assert.Equal(AvatarRules.Url(citizen), result.Value.AvatarUrl);
        using var stored = new MagickImage(store.Avatars[citizen.Id].Content);
        Assert.Equal((512u, 512u), (stored.Width, stored.Height));
    }

    [Fact]
    public async Task Invalid_file_and_empty_upload_are_rejected()
    {
        Assert.Equal("iam.avatar_empty", (await handlers.Handle(new SetAvatarCommand(citizen.Id, []), default)).Error!.Code);
        Assert.Equal("iam.image_unsupported", (await handlers.Handle(new SetAvatarCommand(citizen.Id, "abc"u8.ToArray()), default)).Error!.Code);
        Assert.Null(citizen.AvatarUpdatedAt);
    }

    [Fact]
    public async Task Delete_is_idempotent_and_clears_url()
    {
        await handlers.Handle(new SetAvatarCommand(citizen.Id, Png()), default);
        Assert.True((await handlers.Handle(new DeleteAvatarCommand(citizen.Id), default)).IsSuccess);
        Assert.Null(AvatarRules.Url(citizen));
        Assert.Empty(store.Avatars);
        Assert.True((await handlers.Handle(new DeleteAvatarCommand(citizen.Id), default)).IsSuccess);
    }

    [Fact]
    public async Task Citizen_cannot_see_other_citizen_but_staff_and_self_can()
    {
        await handlers.Handle(new SetAvatarCommand(citizen.Id, Png()), default);
        await handlers.Handle(new SetAvatarCommand(officer.Id, Png()), default);
        Assert.True((await handlers.Handle(new GetAvatarQuery(citizen.Id, citizen.Id), default)).IsSuccess);
        Assert.True((await handlers.Handle(new GetAvatarQuery(officer.Id, citizen.Id), default)).IsSuccess);
        Assert.True((await handlers.Handle(new GetAvatarQuery(other.Id, officer.Id), default)).IsSuccess);
        Assert.Equal("iam.avatar_not_found", (await handlers.Handle(new GetAvatarQuery(other.Id, citizen.Id), default)).Error!.Code);
    }
}

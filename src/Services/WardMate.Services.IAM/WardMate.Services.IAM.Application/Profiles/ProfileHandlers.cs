using MediatR;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Application.Profiles;

public sealed class ProfileHandlers(IProfileStore store, TimeProvider clock) :
    IRequestHandler<ListProfilesQuery, Result<ProfilePage>>,
    IRequestHandler<GetProfileQuery, Result<UserProfileDto>>,
    IRequestHandler<CreateProfileCommand, Result<UserProfileDto>>,
    IRequestHandler<UpdateProfileCommand, Result<UserProfileDto>>,
    IRequestHandler<DeleteProfileCommand, Result<bool>>
{
    public Task<Result<ProfilePage>> Handle(ListProfilesQuery request, CancellationToken ct) =>
        store.List(request.ActorId, request.Page, request.PageSize, ct);

    public async Task<Result<UserProfileDto>> Handle(GetProfileQuery request, CancellationToken ct)
    {
        var profile = await store.Find(request.UserId, ct);
        return profile is null ? Result<UserProfileDto>.Failure(ProfileErrors.NotFound)
            : Result<UserProfileDto>.Success(UserProfileDto.From(profile));
    }

    public async Task<Result<UserProfileDto>> Handle(CreateProfileCommand request, CancellationToken ct)
    {
        if (!await store.UserExists(request.UserId, ct)) return Result<UserProfileDto>.Failure(ProfileErrors.UserNotFound);
        if (await store.Find(request.UserId, ct) is not null) return Result<UserProfileDto>.Failure(ProfileErrors.Exists);
        var profile = new UserProfile { UserId = request.UserId };
        Apply(profile, request.Profile);
        store.Add(profile);
        return await Save(profile, ct);
    }

    public async Task<Result<UserProfileDto>> Handle(UpdateProfileCommand request, CancellationToken ct)
    {
        var profile = await store.Find(request.UserId, ct);
        if (profile is null) return Result<UserProfileDto>.Failure(ProfileErrors.NotFound);
        Apply(profile, request.Profile);
        return await Save(profile, ct);
    }

    public async Task<Result<bool>> Handle(DeleteProfileCommand request, CancellationToken ct)
    {
        var profile = await store.Find(request.UserId, ct);
        if (profile is null) return Result<bool>.Failure(ProfileErrors.NotFound);
        store.Remove(profile);
        return await store.SaveChanges(ct) == SaveOutcome.Saved ? Result<bool>.Success(true)
            : Result<bool>.Failure(ProfileErrors.ConcurrentUpdate);
    }

    private void Apply(UserProfile profile, ProfileInput input)
    {
        profile.FullName = input.FullName.Trim();
        profile.IdentityNumber = Normalize(input.IdentityNumber);
        profile.PhoneNumber = Normalize(input.PhoneNumber);
        profile.DateOfBirth = input.DateOfBirth;
        profile.Gender = Normalize(input.Gender);
        profile.PermanentAddress = Normalize(input.PermanentAddress);
        profile.TemporaryAddress = Normalize(input.TemporaryAddress);
        profile.UpdatedAt = clock.GetUtcNow().UtcDateTime;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<Result<UserProfileDto>> Save(UserProfile profile, CancellationToken ct) =>
        await store.SaveChanges(ct) switch
        {
            SaveOutcome.Saved => Result<UserProfileDto>.Success(UserProfileDto.From(profile)),
            SaveOutcome.Duplicate => Result<UserProfileDto>.Failure(ProfileErrors.Duplicate),
            _ => Result<UserProfileDto>.Failure(ProfileErrors.ConcurrentUpdate)
        };
}

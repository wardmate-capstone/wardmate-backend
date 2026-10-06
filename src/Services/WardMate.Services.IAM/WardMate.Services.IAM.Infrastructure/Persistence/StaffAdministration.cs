using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WardMate.Services.IAM.Application.Accounts;
using WardMate.Services.IAM.Application.Common;
using WardMate.Services.IAM.Application.DTOs;
using WardMate.Services.IAM.Application.Interfaces;
using WardMate.Services.IAM.Application.Rbac;
using WardMate.Services.IAM.Domain;
using WardMate.Services.IAM.Domain.Entities;

namespace WardMate.Services.IAM.Infrastructure.Persistence;

public sealed class StaffAdministration(IamDbContext db, ManagementScope scope, IRbacStore rbac,
    IPasswordHasher passwords, TimeProvider clock) : IStaffAdministration
{
    public async Task<Result<ManagedUserPage>> ListUsers(Guid actorId, int page, int pageSize, CancellationToken ct)
    {
        if (!await scope.CanManage(actorId, ct)) return Result<ManagedUserPage>.Failure(RbacErrors.Forbidden);
        var query = scope.VisibleUsers(actorId).AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ManagedUserDto(x.Id, x.Username, x.Email, x.IsActive, x.WardId,
                x.Ward == null ? null : x.Ward.Name,
                x.Profile == null ? null : new UserProfileDto(x.Profile.FullName, x.Profile.IdentityNumber,
                    x.Profile.PhoneNumber, x.Profile.DateOfBirth, x.Profile.Gender,
                    x.Profile.PermanentAddress, x.Profile.TemporaryAddress),
                x.UserRoles.OrderBy(r => r.Role.RoleName).Select(r => new ManagedRoleDto(r.RoleId, r.Role.RoleName)).ToArray())).ToArrayAsync(ct);
        return Result<ManagedUserPage>.Success(new(items, page, pageSize, total));
    }

    public async Task<Result<WardDto[]>> ListWards(Guid actorId, CancellationToken ct)
    {
        if (!await scope.CanManage(actorId, ct)) return Result<WardDto[]>.Failure(RbacErrors.Forbidden);
        var admin = await rbac.IsAdministrator(actorId, ct);
        var query = db.Wards.AsNoTracking().Where(w => admin || db.Users.Any(u => u.Id == actorId && u.WardId == w.Id));
        return Result<WardDto[]>.Success(await query.OrderBy(w => w.Code)
            .Select(w => new WardDto(w.Id, w.Code, w.Name)).ToArrayAsync(ct));
    }

    public Task<Result<WardDto>> CreateWard(Guid actorId, string code, string name, CancellationToken ct) => rbac.Exclusive(async () =>
    {
        if (!await rbac.IsAdministrator(actorId, ct)) return Result<WardDto>.Failure(RbacErrors.Forbidden);
        var normalized = code.Trim().ToUpperInvariant();
        if (await db.Wards.AnyAsync(w => w.Code == normalized, ct))
            return Result<WardDto>.Failure(new("iam.ward_exists", "Mã phường đã tồn tại.", ErrorKind.Conflict));
        var ward = new Ward { Code = normalized, Name = name.Trim() };
        db.Wards.Add(ward);
        rbac.Audit(actorId, "ward.created", null, null, null, JsonSerializer.Serialize(new { ward.Id, ward.Code }));
        return Result<WardDto>.Success(new(ward.Id, ward.Code, ward.Name));
    }, ct);

    public Task<Result<bool>> AssignWard(Guid actorId, Guid userId, Guid? wardId, CancellationToken ct) => rbac.Exclusive(async () =>
    {
        if (!await rbac.IsAdministrator(actorId, ct)) return Result<bool>.Failure(RbacErrors.Forbidden);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null) return Result<bool>.Failure(RbacErrors.UserNotFound);
        if (wardId.HasValue && !await db.Wards.AnyAsync(w => w.Id == wardId, ct))
            return Result<bool>.Failure(new("iam.ward_not_found", "Không tìm thấy phường.", ErrorKind.NotFound));
        var previousWardId = user.WardId;
        user.WardId = wardId;
        user.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.RefreshTokens.Where(t => t.UserId == userId && !t.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true), ct);
        rbac.Audit(actorId, "account.ward_changed", userId, null, null, JsonSerializer.Serialize(new { previousWardId, wardId }));
        return Result<bool>.Success(true);
    }, ct);

    public Task<Result<ManagedUserDto>> CreateFrontDesk(Guid actorId, CreateFrontDeskInput input, CancellationToken ct) => rbac.Exclusive(async () =>
    {
        if (!await scope.CanManage(actorId, ct)) return Result<ManagedUserDto>.Failure(RbacErrors.Forbidden);
        var admin = await rbac.IsAdministrator(actorId, ct);
        var actorWardId = await db.Users.Where(u => u.Id == actorId).Select(u => u.WardId).SingleAsync(ct);
        if (!admin && input.WardId.HasValue && input.WardId != actorWardId)
            return Result<ManagedUserDto>.Failure(RbacErrors.Forbidden);
        var wardId = admin ? input.WardId : actorWardId;
        var ward = await db.Wards.AsNoTracking().SingleOrDefaultAsync(w => w.Id == wardId, ct);
        if (ward is null) return Result<ManagedUserDto>.Failure(new("iam.ward_not_found", "Cần chọn phường công tác hợp lệ.", ErrorKind.NotFound));
        var username = input.Username.Trim().ToLowerInvariant();
        var email = input.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Username == username || u.Email == email, ct))
            return Result<ManagedUserDto>.Failure(AuthErrors.DuplicateAccount);
        var role = await db.Roles.SingleAsync(r => r.RoleName == RoleNames.FrontDeskOfficer, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var user = new User { Username = username, Email = email, PasswordHash = passwords.Hash(input.Password),
            WardId = ward.Id, CreatedAt = now, UpdatedAt = now };
        user.Profile = new UserProfile { UserId = user.Id, FullName = input.FullName.Trim(), UpdatedAt = now };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.Users.Add(user);
        rbac.Audit(actorId, "account.front_desk_created", user.Id, role.Id, null, JsonSerializer.Serialize(new { wardId = ward.Id }));
        return Result<ManagedUserDto>.Success(new(user.Id, user.Username, user.Email, true, ward.Id, ward.Name,
            UserProfileDto.From(user.Profile), [new(role.Id, RoleNames.FrontDeskOfficer)]));
    }, ct);
}

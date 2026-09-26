using WardMate.Services.IAM.Application.Rbac;
using WardMate.Services.IAM.Domain;
using Xunit;

namespace WardMate.Services.IAM.Tests;

public sealed class RbacValidationTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("ROLE WITH SPACE", false)]
    [InlineData("VAI_TRÒ", false)]
    [InlineData("1ROLE", false)]
    [InlineData("NEW_ROLE", true)]
    [InlineData("new_role2", true)]
    public void RoleNamesHaveStableMachineReadableFormat(string name, bool valid) =>
        Assert.Equal(valid, new RoleInputValidator().Validate(new RoleInput(name, "Vai trò nghiệp vụ")).IsValid);

    [Theory]
    [InlineData(50, 2000, true)]
    [InlineData(51, 2000, false)]
    [InlineData(50, 2001, false)]
    public void RoleFieldLengthsAreBounded(int nameLength, int descriptionLength, bool valid) =>
        Assert.Equal(valid, new RoleInputValidator().Validate(new RoleInput(new string('A', nameLength), new string('a', descriptionLength))).IsValid);

    [Fact]
    public void MissingRoleBodyAndEmptyActorAreRejected()
    {
        var result = new CreateRoleValidator(new RoleInputValidator()).Validate(new CreateRoleCommand(Guid.Empty, null!));
        Assert.Contains(result.Errors, x => x.PropertyName == "Input");
        Assert.Contains(result.Errors, x => x.PropertyName == "ActorId");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, -1)]
    public void PermissionAssignmentRejectsInvalidIds(int role, int permission) =>
        Assert.False(new SetRolePermissionValidator().Validate(new SetRolePermissionCommand(Guid.NewGuid(), role, permission, true)).IsValid);

    [Theory]
    [InlineData(0, 20, false)]
    [InlineData(1, 101, false)]
    [InlineData(1, 0, false)]
    [InlineData(1000001, 1, false)]
    [InlineData(1, 100, true)]
    public void RoleAndAuditPagesAreBounded(int page, int size, bool valid)
    {
        Assert.Equal(valid, new ListRolesValidator().Validate(new ListRolesQuery(page, size)).IsValid);
        Assert.Equal(valid, new ListRbacAuditValidator().Validate(new ListRbacAuditQuery(page, size)).IsValid);
    }

    [Theory]
    [InlineData(RoleNames.ItAdmin)]
    [InlineData(RoleNames.RegisteredCitizen)]
    [InlineData(RoleNames.FrontDeskOfficer)]
    [InlineData(RoleNames.Manager)]
    [InlineData(RoleNames.ProcedureManager)]
    public void SystemRolesAreProtected(string name)
    {
        Assert.True(RbacRules.IsSystem(name));
        Assert.False(RbacRules.IsSystem("NEW_BUSINESS_ROLE"));
    }
}

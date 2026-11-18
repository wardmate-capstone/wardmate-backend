using WardMate.Services.ApplicationWorkflow.Infrastructure;

namespace WardMate.UnitTests.Workflow;

/// <summary>
/// Unit tests for StaffingLocks.FilterAvailabilityReasons.
/// Tests cover: skipShiftCheck flag behaviour, APPROVED_LEAVE always blocks,
/// and the happy-path (active shift, no leave).
/// No real DB or HTTP calls are made.
/// </summary>
public sealed class StaffingShiftCheckTests
{
    // ------------------------------------------------------------------ //
    //  skipShiftCheck = false  (production behaviour)
    // ------------------------------------------------------------------ //

    [Fact]
    public void SkipFalse_NoActiveShift_ReturnsNoActiveShift()
    {
        // Arrange
        bool hasActiveShift = false;
        bool hasApprovedLeave = false;
        bool skipShiftCheck = false;

        // Act
        var reasons = StaffingLocks.FilterAvailabilityReasons(hasActiveShift, hasApprovedLeave, skipShiftCheck);

        // Assert
        Assert.Contains("NO_ACTIVE_SHIFT", reasons);
        Assert.DoesNotContain("APPROVED_LEAVE", reasons);
        Assert.Single(reasons);
    }

    [Fact]
    public void SkipFalse_ActiveShift_NoLeave_ReturnsEmpty()
    {
        // Happy path: officer has an active shift, no approved leave.
        var reasons = StaffingLocks.FilterAvailabilityReasons(
            hasActiveShift: true,
            hasApprovedLeave: false,
            skipShiftCheck: false);

        Assert.Empty(reasons);
    }

    [Fact]
    public void SkipFalse_ActiveShift_ApprovedLeave_ReturnsApprovedLeave()
    {
        // Even with an active shift, approved leave blocks.
        var reasons = StaffingLocks.FilterAvailabilityReasons(
            hasActiveShift: true,
            hasApprovedLeave: true,
            skipShiftCheck: false);

        Assert.Contains("APPROVED_LEAVE", reasons);
        Assert.DoesNotContain("NO_ACTIVE_SHIFT", reasons);
        Assert.Single(reasons);
    }

    [Fact]
    public void SkipFalse_NoActiveShift_ApprovedLeave_ReturnsBothReasons()
    {
        // No shift AND approved leave → both reasons reported.
        var reasons = StaffingLocks.FilterAvailabilityReasons(
            hasActiveShift: false,
            hasApprovedLeave: true,
            skipShiftCheck: false);

        Assert.Contains("NO_ACTIVE_SHIFT", reasons);
        Assert.Contains("APPROVED_LEAVE", reasons);
        Assert.Equal(2, reasons.Length);
    }

    // ------------------------------------------------------------------ //
    //  skipShiftCheck = true  (test mode)
    // ------------------------------------------------------------------ //

    [Fact]
    public void SkipTrue_NoActiveShift_NoLeave_ReturnsEmpty()
    {
        // Test mode: no shift check → officer is available even without active shift.
        var reasons = StaffingLocks.FilterAvailabilityReasons(
            hasActiveShift: false,
            hasApprovedLeave: false,
            skipShiftCheck: true);

        Assert.Empty(reasons);
    }

    [Fact]
    public void SkipTrue_NoActiveShift_ApprovedLeave_ReturnsApprovedLeaveOnly()
    {
        // Test mode: skip shift check, but APPROVED_LEAVE still blocks.
        var reasons = StaffingLocks.FilterAvailabilityReasons(
            hasActiveShift: false,
            hasApprovedLeave: true,
            skipShiftCheck: true);

        Assert.Contains("APPROVED_LEAVE", reasons);
        Assert.DoesNotContain("NO_ACTIVE_SHIFT", reasons);
        Assert.Single(reasons);
    }

    [Fact]
    public void SkipTrue_ActiveShift_ApprovedLeave_ReturnsApprovedLeaveOnly()
    {
        // Test mode with active shift: leave still blocks, shift check is irrelevant.
        var reasons = StaffingLocks.FilterAvailabilityReasons(
            hasActiveShift: true,
            hasApprovedLeave: true,
            skipShiftCheck: true);

        Assert.Contains("APPROVED_LEAVE", reasons);
        Assert.DoesNotContain("NO_ACTIVE_SHIFT", reasons);
        Assert.Single(reasons);
    }

    [Fact]
    public void SkipTrue_ActiveShift_NoLeave_ReturnsEmpty()
    {
        // Test mode with active shift and no leave: fully available.
        var reasons = StaffingLocks.FilterAvailabilityReasons(
            hasActiveShift: true,
            hasApprovedLeave: false,
            skipShiftCheck: true);

        Assert.Empty(reasons);
    }
}

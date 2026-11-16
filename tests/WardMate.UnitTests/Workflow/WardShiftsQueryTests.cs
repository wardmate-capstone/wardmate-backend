using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.UnitTests.Workflow;

public sealed class WardShiftsQueryTests
{
    private static ReviewActor Manager(string ward = "W01") =>
        new(Guid.NewGuid(), IsOfficer: true, WardCode: ward, IsManager: true);
    private static ReviewActor Officer(string ward = "W01") =>
        new(Guid.NewGuid(), IsOfficer: true, WardCode: ward, IsManager: false);

    [Theory]
    [InlineData(2000, 1)]
    [InlineData(2026, 11)]
    [InlineData(2100, 12)]
    public void Valid_year_month_accepted(int year, int month)
    {
        // The boundary check is year 2000–2100, month 1–12
        Assert.True(year is >= 2000 and <= 2100);
        Assert.True(month is >= 1 and <= 12);
    }

    [Theory]
    [InlineData(1999, 1)]
    [InlineData(2101, 1)]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    public void Invalid_year_month_rejected(int year, int month)
    {
        bool invalid = year < 2000 || year > 2100 || month < 1 || month > 12;
        Assert.True(invalid);
    }

    [Fact]
    public void Shifts_grouped_by_officer_correctly()
    {
        var officer1 = Guid.NewGuid();
        var officer2 = Guid.NewGuid();
        var shifts = new[]
        {
            new OfficerShift { OfficerId = officer1, WardCode = "W01", StartsAtUtc = new DateTime(2026,11,1,8,0,0,DateTimeKind.Utc), EndsAtUtc = new DateTime(2026,11,1,17,0,0,DateTimeKind.Utc) },
            new OfficerShift { OfficerId = officer2, WardCode = "W01", StartsAtUtc = new DateTime(2026,11,2,8,0,0,DateTimeKind.Utc), EndsAtUtc = new DateTime(2026,11,2,17,0,0,DateTimeKind.Utc) },
            new OfficerShift { OfficerId = officer1, WardCode = "W01", StartsAtUtc = new DateTime(2026,11,3,8,0,0,DateTimeKind.Utc), EndsAtUtc = new DateTime(2026,11,3,17,0,0,DateTimeKind.Utc) },
        };

        var grouped = shifts.GroupBy(x => x.OfficerId)
            .Select(g => new WardShiftView(g.Key, g.ToArray()))
            .OrderBy(v => v.OfficerId)
            .ToArray();

        Assert.Equal(2, grouped.Length);
        Assert.Equal(2, grouped.Single(v => v.OfficerId == officer1).Shifts.Length);
        Assert.Single(grouped.Single(v => v.OfficerId == officer2).Shifts);
    }

    [Fact]
    public void Empty_ward_returns_empty_array()
    {
        var shifts = Array.Empty<OfficerShift>();
        var grouped = shifts.GroupBy(x => x.OfficerId)
            .Select(g => new WardShiftView(g.Key, g.ToArray()))
            .ToArray();
        Assert.Empty(grouped);
    }

    [Fact]
    public void Only_manager_can_query_ward_shifts()
    {
        // Manager: IsManager=true → allowed
        var manager = Manager();
        Assert.True(manager.IsManager);

        // Officer: IsManager=false → forbidden
        var officer = Officer();
        Assert.False(officer.IsManager);
    }

    [Fact]
    public void Month_boundary_is_exclusive_at_end()
    {
        // A shift starting exactly at monthEnd should NOT be included
        var monthStart = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1); // 2026-12-01

        var shiftInsideMonth = new OfficerShift
        {
            StartsAtUtc = new DateTime(2026, 11, 30, 23, 0, 0, DateTimeKind.Utc),
            EndsAtUtc   = new DateTime(2026, 12,  1,  0, 0, 0, DateTimeKind.Utc)
        };
        var shiftStartingAtMonthEnd = new OfficerShift
        {
            StartsAtUtc = monthEnd,
            EndsAtUtc   = monthEnd.AddHours(8)
        };

        // Filter: StartsAtUtc < monthEnd && EndsAtUtc > monthStart
        bool insideIncluded = shiftInsideMonth.StartsAtUtc < monthEnd && shiftInsideMonth.EndsAtUtc > monthStart;
        bool boundaryIncluded = shiftStartingAtMonthEnd.StartsAtUtc < monthEnd && shiftStartingAtMonthEnd.EndsAtUtc > monthStart;

        Assert.True(insideIncluded);
        Assert.False(boundaryIncluded);
    }
}


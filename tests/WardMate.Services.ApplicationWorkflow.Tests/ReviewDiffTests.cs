using System.Text.Json;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;
using Xunit;

namespace WardMate.Services.ApplicationWorkflow.Tests;

public sealed class ReviewDiffTests
{
    [Fact]
    public void DiffDistinguishesMissingNullTypesAndEscapedKeys()
    {
        var a = new ApplicationVersion { SnapshotData = """{"formData":{"removed":null,"type":1,"a/b~c":"old","empty":{}},"checklists":[]}""" };
        var b = new ApplicationVersion { SnapshotData = """{"formData":{"added":null,"type":"1","a/b~c":"new","empty":{}},"checklists":[]}""" };
        var diff = ApplicationSnapshots.Compare(a, b, []);
        Assert.Equal(4, diff.Length);
        Assert.Contains(diff, x => x.FieldKey == "/formData/removed" && x.OldExists && !x.NewExists);
        Assert.Contains(diff, x => x.FieldKey == "/formData/added" && !x.OldExists && x.NewExists);
        Assert.Contains(diff, x => x.FieldKey == "/formData/type");
        Assert.Contains(diff, x => x.FieldKey == "/formData/a~1b~0c");
    }

    [Fact]
    public void ChecklistOrderDoesNotProduceFalseDiffAndSnapshotIsDetached()
    {
        var a = new ApplicationRecord { FormData = """{"a":1,"b":2}""", Checklists = [new() { Code = "A" }, new() { Code = "B" }] };
        var original = ApplicationSnapshots.Capture(a, Guid.NewGuid(), DateTime.UtcNow);
        a.Checklists.Reverse(); a.FormData = """{"b":2,"a":1}""";
        var reordered = ApplicationSnapshots.Capture(a, Guid.NewGuid(), DateTime.UtcNow);
        Assert.Empty(ApplicationSnapshots.Compare(original, reordered, []));
        a.FormData = "{}"; a.Checklists.Clear();
        using var frozen = JsonDocument.Parse(original.SnapshotData);
        Assert.Equal(2, frozen.RootElement.GetProperty("checklists").GetArrayLength());
        Assert.Equal(1, frozen.RootElement.GetProperty("formData").GetProperty("a").GetInt32());
    }
}

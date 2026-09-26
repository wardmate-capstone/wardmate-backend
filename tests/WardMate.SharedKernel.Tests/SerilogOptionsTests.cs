using WardMate.SharedKernel.Logging;
using Xunit;

namespace WardMate.SharedKernel.Tests;

public sealed class SerilogOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var options = new SerilogOptions();

        Assert.Equal("Serilog", SerilogOptions.SectionName);
        Assert.Equal("Information", options.MinimumLevel);
        Assert.True(options.EnableConsoleSink);
        Assert.True(options.EnableFileSink);
        Assert.Equal("logs/wardmate-.log", options.LogFilePath);
        Assert.Equal(50, options.FileSizeLimitMb);
        Assert.Equal(7, options.RetainedFileCountLimit);
        Assert.Equal("WardMate", options.ApplicationName);
    }

    [Fact]
    public void CustomValues_AreApplied()
    {
        var options = new SerilogOptions
        {
            MinimumLevel = "Debug",
            EnableConsoleSink = false,
            EnableFileSink = true,
            LogFilePath = "logs/iam-.log",
            FileSizeLimitMb = 100,
            RetainedFileCountLimit = 14,
            ApplicationName = "WardMate.IAM"
        };

        Assert.Equal("Debug", options.MinimumLevel);
        Assert.False(options.EnableConsoleSink);
        Assert.True(options.EnableFileSink);
        Assert.Equal("logs/iam-.log", options.LogFilePath);
        Assert.Equal(100, options.FileSizeLimitMb);
        Assert.Equal(14, options.RetainedFileCountLimit);
        Assert.Equal("WardMate.IAM", options.ApplicationName);
    }
}

using RoboSync.Core.Engine;

namespace RoboSync.Core.Tests;

public class RobocopyOutputParserTests
{
    [Fact]
    public void Parses_new_file_line_with_size_and_path()
    {
        // Columns are tab-delimited: class, size (bytes), full path (/FP).
        var line = "\t  New File  \t\t      1024\tC:\\src\\report.txt";
        var result = RobocopyOutputParser.Parse(line);

        Assert.Equal(RoboLineType.File, result.Type);
        Assert.Equal(1024, result.Bytes);
        Assert.Equal(@"C:\src\report.txt", result.Path);
        Assert.Equal("New File", result.Class);
    }

    [Fact]
    public void Parses_changed_file_line()
    {
        var line = "\t  Newer  \t\t      52428800\tC:\\src\\movie.mp4";
        var result = RobocopyOutputParser.Parse(line);

        Assert.Equal(RoboLineType.File, result.Type);
        Assert.Equal(52428800, result.Bytes);
        Assert.Equal(@"C:\src\movie.mp4", result.Path);
    }

    [Fact]
    public void Directory_header_is_not_a_file()
    {
        var line = "\t  New Dir  \t\t          4\tC:\\src\\subdir\\";
        var result = RobocopyOutputParser.Parse(line);

        Assert.Equal(RoboLineType.Directory, result.Type);
    }

    [Fact]
    public void Extra_file_is_ignored_for_counting()
    {
        var line = "\t*EXTRA File \t\t      2048\tC:\\dst\\old.txt";
        var result = RobocopyOutputParser.Parse(line);

        Assert.NotEqual(RoboLineType.File, result.Type);
    }

    [Fact]
    public void Error_line_is_detected()
    {
        var line = "2026/06/14 10:00:00 ERROR 5 (0x00000005) Accessing Source Directory C:\\src\\";
        var result = RobocopyOutputParser.Parse(line);

        Assert.Equal(RoboLineType.Error, result.Type);
    }

    [Fact]
    public void Summary_totals_line_is_detected()
    {
        var line = "    Files :         3         3         0         0         0         0";
        var result = RobocopyOutputParser.Parse(line);

        Assert.Equal(RoboLineType.Summary, result.Type);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blank_lines_are_other(string? line)
    {
        var result = RobocopyOutputParser.Parse(line);
        Assert.Equal(RoboLineType.Other, result.Type);
    }

    [Fact]
    public void Banner_lines_are_other()
    {
        var result = RobocopyOutputParser.Parse("   ROBOCOPY     ::     Robust File Copy for Windows");
        Assert.Equal(RoboLineType.Other, result.Type);
    }
}

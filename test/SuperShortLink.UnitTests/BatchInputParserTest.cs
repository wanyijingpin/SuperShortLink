using System;
using Xunit;

namespace SuperShortLink.UnitTests;

public sealed class BatchInputParserTest
{
    [Fact]
    public void ParsesInlineRemarksAndPreservesSourceText()
    {
        var result = BatchInputParser.Parse("活动A https://example.com/a。\n活动B https://example.com/b", 200);

        Assert.Equal(2, result.Count);
        Assert.Equal("活动A", result[0].remark);
        Assert.Equal("https://example.com/a", result[0].url);
        Assert.Equal("活动A https://example.com/a。", result[0].source_text);
        Assert.Equal("活动B", result[1].remark);
    }

    [Fact]
    public void PairsStandaloneRemarkWithFollowingUrl()
    {
        var result = BatchInputParser.Parse("活动A\nhttps://example.com/a\n活动B\nhttps://example.com/b", 200);

        Assert.Equal(2, result.Count);
        Assert.Equal("活动A", result[0].remark);
        Assert.Equal("活动A" + Environment.NewLine + "https://example.com/a", result[0].source_text);
        Assert.Equal(1, result[0].line);
        Assert.Equal("活动B", result[1].remark);
    }

    [Fact]
    public void RejectsAmbiguousOrIncompleteInput()
    {
        Assert.Throws<FormatException>(() => BatchInputParser.Parse("活动A\n活动B\nhttps://example.com/a", 200));
        Assert.Throws<FormatException>(() => BatchInputParser.Parse("活动A", 200));
        Assert.Throws<FormatException>(() => BatchInputParser.Parse("https://example.com/a https://example.com/b", 200));
    }

    [Fact]
    public void ReturnsNoItemsForBlankInput()
    {
        var result = BatchInputParser.Parse(" \n\r\n", 200);

        Assert.Empty(result);
    }
}

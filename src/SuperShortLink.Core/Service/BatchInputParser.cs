using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SuperShortLink.Models;

namespace SuperShortLink;

public static class BatchInputParser
{
    private static readonly Regex UrlPattern = new(@"https?://[^\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<BatchInputLine> Parse(string input, int maxItems)
    {
        var rawLines = (input ?? string.Empty)
            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
            .Select((text, index) => new { Text = text.Trim(), Line = index + 1 })
            .Where(x => x.Text.Length > 0)
            .ToList();
        var result = new List<BatchInputLine>();
        string pendingRemark = null;
        int pendingLine = 0;
        foreach (var raw in rawLines)
        {
            var matches = UrlPattern.Matches(raw.Text);
            if (matches.Count > 1)
                throw new FormatException($"第 {raw.Line} 行包含多个链接，请每行只放一个链接");
            if (matches.Count == 0)
            {
                if (pendingRemark is not null)
                    throw new FormatException($"第 {raw.Line} 行无法与上一条文案唯一对应");
                pendingRemark = raw.Text;
                pendingLine = raw.Line;
                continue;
            }

            var match = matches[0];
            var url = match.Value.TrimEnd('。', '，', ',', ';', '；');
            var trailingText = raw.Text[(match.Index + match.Length)..].Trim();
            // Punctuation directly after a URL is sentence punctuation, not part of the remark.
            trailingText = trailingText.TrimStart('。', '，', ',', ';', '；');
            var inlineRemark = (raw.Text[..match.Index] + " " + trailingText).Trim();
            var remark = string.Join(" ", new[] { pendingRemark, inlineRemark }.Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
            result.Add(new BatchInputLine { line = pendingRemark is null ? raw.Line : pendingLine, source_text = pendingRemark is null ? raw.Text : pendingRemark + Environment.NewLine + raw.Text, url = url, remark = remark });
            pendingRemark = null;
            pendingLine = 0;
            if (result.Count > maxItems)
                throw new FormatException($"单次最多生成 {maxItems} 条链接");
        }
        if (pendingRemark is not null)
            throw new FormatException($"第 {pendingLine} 行只有文案，没有对应链接");
        return result;
    }
}

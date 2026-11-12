using Xunit;

namespace ASI.Basecode.IntegrationTests;

/// <summary>
/// Guards the hotspot HTTPS checklist: an unrun row must stay <c>BLOCKED</c> with the missing input
/// named, and a closed tracking issue is never proof. Update this guard only when real, dated
/// evidence replaces the placeholder rows.
/// </summary>
// PLAIN ENGLISH: This test protects the honesty of the evidence file. It fails if someone writes
// "PASS" for a phone or host check that has not actually been run, or if a blocked row stops naming
// what is missing.
public class HotspotHttpsEvidenceTests
{
    [Fact]
    public async Task ChecklistLeavesUnverifiedRowsBlockedWithoutPassOrIssueProof()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "docs", "hotspot-https-evidence.md");
        var text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

        Assert.Contains("not evidence by itself", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("may say `PASS` only when", text, StringComparison.Ordinal);
        Assert.Contains("docs/hotspot-https.md", text, StringComparison.Ordinal);
        Assert.Contains("Q2 stays open", text, StringComparison.Ordinal);

        var start = text.IndexOf("## Verification", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var section = text[start..];
        var end = section.IndexOf("\n## ", StringComparison.Ordinal);
        if (end >= 0)
        {
            section = section[..end];
        }

        var rows = section.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("|", StringComparison.Ordinal))
            .Select(line => line.Split('|'))
            .Where(cells => cells.Length >= 7)
            .Where(cells => !cells[1].Trim().Equals("#", StringComparison.Ordinal))
            .Where(cells => !cells[1].Trim().All(character => character is '-' or ':'))
            .ToList();

        Assert.NotEmpty(rows);
        foreach (var cells in rows)
        {
            Assert.Equal("BLOCKED", cells[4].Trim());
            Assert.False(string.IsNullOrWhiteSpace(cells[5]));
        }

        foreach (var required in new[]
                 {
                     "phone 1", "phone 2", "manual lookup", "camera", "offline", "certificate",
                 })
        {
            Assert.Contains(required, text, StringComparison.OrdinalIgnoreCase);
        }
    }
}

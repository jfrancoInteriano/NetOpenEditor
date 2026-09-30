using Microsoft.Playwright;

namespace NetOpenEditor.E2E.Tests;

[Collection("sample")]
public sealed class PasteTests(SampleServerFixture server)
{
    private Task<IPage> OpenAsync() => server.NewPageAsync("/journal/edit");

    [Fact]
    public async Task ParseClipboard_SplitsRowsAndCells()
    {
        var page = await OpenAsync();

        var rows = await page.EvaluateAsync<string[][]>(
            "() => NetOpenEditor.parseClipboard('a\\tb\\nc\\td').rows");

        Assert.Equal(2, rows.Length);
        Assert.Equal(["a", "b"], rows[0]);
        Assert.Equal(["c", "d"], rows[1]);
    }

    [Fact]
    public async Task ParseClipboard_NormalizesCrLf_AndDropsExcelsTrailingBlankLine()
    {
        var page = await OpenAsync();

        var count = await page.EvaluateAsync<int>(
            "() => NetOpenEditor.parseClipboard('a\\tb\\r\\nc\\td\\r\\n').rows.length");

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ParseClipboard_TruncatesAtFiveHundredRows()
    {
        var page = await OpenAsync();

        var result = await page.EvaluateAsync<int[]>(@"() => {
            const text = Array.from({ length: 520 }, (_, i) => i + '\tx').join('\n');
            const r = NetOpenEditor.parseClipboard(text);
            return [r.rows.length, r.truncated];
        }");

        Assert.Equal(500, result[0]);
        Assert.Equal(20, result[1]);
    }
}

using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

/// <summary>
/// A control on the page may hide a column while the form is open. One CSS rule on data-noe-col
/// does it: header, cells and footer go together, and the inputs stay in the DOM — so a column the
/// user hides and shows again keeps posting what was already captured.
/// </summary>
[Collection("sample")]
public sealed class ColumnVisibilityTests(SampleServerFixture server)
{
    private const string Hide = "[data-noe-col='Description'] { display: none !important; }";

    private static async Task<float> LeftAsync(IPage page, string selector)
    {
        var box = await page.Locator(selector).BoundingBoxAsync();
        Assert.NotNull(box);
        return box!.X;
    }

    [Fact]
    public async Task HidingAColumn_LeavesHeaderCellsAndTotalsAligned()
    {
        var page = await server.NewPageAsync("/journal/edit");
        await page.Locator(Cell(0, "DebitAmount")).FillAsync("25");
        await page.Keyboard.PressAsync("Tab");

        await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = Hide });
        await page.WaitForTimeoutAsync(200);

        // The hidden column is gone from all three bands...
        Assert.Equal(0, await page.Locator("[data-noe-col='Description']:visible").CountAsync());
        // ...and the one that follows it starts at the same x in the header, the body and the footer.
        var head = await LeftAsync(page, "thead th[data-noe-col='DebitAmount']");
        var body = await LeftAsync(page, "[data-noe-row='0'] td[data-noe-col='DebitAmount']");
        var foot = await LeftAsync(page, "tfoot td[data-noe-col='DebitAmount']");

        Assert.True(Math.Abs(head - body) < 1, $"header {head} vs body {body}");
        Assert.True(Math.Abs(head - foot) < 1, $"header {head} vs footer {foot}");
    }

    [Fact]
    public async Task AHiddenColumn_KeepsPostingWhatWasCaptured()
    {
        var page = await server.NewPageAsync("/journal/edit");
        await page.Locator(Cell(0, "Description")).FillAsync("capturado antes de ocultar");
        await page.Locator(Cell(0, "DebitAmount")).FillAsync("25");
        await page.Keyboard.PressAsync("Tab");

        await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = Hide });
        await page.WaitForTimeoutAsync(200);
        await page.Locator("#save").ClickAsync();

        using var json = await BoundJsonAsync(page);
        var line = json.RootElement.GetProperty("Lines")[0];
        Assert.Equal("capturado antes de ocultar", line.GetProperty("Description").GetString());
    }

    [Fact]
    public async Task ShowingTheColumnAgain_FindsTheValueIntact()
    {
        var page = await server.NewPageAsync("/journal/edit");
        await page.Locator(Cell(0, "Description")).FillAsync("sigue aquí");

        await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = Hide });
        await page.WaitForTimeoutAsync(200);
        await page.EvaluateAsync("() => { for (const s of document.querySelectorAll('style')) if (s.textContent.includes(\"data-noe-col='Description'\")) s.remove(); }");
        await page.WaitForTimeoutAsync(200);

        Assert.Equal("sigue aquí", await page.Locator(Cell(0, "Description")).InputValueAsync());
    }
}

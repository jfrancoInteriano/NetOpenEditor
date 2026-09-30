using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

[Collection("sample")]
public sealed class RowShortcutTests(SampleServerFixture server)
{
    private Task<IPage> OpenAsync() => server.NewPageAsync("/journal/edit");

    [Fact]
    public async Task CtrlD_DuplicatesTheRowBelow_AndKeepsTheColumn()
    {
        var page = await OpenAsync();
        await page.Locator(Cell(0, "Description")).ClickAsync();

        await page.Keyboard.PressAsync("Control+d");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("Apertura caja", await page.Locator(Cell(1, "Description")).InputValueAsync());
        Assert.Equal("Apertura capital", await page.Locator(Cell(2, "Description")).InputValueAsync());
        Assert.Equal("Description", await page.EvaluateAsync<string>("() => document.activeElement.dataset.noeField"));
    }

    [Fact]
    public async Task CtrlDelete_RemovesTheRow()
    {
        var page = await OpenAsync();
        await page.Locator(Cell(0, "Description")).ClickAsync();

        await page.Keyboard.PressAsync("Control+Delete");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("Apertura capital", await page.Locator(Cell(0, "Description")).InputValueAsync());
    }

    [Fact]
    public async Task CtrlEnter_InsertsAnEmptyRowAbove()
    {
        var page = await OpenAsync();
        await page.Locator(Cell(1, "Description")).ClickAsync();

        await page.Keyboard.PressAsync("Control+Enter");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal(string.Empty, await page.Locator(Cell(1, "Description")).InputValueAsync());
        Assert.Equal("Apertura capital", await page.Locator(Cell(2, "Description")).InputValueAsync());
    }

    [Fact]
    public async Task Shortcuts_DoNothingOnThePhantomRow()
    {
        var page = await OpenAsync();
        var before = await page.Locator("[data-noe-row]").CountAsync();

        await page.Locator(Cell(2, "Description")).ClickAsync();   // the phantom row
        await page.Keyboard.PressAsync("Control+d");
        await page.Keyboard.PressAsync("Control+Delete");
        await page.Keyboard.PressAsync("Control+Enter");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal(before, await page.Locator("[data-noe-row]").CountAsync());
    }

    [Fact]
    public async Task CtrlD_RecomputesTotals()
    {
        var page = await OpenAsync();
        Assert.Equal("100.00", await page.Locator("[data-noe-total='DebitAmount']").InnerTextAsync());

        await page.Locator(Cell(0, "Description")).ClickAsync();   // the row holding 100.00 of debit
        await page.Keyboard.PressAsync("Control+d");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("200.00", await page.Locator("[data-noe-total='DebitAmount']").InnerTextAsync());
    }

    // MinRows is not enforced on delete by design: the v1 spec validates it on submit
    // (covered by ValidationTests), so the user can empty the table and be told when saving.
}

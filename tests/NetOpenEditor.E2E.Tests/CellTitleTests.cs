using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

/// <summary>
/// A cell's tooltip can come from the host. The hook is read inside the binding, so what it derives
/// from the row follows the row: a stale tooltip is worse than none when it is being read off.
/// </summary>
[Collection("sample")]
public sealed class CellTitleTests(SampleServerFixture server)
{
    private static Task<string?> TitleAsync(IPage page, int row, string field) =>
        page.Locator(Cell(row, field)).GetAttributeAsync("title");

    [Fact]
    public async Task TheHookTitlesANumericCell_AndTheTitleFollowsTheRow()
    {
        var page = await server.NewPageAsync("/journal/edit");

        // The first line comes preloaded with a 100.00 debit: the hook already ran at hydration.
        Assert.Equal("Neto de la línea: 100.00", await TitleAsync(page, 0, "DebitAmount"));

        await page.Locator(Cell(0, "DebitAmount")).FillAsync("10");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("Neto de la línea: 10.00", await TitleAsync(page, 0, "DebitAmount"));
        // Same derived text on the sibling amount: the hook is asked per cell, per row.
        Assert.Equal("Neto de la línea: 10.00", await TitleAsync(page, 0, "CreditAmount"));
    }

    [Fact]
    public async Task ACellTheHookIgnores_KeepsTheDefaultTooltip()
    {
        var page = await server.NewPageAsync("/journal/edit");

        await page.Locator(Cell(0, "Description")).FillAsync("Pago de alquiler de noviembre");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(200);

        // Returning nothing leaves 1.3.0's behaviour: a text cell is readable on hover.
        Assert.Equal("Pago de alquiler de noviembre", await TitleAsync(page, 0, "Description"));
        // And a cell that had no tooltip gets none.
        Assert.Null(await TitleAsync(page, 0, "AccountId"));
    }

    [Fact]
    public async Task TheHookTitlesAComputedCell()
    {
        var page = await server.NewPageAsync("/quote/edit?id=NUEVA");

        await page.Locator(Cell(0, "Quantity")).FillAsync("2");
        await page.Keyboard.PressAsync("Tab");
        await page.Locator(Cell(0, "UnitPrice")).FillAsync("100");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("Sin impuesto: 200.00", await TitleAsync(page, 0, "LineTotal"));
    }

    [Fact]
    public async Task AnEditorWithoutTheHook_AddsNoTooltipOfItsOwn()
    {
        var page = await server.NewPageAsync("/receipt/edit");

        Assert.Null(await TitleAsync(page, 0, "Received"));
        await page.Locator(Cell(0, "Batch")).FillAsync("L-77");
        await page.WaitForTimeoutAsync(200);
        Assert.Equal("L-77", await TitleAsync(page, 0, "Batch"));
    }
}

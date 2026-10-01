using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

/// <summary>
/// An editable computed column takes typing but still posts nothing: the editor hands the raw text
/// to the host and never writes the cell itself, so compute() stays the only owner of that value.
/// </summary>
[Collection("sample")]
public sealed class EditableComputedTests(SampleServerFixture server)
{
    private Task<IPage> OpenAsync() => server.NewPageAsync("/quote/edit?id=NUEVA");

    private static Task<string?> FieldAsync(IPage page, int row, string field) =>
        page.EvaluateAsync<string?>($"() => {{ const r = NetOpenEditor.get('quote-lines').rows()[{row}]; return r ? String(r.{field} ?? '') : null; }}");

    private static async Task FillLineAsync(IPage page, string quantity, string price)
    {
        await page.Locator(Cell(0, "Quantity")).FillAsync(quantity);
        await page.Keyboard.PressAsync("Tab");
        await page.Locator(Cell(0, "UnitPrice")).FillAsync(price);
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(300);
    }

    [Fact]
    public async Task TheComputedCell_IsAnInputButCarriesNoName()
    {
        var page = await OpenAsync();

        var cell = page.Locator(Cell(0, "LineTotal"));
        Assert.Equal("input", await cell.EvaluateAsync<string>("el => el.tagName.toLowerCase()"));
        Assert.Null(await cell.GetAttributeAsync("name"));

        var names = await page.EvaluateAsync<string[]>("() => [...document.querySelectorAll('#quoteForm [name]')].map(e => e.name)");
        Assert.DoesNotContain(names, n => n.Contains("LineTotal", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TypingTheTotal_RunsTheHookWithTheRawText_AndTheHostWritesTheSibling()
    {
        var page = await OpenAsync();
        await FillLineAsync(page, "2", "100");

        // No product picked, so no tax: 460 over 2 units back-solves to 230 each.
        await page.Locator(Cell(0, "LineTotal")).FillAsync("460");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(400);

        Assert.Equal("230", await FieldAsync(page, 0, "UnitPrice"));
        Assert.Equal("230.00", await page.Locator(Cell(0, "UnitPrice")).InputValueAsync());
    }

    [Fact]
    public async Task TheRecomputedValue_WinsOverWhatWasTyped()
    {
        var page = await OpenAsync();
        await FillLineAsync(page, "2", "100");

        await page.Locator(Cell(0, "LineTotal")).FillAsync("460");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(400);

        // compute() owns the cell: after the host wrote the price, the total is recomputed, not kept.
        Assert.Equal("460.00", await page.Locator(Cell(0, "LineTotal")).InputValueAsync());
        Assert.Equal("460.00", await page.Locator("[data-noe-total='LineTotal']").InnerTextAsync());
    }

    [Fact]
    public async Task ANonEditableComputedColumn_StillRendersAsText()
    {
        // The receipt editor has no editable computed column; its totals row is plain text.
        var page = await server.NewPageAsync("/receipt/edit");

        Assert.Equal(0, await page.Locator("[data-noe-row='0'] input[data-noe-field='Ordered']").CountAsync());
    }
}

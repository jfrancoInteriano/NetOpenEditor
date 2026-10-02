using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

/// <summary>
/// A column can present a per-row button whose label depends on row state. The state lives in
/// row.__host, which the editor never posts: toggling a row must not add a field to the form.
/// </summary>
[Collection("sample")]
public sealed class AdornmentTests(SampleServerFixture server)
{
    private Task<IPage> OpenAsync() => server.NewPageAsync("/quote/edit?id=NUEVA");

    private static ILocator Button(IPage page, int row) =>
        page.Locator($"[data-noe-row='{row}'] [data-noe-field='DiscountPercent'] ~ .noe-adorn, [data-noe-row='{row}'] .noe-adorn");

    [Fact]
    public async Task TheButton_ShowsTheLabelTheHostReturns()
    {
        var page = await OpenAsync();

        Assert.Equal("%", await Button(page, 0).First.InnerTextAsync());
    }

    [Fact]
    public async Task Clicking_RunsTheHost_AndTheLabelFollowsTheRowState()
    {
        var page = await OpenAsync();

        await Button(page, 0).First.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("HNL", await Button(page, 0).First.InnerTextAsync());

        await Button(page, 0).First.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("%", await Button(page, 0).First.InnerTextAsync());
    }

    [Fact]
    public async Task TheLabelFollowsTheHeader_ForRowsAlreadyToggled()
    {
        var page = await OpenAsync();
        await Button(page, 0).First.ClickAsync();
        await page.WaitForTimeoutAsync(200);

        // The page tells the editor that the label source changed; rows already in money relabel.
        await page.SelectOptionAsync("#currency", "USD");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("USD", await Button(page, 0).First.InnerTextAsync());
    }

    [Fact]
    public async Task TheToggledState_IsNeverPosted()
    {
        var page = await OpenAsync();
        await page.Locator(Cell(0, "Quantity")).FillAsync("1");
        await Button(page, 0).First.ClickAsync();
        await page.WaitForTimeoutAsync(300);

        // No input carries the host bag, under any name.
        var names = await page.EvaluateAsync<string[]>(
            "() => [...document.querySelectorAll('#quoteForm [name]')].map(e => e.name)");

        Assert.DoesNotContain(names, n => n.Contains("__host", StringComparison.Ordinal));
        Assert.DoesNotContain(names, n => n.Contains("discountUnit", StringComparison.Ordinal));
        Assert.Contains(names, n => n.EndsWith(".DiscountPercent", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AColumnWithoutAnAdornment_RendersNoButton()
    {
        var page = await OpenAsync();

        Assert.Equal(0, await page.Locator("[data-noe-row='0'] [data-noe-field='Quantity'] ~ .noe-adorn").CountAsync());
        // Exactly one adornment per row: the discount column.
        Assert.Equal(1, await page.Locator("[data-noe-row='0'] .noe-adorn").CountAsync());
    }

    [Fact]
    public async Task DuplicatingARow_KeepsItsHostState()
    {
        var quote = await OpenAsync();

        await quote.Locator(Cell(0, "Quantity")).FillAsync("1");
        await Button(quote, 0).First.ClickAsync();
        await quote.WaitForTimeoutAsync(200);

        await quote.Locator(Cell(0, "Quantity")).ClickAsync();
        await quote.Keyboard.PressAsync("Control+d");
        await quote.WaitForTimeoutAsync(300);

        Assert.Equal("HNL", await Button(quote, 1).First.InnerTextAsync());
    }

    [Fact]
    public async Task AnEditableComputedCell_RendersBothTheInputAndTheButton()
    {
        var page = await OpenAsync();

        var cell = page.Locator(Cell(0, "DiscountShown"));
        Assert.Equal("input", await cell.EvaluateAsync<string>("el => el.tagName.toLowerCase()"));
        Assert.Null(await cell.GetAttributeAsync("name"));          // computed: never posted
        Assert.Equal("%", await Button(page, 0).First.InnerTextAsync());
    }

    [Fact]
    public async Task TheCellShowsMoneyOrPercent_ButAlwaysPostsThePercentage()
    {
        var page = await OpenAsync();
        await page.Locator(Cell(0, "Quantity")).FillAsync("2");
        await page.Keyboard.PressAsync("Tab");
        await page.Locator(Cell(0, "UnitPrice")).FillAsync("100");
        await page.Keyboard.PressAsync("Tab");

        // 10% of a 200 line
        await page.Locator(Cell(0, "DiscountShown")).FillAsync("10");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(300);
        Assert.Equal("10", await page.EvaluateAsync<string>("() => String(NetOpenEditor.get('quote-lines').rows()[0].DiscountPercent)"));

        // Flipping to money shows the amount, and typing an amount still posts a percentage.
        await Button(page, 0).First.ClickAsync();
        await page.WaitForTimeoutAsync(300);
        Assert.Equal("20.00", await page.Locator(Cell(0, "DiscountShown")).InputValueAsync());

        await page.Locator(Cell(0, "DiscountShown")).FillAsync("50");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("25", await page.EvaluateAsync<string>("() => String(NetOpenEditor.get('quote-lines').rows()[0].DiscountPercent)"));
        // The box keeps meaning money: it does not repaint itself as 25.
        Assert.Equal("50.00", await page.Locator(Cell(0, "DiscountShown")).InputValueAsync());
    }

    [Fact]
    public async Task RefreshStillRepaintsTheLabel_OnAnEditableComputedCell()
    {
        var page = await OpenAsync();
        await Button(page, 0).First.ClickAsync();
        await page.WaitForTimeoutAsync(200);

        await page.SelectOptionAsync("#currency", "USD");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("USD", await Button(page, 0).First.InnerTextAsync());
    }
}

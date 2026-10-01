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
}

using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

[Collection("sample")]
public sealed class LookupTests(SampleServerFixture server)
{
    private const string Capital = "00000000-0000-0000-0000-000000003101";

    private Task<IPage> OpenAsync() => server.NewPageAsync("/journal/edit");

    private static ILocator Hidden(IPage page, int row, string field) =>
        page.Locator($"[data-noe-row='{row}'] input[type='hidden'][name='Lines[{row}].{field}']");

    [Fact]
    public async Task TypeAndPickWithKeyboard_SetsValueLabelAndCompanions_AndPromotesPhantom()
    {
        var page = await OpenAsync();
        var input = page.Locator(Cell(2, "AccountId"));

        await input.FocusAsync();
        await page.Keyboard.TypeAsync("cap");
        await page.WaitForSelectorAsync(".noe-lookup-item.is-active");
        Assert.Contains("3101", await page.Locator(".noe-lookup-item.is-active").InnerTextAsync());

        await page.Keyboard.PressAsync("Enter");

        Assert.Equal("3101 - Capital", await input.InputValueAsync());
        Assert.Equal(Capital, await Hidden(page, 2, "AccountId").InputValueAsync());
        Assert.Equal("3101", await Hidden(page, 2, "AccountCode").InputValueAsync());
        Assert.Equal("Capital", await Hidden(page, 2, "AccountName").InputValueAsync());
        Assert.Equal(4, await page.Locator("[data-noe-row]").CountAsync());
        Assert.False(await page.Locator(".noe-lookup-panel").IsVisibleAsync());
    }

    [Fact]
    public async Task ArrowDown_MovesActiveItem_AndMouseClickPicks()
    {
        var page = await OpenAsync();
        var input = page.Locator(Cell(2, "AccountId"));

        await input.FocusAsync();                     // minLength 0: opens with all accounts
        await page.WaitForSelectorAsync(".noe-lookup-item.is-active");
        var first = await page.Locator(".noe-lookup-item.is-active").InnerTextAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        var second = await page.Locator(".noe-lookup-item.is-active").InnerTextAsync();
        Assert.NotEqual(first, second);

        await page.Locator(".noe-lookup-item").Nth(2).ClickAsync();

        Assert.Equal("1201 - Clientes", await input.InputValueAsync());
        Assert.Equal("00000000-0000-0000-0000-000000001201", await Hidden(page, 2, "AccountId").InputValueAsync());
    }

    [Fact]
    public async Task BlurWithoutPick_RestoresPreviousLabelAndValue()
    {
        var page = await OpenAsync();
        var input = page.Locator(Cell(0, "AccountId"));

        await input.FillAsync("zzz");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(250);

        Assert.Equal("1101 - Caja", await input.InputValueAsync());
        Assert.Equal("00000000-0000-0000-0000-000000001101", await Hidden(page, 0, "AccountId").InputValueAsync());
    }

    [Fact]
    public async Task ClearAndBlur_ClearsValueAndCompanions()
    {
        var page = await OpenAsync();
        var input = page.Locator(Cell(0, "AccountId"));

        await input.FillAsync("");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(250);

        Assert.Equal("", await input.InputValueAsync());
        Assert.Equal("", await Hidden(page, 0, "AccountId").InputValueAsync());
        Assert.Equal("", await Hidden(page, 0, "AccountCode").InputValueAsync());
    }

    [Fact]
    public async Task Escape_ClosesPanelFirst_ThenRevertsCell()
    {
        var page = await OpenAsync();
        var input = page.Locator(Cell(0, "AccountId"));

        await input.FillAsync("ban");   // fill focuses (snapshot = Caja) and replaces the label text
        await page.WaitForSelectorAsync(".noe-lookup-item.is-active");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("1102 - Bancos", await input.InputValueAsync());

        await page.Keyboard.TypeAsync("x");
        await page.WaitForSelectorAsync(".noe-lookup-panel:visible");
        await page.Keyboard.PressAsync("Escape");
        Assert.False(await page.Locator(".noe-lookup-panel").IsVisibleAsync());

        await page.Keyboard.PressAsync("Escape");
        Assert.Equal("1101 - Caja", await input.InputValueAsync());
        Assert.Equal("00000000-0000-0000-0000-000000001101", await Hidden(page, 0, "AccountId").InputValueAsync());
        Assert.Equal("1101", await Hidden(page, 0, "AccountCode").InputValueAsync());
    }

    [Fact]
    public async Task Panel_IsFixedPositionedSoItEscapesOverflowContainers()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(2, "AccountId")).FocusAsync();
        await page.WaitForSelectorAsync(".noe-lookup-item.is-active");

        Assert.Equal("fixed", await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.noe-lookup-panel')).position"));
        Assert.Equal("auto", await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('.card:has(.noe)')).overflowX"));
    }

    [Fact]
    public async Task NoResults_ShowsEmptyMessageAndKeepsValue()
    {
        var page = await OpenAsync();
        var input = page.Locator(Cell(0, "AccountId"));

        await input.FocusAsync();
        await page.Keyboard.TypeAsync("qqqq");
        await page.WaitForSelectorAsync(".noe-lookup-status:visible");

        Assert.Contains("Sin resultados", await page.Locator(".noe-lookup-panel").InnerTextAsync());
        await page.Keyboard.PressAsync("Enter");   // nothing to pick: value unchanged
        Assert.Equal("00000000-0000-0000-0000-000000001101", await Hidden(page, 0, "AccountId").InputValueAsync());
    }
}

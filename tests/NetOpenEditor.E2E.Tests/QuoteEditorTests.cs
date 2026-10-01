using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

[Collection("sample")]
public sealed class QuoteEditorTests(SampleServerFixture server)
{
    // A document id with no lines: these tests capture from an empty grid. "Q-1" now comes
    // preloaded by QuoteLineSource, which QuoteSourceTests covers.
    private Task<IPage> OpenAsync() => server.NewPageAsync("/quote/edit?id=NUEVA");

    [Fact]
    public async Task PickingProduct_FillsPriceTaxAndQuantity_AndComputesTotals()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(0, "ProductId")).FocusAsync();
        await page.Keyboard.TypeAsync("lap");
        await page.WaitForSelectorAsync(".noe-lookup-item.is-active");
        await page.Keyboard.PressAsync("Enter");

        Assert.Equal("1", await page.Locator(Cell(0, "Quantity")).InputValueAsync());
        Assert.Equal("1200.00", await page.Locator(Cell(0, "UnitPrice")).InputValueAsync());
        Assert.Equal("15.00", await page.Locator(Cell(0, "TaxRate")).InputValueAsync());
        Assert.Equal("1380.00", await page.Locator(Cell(0, "LineTotal")).InputValueAsync());
        Assert.Equal("1380.00", await page.Locator("[data-noe-total='LineTotal']").InnerTextAsync());

        await page.Locator(Cell(0, "Quantity")).FillAsync("3");
        await page.Keyboard.PressAsync("Tab");

        Assert.Equal("4140.00", await page.Locator(Cell(0, "LineTotal")).InputValueAsync());
        Assert.Equal("4140.00", await page.Locator("[data-noe-total='LineTotal']").InnerTextAsync());
        Assert.Equal("4140.00", await page.Locator("#grand-total").InnerTextAsync());
    }

    [Fact]
    public async Task IntegerColumn_TruncatesTheDecimalPart_WithoutClosingTheDigits()
    {
        var page = await OpenAsync();

        // "2.7" used to become "27": the separator was deleted and the digits closed up, so a
        // quantity silently turned into ten times what was typed.
        await page.Locator(Cell(0, "Quantity")).FillAsync("2.7");
        await page.Keyboard.PressAsync("Tab");

        Assert.Equal("2", await page.Locator(Cell(0, "Quantity")).InputValueAsync());
    }

    [Fact]
    public async Task Submit_PostsQuantityAsIntegerAndSkipsComputed()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(0, "ProductId")).FocusAsync();
        await page.Keyboard.TypeAsync("mou");
        await page.WaitForSelectorAsync(".noe-lookup-item.is-active");
        await page.Keyboard.PressAsync("Enter");
        await page.Locator(Cell(0, "Quantity")).FillAsync("4");
        await page.Locator("#save").ClickAsync();

        using var json = await BoundJsonAsync(page);
        var line = json.RootElement.GetProperty("Lines")[0];
        Assert.Equal(4, line.GetProperty("Quantity").GetInt32());
        Assert.Equal("P-002", line.GetProperty("ProductCode").GetString());
        Assert.False(line.TryGetProperty("LineTotal", out _));
    }

    [Fact]
    public async Task IntegerColumn_TypedCharacterByCharacter_LeavesNoTrailingSeparator()
    {
        var page = await OpenAsync();

        // Typing digit by digit keeps "2." while editing — that dangling dot is what stops the next
        // keystroke from closing up into "27" — but it must be gone once the cell loses focus.
        await page.Locator(Cell(0, "Quantity")).ClickAsync();
        await page.Locator(Cell(0, "Quantity")).PressSequentiallyAsync("2.7");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("2", await page.Locator(Cell(0, "Quantity")).InputValueAsync());
    }
}

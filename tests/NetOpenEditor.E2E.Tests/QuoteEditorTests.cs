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
        Assert.Equal("1380.00", await page.Locator("[data-noe-row='0'] .noe-num-text").InnerTextAsync());
        Assert.Equal("1380.00", await page.Locator("[data-noe-total='LineTotal']").InnerTextAsync());

        await page.Locator(Cell(0, "Quantity")).FillAsync("3");
        await page.Keyboard.PressAsync("Tab");

        Assert.Equal("4140.00", await page.Locator("[data-noe-row='0'] .noe-num-text").InnerTextAsync());
        Assert.Equal("4140.00", await page.Locator("[data-noe-total='LineTotal']").InnerTextAsync());
        Assert.Equal("4140.00", await page.Locator("#grand-total").InnerTextAsync());
    }

    [Fact]
    public async Task IntegerColumn_DropsDecimalSeparator()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(0, "Quantity")).FillAsync("2.7");
        Assert.Equal("27", await page.Locator(Cell(0, "Quantity")).InputValueAsync());
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
}

using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

[Collection("sample")]
public sealed class JournalEditorTests(SampleServerFixture server)
{
    private Task<IPage> OpenAsync() => server.NewPageAsync("/journal/edit");

    [Fact]
    public async Task InitialRender_TwoRealRowsPlusPhantomWithoutNames()
    {
        var page = await OpenAsync();

        Assert.Equal(3, await page.Locator("[data-noe-row]").CountAsync());
        Assert.Contains("noe-row-phantom", await page.Locator("[data-noe-row='2']").GetAttributeAsync("class"));
        Assert.Equal("Lines[0].Description", await page.Locator(Cell(0, "Description")).GetAttributeAsync("name"));
        Assert.Null(await page.Locator(Cell(2, "Description")).GetAttributeAsync("name"));
        Assert.Equal("1101 - Caja", await page.Locator(Cell(0, "AccountId")).InputValueAsync());
        Assert.Equal("100.00", await page.Locator(Cell(0, "DebitAmount")).InputValueAsync());
        Assert.Equal(2, await page.EvaluateAsync<int>("() => NetOpenEditor.get('journal-lines').rows().length"));
    }

    [Fact]
    public async Task TypingInPhantom_PromotesRowKeepsFocusAndAddsNewPhantom()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(2, "Description")).FillAsync("Nueva");

        Assert.Equal(4, await page.Locator("[data-noe-row]").CountAsync());
        Assert.Equal("Lines[2].Description", await page.Locator(Cell(2, "Description")).GetAttributeAsync("name"));
        Assert.Equal("Nueva", await page.Locator(Cell(2, "Description")).InputValueAsync());
        Assert.DoesNotContain("noe-row-phantom", await page.Locator("[data-noe-row='2']").GetAttributeAsync("class"));
        Assert.Contains("noe-row-phantom", await page.Locator("[data-noe-row='3']").GetAttributeAsync("class"));
        Assert.Equal("2:Description", await ActiveCellAsync(page));
    }

    [Fact]
    public async Task Enter_MovesToSameColumnNextRow_ArrowsMoveUpAndDown()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(0, "Description")).FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal("1:Description", await ActiveCellAsync(page));

        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("2:Description", await ActiveCellAsync(page));

        await page.Keyboard.PressAsync("ArrowUp");
        await page.Keyboard.PressAsync("ArrowUp");
        Assert.Equal("0:Description", await ActiveCellAsync(page));

        await page.Locator(Cell(0, "DebitAmount")).FocusAsync();
        await page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal("1:DebitAmount", await ActiveCellAsync(page));
    }

    [Fact]
    public async Task Escape_RevertsCellToValueAtFocus()
    {
        var page = await OpenAsync();
        var cell = page.Locator(Cell(0, "Description"));

        await cell.FocusAsync();
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.TypeAsync(" XYZ");
        Assert.Equal("Apertura caja XYZ", await cell.InputValueAsync());

        await page.Keyboard.PressAsync("Escape");
        Assert.Equal("Apertura caja", await cell.InputValueAsync());
    }

    [Fact]
    public async Task Decimal_CommaIsNormalizedOnBlur_AndHookZeroesOppositeColumn()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(1, "DebitAmount")).FillAsync("150,5");
        await page.Keyboard.PressAsync("Tab");

        Assert.Equal("150.50", await page.Locator(Cell(1, "DebitAmount")).InputValueAsync());
        Assert.Equal("0.00", await page.Locator(Cell(1, "CreditAmount")).InputValueAsync());
        Assert.Equal("250.50", await page.Locator("[data-noe-total='DebitAmount']").InnerTextAsync());
        Assert.Equal("0.00", await page.Locator("[data-noe-total='CreditAmount']").InnerTextAsync());
        Assert.Equal("250.50", await page.Locator("#diff").InnerTextAsync());
    }

    [Fact]
    public async Task Integer_LikeInputRejectsLetters()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(0, "DebitAmount")).FillAsync("12ab.5x");
        Assert.Equal("12.5", await page.Locator(Cell(0, "DebitAmount")).InputValueAsync());
    }

    [Fact]
    public async Task RemoveRow_RenumbersNamesAndKeepsPhantom()
    {
        var page = await OpenAsync();

        await page.Locator("[data-noe-row='0'] .noe-btn-remove").ClickAsync();

        Assert.Equal(2, await page.Locator("[data-noe-row]").CountAsync());
        Assert.Equal("Apertura capital", await page.Locator(Cell(0, "Description")).InputValueAsync());
        Assert.Equal("Lines[0].Description", await page.Locator(Cell(0, "Description")).GetAttributeAsync("name"));
        Assert.Contains("noe-row-phantom", await page.Locator("[data-noe-row='1']").GetAttributeAsync("class"));
        Assert.Equal(0, await page.Locator("[data-noe-row='1'] .noe-btn-remove:visible").CountAsync());
    }

    [Fact]
    public async Task CtrlDelete_RemovesCurrentRow()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(1, "Description")).FocusAsync();
        await page.Keyboard.PressAsync("Control+Delete");

        Assert.Equal(2, await page.Locator("[data-noe-row]").CountAsync());
        Assert.Equal("Apertura caja", await page.Locator(Cell(0, "Description")).InputValueAsync());
    }

    [Fact]
    public async Task Submit_PostsOnlyRealRowsWithNormalizedDecimals()
    {
        var page = await OpenAsync();
        await page.Locator(Cell(0, "DebitAmount")).FillAsync("100,5");   // not blurred on purpose
        await page.Locator(Cell(1, "CreditAmount")).FillAsync("100.5");

        await page.Locator("#save").ClickAsync();

        using var json = await BoundJsonAsync(page);
        var lines = json.RootElement.GetProperty("Lines");
        Assert.Equal(2, lines.GetArrayLength());
        Assert.Equal(100.5m, lines[0].GetProperty("DebitAmount").GetDecimal());
        Assert.Equal("00000000-0000-0000-0000-000000001101", lines[0].GetProperty("AccountId").GetString());
        Assert.Equal("1101", lines[0].GetProperty("AccountCode").GetString());
        Assert.Equal(100.5m, lines[1].GetProperty("CreditAmount").GetDecimal());
    }

    [Fact]
    public async Task PublicApi_AddRowAndSetRecalculateTotals()
    {
        var page = await OpenAsync();

        await page.EvaluateAsync("() => { const ed = NetOpenEditor.get('journal-lines'); const row = ed.addRow({ Description: 'API', DebitAmount: 25 }); ed.set(row, 'DebitAmount', 50); }");

        Assert.Equal(4, await page.Locator("[data-noe-row]").CountAsync());
        Assert.Equal("50.00", await page.Locator(Cell(2, "DebitAmount")).InputValueAsync());
        Assert.Equal("150.00", await page.Locator("[data-noe-total='DebitAmount']").InnerTextAsync());
    }
}

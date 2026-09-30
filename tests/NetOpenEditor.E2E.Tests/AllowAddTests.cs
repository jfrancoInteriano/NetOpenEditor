using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

/// <summary>
/// An editor registered with AllowAdd(false) never grows. There are four paths that create a row —
/// the initial phantom, the replacement phantom on promote, Ctrl+Enter and Ctrl+D — plus paste and
/// the public addRow(); each one gets its own assertion so a failure names the path that regressed.
/// </summary>
[Collection("sample")]
public sealed class AllowAddTests(SampleServerFixture server)
{
    private static string PasteScript(string selector, string text) => $$"""
        () => {
            const el = document.querySelector("{{selector}}");
            el.focus();
            const dt = new DataTransfer();
            dt.setData('text/plain', {{text}});
            el.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }));
        }
        """;

    private Task<IPage> OpenAsync() => server.NewPageAsync("/receipt/edit");

    [Fact]
    public async Task NoPhantomRow_IsRenderedOnLoad()
    {
        var page = await OpenAsync();

        // Three ordered lines and nothing else: no trailing empty row inviting a fourth.
        Assert.Equal(3, await page.Locator("[data-noe-row]").CountAsync());
        Assert.Equal(0, await page.Locator(".noe-row-phantom").CountAsync());
    }

    [Fact]
    public async Task TypingInTheLastRow_DoesNotCreateAnother()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(2, "Batch")).FillAsync("L-9981");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal(3, await page.Locator("[data-noe-row]").CountAsync());
        Assert.Equal("L-9981", await page.Locator(Cell(2, "Batch")).InputValueAsync());
    }

    [Fact]
    public async Task CtrlEnter_DoesNotInsertARow()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(1, "Batch")).ClickAsync();
        await page.Keyboard.PressAsync("Control+Enter");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal(3, await page.Locator("[data-noe-row]").CountAsync());
    }

    [Fact]
    public async Task CtrlD_DoesNotDuplicateARow()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(1, "Batch")).ClickAsync();
        await page.Keyboard.PressAsync("Control+d");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal(3, await page.Locator("[data-noe-row]").CountAsync());
    }

    [Fact]
    public async Task PastingMoreRowsThanExist_ClampsToTheExistingOnes()
    {
        var page = await OpenAsync();

        // Five pasted lines over three existing rows: the extra two have nowhere to go.
        await page.EvaluateAsync(PasteScript(
            "[data-noe-row='0'] [data-noe-field='Batch']",
            "'L-1\\nL-2\\nL-3\\nL-4\\nL-5'"));
        await page.WaitForTimeoutAsync(500);

        Assert.Equal(3, await page.Locator("[data-noe-row]").CountAsync());
        Assert.Equal("L-3", await page.Locator(Cell(2, "Batch")).InputValueAsync());
    }

    [Fact]
    public async Task PublicAddRow_DoesNothingAndReturnsNull()
    {
        var page = await OpenAsync();

        var returned = await page.EvaluateAsync<bool>(
            "() => NetOpenEditor.get('receipt-lines').addRow({ Batch: 'X' }) === null");

        Assert.True(returned);
        Assert.Equal(3, await page.Locator("[data-noe-row]").CountAsync());
    }

    [Fact]
    public async Task CellsStayEditable_AndTotalsStillRun()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(0, "Received")).FillAsync("3");
        await page.Keyboard.PressAsync("Tab");
        await page.Locator(Cell(1, "Received")).FillAsync("7");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("10.00", await page.Locator("[data-noe-total='Received']").InnerTextAsync());
    }

    [Fact]
    public async Task AnEditorWithoutAllowAdd_StillGrows()
    {
        // The default is unchanged: the journal editor keeps its phantom row.
        var page = await server.NewPageAsync("/journal/edit");

        Assert.Equal(1, await page.Locator(".noe-row-phantom").CountAsync());

        await page.Locator(Cell(2, "Description")).FillAsync("Nueva");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal(4, await page.Locator("[data-noe-row]").CountAsync());
    }

    [Fact]
    public async Task WithNoRowsAtAll_TheBodyShowsTheEmptyMessage()
    {
        // The phantom used to be the only thing keeping the body non-empty.
        var page = await server.Browser.NewPageAsync();
        await page.GotoAsync(server.BaseUrl + "/receipt/edit?empty=true");
        await page.WaitForSelectorAsync(".noe-table");
        await page.WaitForTimeoutAsync(400);

        Assert.Equal(0, await page.Locator("[data-noe-row]").CountAsync());
        Assert.Equal("Sin líneas", await page.Locator(".noe-row-empty").InnerTextAsync());
    }
}

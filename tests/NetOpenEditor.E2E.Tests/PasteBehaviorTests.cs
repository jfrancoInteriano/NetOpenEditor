using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

[Collection("sample")]
public sealed class PasteBehaviorTests(SampleServerFixture server)
{
    // Reading the real clipboard needs browser permissions; dispatching the event with its own
    // DataTransfer exercises the same handler without making the test environment-dependent.
    private static string PasteScript(string selector, string text) => $$"""
        () => {
            const el = document.querySelector("{{selector}}");
            el.focus();
            const dt = new DataTransfer();
            dt.setData('text/plain', {{text}});
            el.dispatchEvent(new ClipboardEvent('paste', { clipboardData: dt, bubbles: true, cancelable: true }));
        }
        """;

    [Fact]
    public async Task Paste_ThreeByTwo_FillsRowsAndKeepsThePhantomLast()
    {
        var page = await server.NewPageAsync("/journal/edit");

        await page.EvaluateAsync(PasteScript(
            "[data-noe-row='0'] [data-noe-field='Description']",
            "'Uno\\t10\\nDos\\t20\\nTres\\t30'"));
        await page.WaitForTimeoutAsync(400);

        Assert.Equal("Uno", await page.Locator(Cell(0, "Description")).InputValueAsync());
        Assert.Equal("Tres", await page.Locator(Cell(2, "Description")).InputValueAsync());
        Assert.Equal("30.00", await page.Locator(Cell(2, "DebitAmount")).InputValueAsync());
        Assert.Equal(string.Empty, await page.Locator(Cell(3, "Description")).InputValueAsync());
    }

    [Fact]
    public async Task Paste_DecimalWithThousandsSeparator_IsNormalized()
    {
        var page = await server.NewPageAsync("/journal/edit");

        await page.EvaluateAsync(PasteScript(
            "[data-noe-row='0'] [data-noe-field='Description']",
            "'x\\t1.234,56'"));
        await page.WaitForTimeoutAsync(400);

        Assert.Equal("1234.56", await page.Locator(Cell(0, "DebitAmount")).InputValueAsync());
    }

    [Fact]
    public async Task Paste_SingleCell_IsNotIntercepted()
    {
        var page = await server.NewPageAsync("/journal/edit");
        var target = page.Locator(Cell(0, "Description"));
        await target.FillAsync("previo");

        await page.EvaluateAsync(PasteScript("[data-noe-row='0'] [data-noe-field='Description']", "'solo'"));
        await page.WaitForTimeoutAsync(300);

        // The handler returns without preventDefault; headless Chromium inserts nothing on a
        // synthetic event, so the field keeps what it had instead of being overwritten by the block logic.
        Assert.Equal("previo", await target.InputValueAsync());
    }

    [Fact]
    public async Task Paste_EmitsASingleChangeEvent()
    {
        var page = await server.NewPageAsync("/journal/edit");
        await page.EvaluateAsync("() => { window.__noeChanges = 0; document.addEventListener('noe:change', () => window.__noeChanges++); }");

        await page.EvaluateAsync(PasteScript(
            "[data-noe-row='0'] [data-noe-field='Description']",
            "'Uno\\t10\\nDos\\t20'"));
        await page.WaitForTimeoutAsync(400);

        Assert.Equal(1, await page.EvaluateAsync<int>("() => window.__noeChanges"));
    }

    [Fact]
    public async Task Paste_OverAComputedColumn_DiscardsThatValue()
    {
        var page = await server.NewPageAsync("/quote/edit?id=NUEVA");

        // Quantity, UnitPrice, TaxRate, then a value landing on the computed LineTotal.
        await page.EvaluateAsync(PasteScript(
            "[data-noe-row='0'] [data-noe-field='Quantity']",
            "'2\\t50\\t0\\t999999'"));
        await page.WaitForTimeoutAsync(400);

        Assert.Equal("2", await page.Locator(Cell(0, "Quantity")).InputValueAsync());
        Assert.Equal("50.00", await page.Locator(Cell(0, "UnitPrice")).InputValueAsync());
        // The computed column keeps quantity * price, not the pasted number.
        Assert.Equal("100.00", await page.Locator("[data-noe-row='0'] .noe-num-text").InnerTextAsync());
    }
}

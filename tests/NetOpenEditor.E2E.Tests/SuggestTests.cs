using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

/// <summary>
/// A Suggest column posts the typed text. The type-ahead only offers to fill it in: text that
/// matches no suggestion is a valid answer, and nothing — blur, paste or reload — may rewrite it.
/// </summary>
[Collection("sample")]
public sealed class SuggestTests(SampleServerFixture server)
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

    private Task<IPage> OpenAsync() => server.NewPageAsync("/request/edit");

    private static async Task TypeAsync(IPage page, int row, string text)
    {
        await page.Locator(Cell(row, "Description")).ClickAsync();
        await page.Locator(Cell(row, "Description")).PressSequentiallyAsync(text);
        await page.WaitForTimeoutAsync(500);
    }

    private static Task<string?> FieldAsync(IPage page, int row, string field) =>
        page.EvaluateAsync<string?>($"() => {{ const r = NetOpenEditor.get('request-lines').rows()[{row}]; return r ? (r.{field} ?? null) : null; }}");

    [Fact]
    public async Task TypedTextThatMatchesNothing_SurvivesTheBlur_AndIsPosted()
    {
        var page = await OpenAsync();

        await TypeAsync(page, 0, "Tubo PVC 4 pulgadas");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(400);

        Assert.Equal("Tubo PVC 4 pulgadas", await page.Locator(Cell(0, "Description")).InputValueAsync());
        Assert.Equal("Tubo PVC 4 pulgadas", await FieldAsync(page, 0, "Description"));
        Assert.Null(await FieldAsync(page, 0, "ProductId"));
    }

    [Fact]
    public async Task PickingASuggestion_WritesTheLabel_AndLetsTheHookFillTheSiblings()
    {
        var page = await OpenAsync();

        await TypeAsync(page, 0, "Lap");
        await page.WaitForSelectorAsync(".noe-lookup-item");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(400);

        Assert.Equal("Laptop", await page.Locator(Cell(0, "Description")).InputValueAsync());
        Assert.False(string.IsNullOrEmpty(await FieldAsync(page, 0, "ProductId")));
        Assert.Equal("UND", await FieldAsync(page, 0, "UnitOfMeasure"));
    }

    [Fact]
    public async Task TheHookDoesNotOverwriteWhatTheUserAlreadyTyped()
    {
        var page = await OpenAsync();
        await page.Locator(Cell(0, "UnitOfMeasure")).ClickAsync();
        await page.Locator(Cell(0, "UnitOfMeasure")).PressSequentiallyAsync("CAJA");

        await TypeAsync(page, 0, "Lap");
        await page.WaitForSelectorAsync(".noe-lookup-item");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(400);

        // The hook guards with "if (!row.UnitOfMeasure)", which a blanket Companion copy could not.
        Assert.Equal("CAJA", await FieldAsync(page, 0, "UnitOfMeasure"));
    }

    [Fact]
    public async Task EditingTheTextAfterPicking_KeepsTheIdsTheHookWrote()
    {
        var page = await OpenAsync();
        await TypeAsync(page, 0, "Lap");
        await page.WaitForSelectorAsync(".noe-lookup-item");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(300);
        var pickedId = await FieldAsync(page, 0, "ProductId");

        await page.Locator(Cell(0, "Description")).ClickAsync();
        await page.Locator(Cell(0, "Description")).PressSequentiallyAsync(" (reacondicionada)");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(400);

        Assert.Equal("Laptop (reacondicionada)", await FieldAsync(page, 0, "Description"));
        Assert.Equal(pickedId, await FieldAsync(page, 0, "ProductId"));
    }

    [Fact]
    public async Task PastedText_StaysText_WithNoRemoteResolution()
    {
        var page = await OpenAsync();
        var requests = 0;
        page.Request += (_, r) => { if (r.Url.Contains("/products/suggest", StringComparison.Ordinal)) requests++; };

        await page.EvaluateAsync(PasteScript(
            "[data-noe-row='0'] [data-noe-field='Description']",
            "'Manguera de 1/2\\tListón\\n'"));
        await page.WaitForTimeoutAsync(800);

        Assert.Equal("Manguera de 1/2", await FieldAsync(page, 0, "Description"));
        Assert.Equal(0, requests);
        Assert.Equal(0, await page.Locator("[data-noe-row='0'] .noe-error:visible").CountAsync());
    }

    [Fact]
    public async Task TheSearchCarriesTheHeaderParameter()
    {
        var page = await OpenAsync();
        var urls = new List<string>();
        page.Request += (_, r) => { if (r.Url.Contains("/products/suggest", StringComparison.Ordinal)) urls.Add(r.Url); };

        await TypeAsync(page, 0, "Mon");

        Assert.Contains(urls, u => u.Contains("providerId=PROV-A", StringComparison.Ordinal));
        // Monitor belongs to the other provider, so the filtered search offers nothing.
        Assert.Equal(0, await page.Locator(".noe-lookup-item").CountAsync());
    }

    [Fact]
    public async Task ALookupInAnotherEditor_StillRevertsItsText()
    {
        // The lookup branch is untouched: its text is display, so leaving a non-matching value reverts.
        var page = await server.NewPageAsync("/journal/edit");

        await page.Locator(Cell(0, "AccountId")).ClickAsync();
        await page.Locator(Cell(0, "AccountId")).FillAsync("texto inventado");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(500);

        Assert.Equal("1101 - Caja", await page.Locator(Cell(0, "AccountId")).InputValueAsync());
    }

    [Fact]
    public async Task ASuggestAndALookup_InTheSameEditor_EachPickWithTheirOwnRules()
    {
        var page = await OpenAsync();

        // Use the suggest first: it leaves the shared panel in "suggest" mode.
        await TypeAsync(page, 0, "Lap");
        await page.WaitForSelectorAsync(".noe-lookup-item");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(300);
        Assert.Equal("Laptop", await page.Locator(Cell(0, "Description")).InputValueAsync());

        // Now the lookup in the same row must behave as a lookup: picking fills key and companion.
        await page.Locator(Cell(0, "AccountId")).ClickAsync();
        await page.Locator(Cell(0, "AccountId")).PressSequentiallyAsync("caj");
        await page.WaitForSelectorAsync(".noe-lookup-item");
        await page.Keyboard.PressAsync("Enter");
        await page.WaitForTimeoutAsync(300);

        Assert.Equal("1101 - Caja", await page.Locator(Cell(0, "AccountId")).InputValueAsync());
        Assert.Equal("1101", await FieldAsync(page, 0, "AccountCode"));
        // And the suggest cell kept its text: the lookup pick did not run through the suggest path.
        Assert.Equal("Laptop", await FieldAsync(page, 0, "Description"));
    }

    [Fact]
    public async Task ALongValue_IsReadableOnHover()
    {
        var page = await OpenAsync();

        await TypeAsync(page, 0, "Tubería de PVC de 4 pulgadas, cédula 40, por unidad");

        Assert.Equal("Tubería de PVC de 4 pulgadas, cédula 40, por unidad",
            await page.Locator(Cell(0, "Description")).GetAttributeAsync("title"));
    }
}

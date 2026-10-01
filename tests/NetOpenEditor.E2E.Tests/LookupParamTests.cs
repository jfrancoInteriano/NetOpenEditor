using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

/// <summary>
/// Lookup parameters are resolved on every search, not captured once: the picker follows a header
/// field while the page is open. Both fetch paths — the type-ahead and the paste resolver — must
/// carry them, or a paste quietly matches rows the filter excludes.
/// </summary>
[Collection("sample")]
public sealed class LookupParamTests(SampleServerFixture server)
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

    private Task<IPage> OpenAsync() => server.NewPageAsync("/quote/edit?id=NUEVA");

    private static async Task<IReadOnlyList<string>> SearchAsync(IPage page, string term)
    {
        await page.Locator(Cell(0, "ProductId")).FocusAsync();
        await page.Locator(Cell(0, "ProductId")).FillAsync(string.Empty);
        await page.Locator(Cell(0, "ProductId")).PressSequentiallyAsync(term);
        await page.WaitForTimeoutAsync(600);
        return await page.Locator(".noe-lookup-item").AllInnerTextsAsync();
    }

    [Fact]
    public async Task ChangingTheHeaderField_ChangesTheResults_WithNoReload()
    {
        var page = await OpenAsync();

        var forProviderA = await SearchAsync(page, "P-");
        await page.SelectOptionAsync("#provider", "PROV-B");
        var forProviderB = await SearchAsync(page, "P-");

        Assert.Contains(forProviderA, i => i.Contains("Laptop", StringComparison.Ordinal));
        Assert.DoesNotContain(forProviderA, i => i.Contains("Monitor", StringComparison.Ordinal));
        Assert.Contains(forProviderB, i => i.Contains("Monitor", StringComparison.Ordinal));
        Assert.DoesNotContain(forProviderB, i => i.Contains("Laptop", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheRequestCarriesTheParameter()
    {
        var page = await OpenAsync();
        var urls = new List<string>();
        page.Request += (_, r) => { if (r.Url.Contains("/products/lookup", StringComparison.Ordinal)) urls.Add(r.Url); };

        await SearchAsync(page, "Lap");

        Assert.Contains(urls, u => u.Contains("providerId=PROV-A", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PasteResolvesAgainstTheSameFilteredSet()
    {
        var page = await OpenAsync();
        await page.SelectOptionAsync("#provider", "PROV-B");

        // P-001 belongs to the other provider: with the filter applied it must not resolve.
        await page.EvaluateAsync(PasteScript(
            "[data-noe-row='0'] [data-noe-field='ProductId']", "'P-001\\tx\\n'"));
        await page.WaitForTimeoutAsync(900);

        var productId = await page.EvaluateAsync<string?>(
            "() => NetOpenEditor.get('quote-lines').rows()[0]?.ProductId");
        Assert.True(string.IsNullOrEmpty(productId));
        Assert.Contains("P-001", await page.Locator("[data-noe-row='0'] .noe-error:visible").First.InnerTextAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PasteResolves_WhenTheValueBelongsToTheSelectedProvider()
    {
        var page = await OpenAsync();   // starts on PROV-A

        await page.EvaluateAsync(PasteScript(
            "[data-noe-row='0'] [data-noe-field='ProductId']", "'P-001\\tx\\n'"));
        await page.WaitForTimeoutAsync(900);

        Assert.Equal("P-001 - Laptop", await page.Locator(Cell(0, "ProductId")).InputValueAsync());
    }

    [Fact]
    public async Task AnEditorWithoutParams_SendsTheSameRequestAsBefore()
    {
        var page = await server.NewPageAsync("/journal/edit");
        var urls = new List<string>();
        page.Request += (_, r) => { if (r.Url.Contains("/accounts/lookup", StringComparison.Ordinal)) urls.Add(r.Url); };

        await page.Locator(Cell(0, "AccountId")).FocusAsync();
        await page.Locator(Cell(0, "AccountId")).FillAsync(string.Empty);
        await page.Locator(Cell(0, "AccountId")).PressSequentiallyAsync("caj");
        await page.WaitForTimeoutAsync(600);

        Assert.NotEmpty(urls);
        Assert.All(urls, u => Assert.EndsWith("/accounts/lookup?term=caj", u, StringComparison.Ordinal));
    }
}

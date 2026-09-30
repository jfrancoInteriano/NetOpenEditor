using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

/// <summary>
/// Pasting into a lookup column: the text is resolved against the same endpoint the picker uses,
/// and only an exact, single match is applied.
/// </summary>
[Collection("sample")]
public sealed class LookupPasteTests(SampleServerFixture server)
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

    private const string AccountCell = "[data-noe-row='2'] [data-noe-field='AccountId']";

    [Fact]
    public async Task ExactCode_ResolvesTheAccount_AndFillsItsCompanions()
    {
        var page = await server.NewPageAsync("/journal/edit");

        // Two lines pasted onto the phantom row: code, description, debit.
        await page.EvaluateAsync(PasteScript(AccountCell, "'1102\\tDepósito\\t50\\n1201\\tCobro\\t75'"));
        await page.WaitForSelectorAsync("[data-noe-row='3']");
        await page.WaitForTimeoutAsync(800);   // resolution happens after the block is applied

        Assert.Equal("1102 - Bancos", await page.Locator(Cell(2, "AccountId")).InputValueAsync());
        Assert.Equal("1201 - Clientes", await page.Locator(Cell(3, "AccountId")).InputValueAsync());

        // The row now holds the same id and companions that picking by hand would produce.
        var row = await page.EvaluateAsync<string>(
            "() => JSON.stringify(NetOpenEditor.get('journal-lines').rows()[2])");
        Assert.Contains("\"AccountCode\":\"1102\"", row, StringComparison.Ordinal);
        Assert.Contains("\"AccountName\":\"Bancos\"", row, StringComparison.Ordinal);
        Assert.DoesNotContain("\"AccountId\":null", row, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownCode_LeavesTheCellInError_KeepingWhatWasPasted()
    {
        var page = await server.NewPageAsync("/journal/edit");

        await page.EvaluateAsync(PasteScript(AccountCell, "'9999\\tNo existe\\t10'"));
        await page.WaitForTimeoutAsync(800);

        Assert.Equal("9999", await page.Locator(Cell(2, "AccountId")).InputValueAsync());
        Assert.Contains("9999", await page.Locator("[data-noe-row='2'] .noe-error:visible").First.InnerTextAsync(), StringComparison.Ordinal);
        Assert.Contains("noe-invalid", await page.Locator(Cell(2, "AccountId")).GetAttributeAsync("class") ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AmbiguousTerm_IsReportedInsteadOfGuessing()
    {
        var page = await server.NewPageAsync("/journal/edit");

        // The sample catalog has unique codes, so force the ambiguous shape at the transport level:
        // two items whose code equals the pasted text.
        await page.RouteAsync("**/accounts/lookup*", async route => await route.FulfillAsync(new()
        {
            ContentType = "application/json",
            Body = """
                [{"accountId":"11111111-1111-1111-1111-111111111111","code":"DUP","name":"Uno","display":"DUP - Uno"},
                 {"accountId":"22222222-2222-2222-2222-222222222222","code":"DUP","name":"Dos","display":"DUP - Dos"}]
                """,
        }));

        await page.EvaluateAsync(PasteScript(AccountCell, "'DUP\\tAmbiguo\\t10'"));
        await page.WaitForTimeoutAsync(800);

        var error = await page.Locator("[data-noe-row='2'] .noe-error:visible").First.InnerTextAsync();
        Assert.Contains("DUP", error, StringComparison.Ordinal);
        // Nothing was guessed: the cell holds no id.
        var accountId = await page.EvaluateAsync<string?>(
            "() => NetOpenEditor.get('journal-lines').rows()[2].AccountId");
        Assert.True(string.IsNullOrEmpty(accountId));
    }

    [Fact]
    public async Task Resolution_EmitsOneExtraChangeEvent_AfterTheBlock()
    {
        var page = await server.NewPageAsync("/journal/edit");
        await page.EvaluateAsync("() => { window.__noeChanges = 0; document.addEventListener('noe:change', () => window.__noeChanges++); }");

        await page.EvaluateAsync(PasteScript(AccountCell, "'1102\\tDepósito\\t50\\n1201\\tCobro\\t75'"));
        await page.WaitForTimeoutAsync(800);

        // One for the pasted block, one after the lookups resolve.
        Assert.Equal(2, await page.EvaluateAsync<int>("() => window.__noeChanges"));
    }

    [Fact]
    public async Task RepeatedCodes_AreRequestedOnce()
    {
        var page = await server.NewPageAsync("/journal/edit");
        var requests = 0;
        page.Request += (_, request) =>
        {
            // Focusing a lookup cell opens its panel, which searches on its own; count only the
            // resolution requests for the pasted term.
            if (request.Url.Contains("/accounts/lookup", StringComparison.Ordinal)
                && request.Url.Contains("term=1102", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref requests);
            }
        };

        await page.EvaluateAsync(PasteScript(AccountCell, "'1102\\tUno\\t10\\n1102\\tDos\\t20\\n1102\\tTres\\t30'"));
        await page.WaitForTimeoutAsync(900);

        Assert.Equal(1, requests);
        Assert.Equal("1102 - Bancos", await page.Locator(Cell(4, "AccountId")).InputValueAsync());
    }

    [Fact]
    public async Task Paste_ClosesTheLookupPanelLeftOpenByTheFocusedCell()
    {
        var page = await server.NewPageAsync("/journal/edit");

        // Leave the picker open on the account cell with no matches, the way a user does before pasting.
        await page.Locator(Cell(2, "AccountId")).FocusAsync();
        await page.Keyboard.TypeAsync("zzz");
        await page.WaitForSelectorAsync(".noe-lookup-panel");
        Assert.True(await page.Locator(".noe-lookup-panel").IsVisibleAsync());

        await page.EvaluateAsync(PasteScript(AccountCell, "'1102\\tDepósito\\t50\\n1201\\tCobro\\t75'"));
        await page.WaitForTimeoutAsync(900);

        Assert.False(await page.Locator(".noe-lookup-panel").IsVisibleAsync());
    }

    [Fact]
    public async Task Escape_ClosesTheLookupPanel_EvenFromAnotherCell()
    {
        var page = await server.NewPageAsync("/journal/edit");
        await page.Locator(Cell(2, "AccountId")).FocusAsync();
        await page.Keyboard.TypeAsync("zzz");
        await page.WaitForSelectorAsync(".noe-lookup-panel");

        // The panel outlives the focus: pressing Escape anywhere in the editor must dismiss it.
        await page.Locator(Cell(0, "Description")).FocusAsync();
        await page.Keyboard.PressAsync("Escape");
        await page.WaitForTimeoutAsync(300);

        Assert.False(await page.Locator(".noe-lookup-panel").IsVisibleAsync());
    }
}

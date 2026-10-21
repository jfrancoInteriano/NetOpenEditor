using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

/// <summary>
/// What the host can see and do from the page: a hook that fails says so, writing through the API
/// says what it does, the lookup endpoint can move, and a read-only number is still a number.
/// </summary>
[Collection("sample")]
public sealed class HostApiTests(SampleServerFixture server)
{
    [Fact]
    public async Task AHookThatThrows_NamesTheHookAndTheRow_AndTheEditorKeepsWorking()
    {
        var page = await server.NewPageAsync("/journal/edit");
        var errors = new List<string>();
        page.Console += (_, m) => { if (m.Type == "error") errors.Add(m.Text); };

        // A typo inside a hook: the reported symptom was a calculation that silently stopped.
        await page.EvaluateAsync("() => NetOpenEditor.configure('journal-lines', { onCellChange(row) { row.missing.field = 1; } })");

        await page.Locator(Cell(0, "Description")).FillAsync("sigue viva");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(300);

        Assert.Contains(errors, e => e.Contains("onCellChange", StringComparison.Ordinal) && e.Contains("row 1", StringComparison.Ordinal));
        // The editor did not fall over: the cell kept the value and the totals still ran.
        Assert.Equal("sigue viva", await page.Locator(Cell(0, "Description")).InputValueAsync());
        Assert.Equal("100.00", await page.Locator("[data-noe-total='DebitAmount']").InnerTextAsync());
    }

    [Fact]
    public async Task Set_StaysQuiet_AndSetWithNotify_RunsOnCellChange()
    {
        var page = await server.NewPageAsync("/journal/edit");
        await page.EvaluateAsync("() => { window.__seen = []; NetOpenEditor.configure('journal-lines', { onCellChange(row, field) { window.__seen.push(field); } }); }");

        await page.EvaluateAsync("() => { const ed = NetOpenEditor.get('journal-lines'); ed.set(ed.rows()[0], 'Description', 'uno'); }");
        Assert.Empty(await page.EvaluateAsync<string[]>("() => window.__seen"));

        await page.EvaluateAsync("() => { const ed = NetOpenEditor.get('journal-lines'); ed.set(ed.rows()[0], 'Description', 'dos', { notify: true }); }");
        Assert.Equal(["Description"], await page.EvaluateAsync<string[]>("() => window.__seen"));
        Assert.Equal("dos", await page.Locator(Cell(0, "Description")).InputValueAsync());
    }

    [Fact]
    public async Task Refresh_RunsComputeAgain_WhileRecalcOnlyAddsTheTotalsUp()
    {
        var page = await server.NewPageAsync("/journal/edit");
        await page.EvaluateAsync("() => { window.__computes = 0; NetOpenEditor.configure('journal-lines', { compute() { window.__computes++; } }); }");

        await page.EvaluateAsync("() => NetOpenEditor.get('journal-lines').recalc()");
        Assert.Equal(0, await page.EvaluateAsync<int>("() => window.__computes"));

        await page.EvaluateAsync("() => NetOpenEditor.get('journal-lines').refresh()");
        Assert.Equal(2, await page.EvaluateAsync<int>("() => window.__computes"));
    }

    [Fact]
    public async Task TheLookupUrlHook_MovesTheEndpointWithoutRebuildingTheEditor()
    {
        var page = await server.NewPageAsync("/journal/edit");
        var urls = new List<string>();
        page.Request += (_, r) => { if (r.Url.Contains("/accounts/lookup", StringComparison.Ordinal)) urls.Add(r.Url); };

        await page.EvaluateAsync("() => NetOpenEditor.configure('journal-lines', { lookupUrl: (row, field, url) => url + '?scope=alt' })");

        await page.Locator(Cell(2, "AccountId")).ClickAsync();
        await page.Locator(Cell(2, "AccountId")).PressSequentiallyAsync("caj");
        await page.WaitForSelectorAsync(".noe-lookup-item");

        Assert.Contains(urls, u => u.Contains("scope=alt", StringComparison.Ordinal) && u.Contains("term=caj", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AReadOnlyNumericColumn_ShowsItsFormat_CarriesItsTotal_AndStillPosts()
    {
        var page = await server.NewPageAsync("/receipt/edit");

        // No input to type into, but the number is formatted like any other numeric cell.
        Assert.Equal(0, await page.Locator("[data-noe-row='0'] input[data-noe-field='Ordered']").CountAsync());
        Assert.Equal("3.00", await page.Locator("[data-noe-row='0'] td[data-noe-col='Ordered'] .noe-num-text").InnerTextAsync());
        // 3 + 10 + 2: a read-only column can carry a total again.
        Assert.Equal("15.00", await page.Locator("[data-noe-total='Ordered']").InnerTextAsync());

        await page.Locator(Cell(0, "Received")).FillAsync("2");
        await page.Keyboard.PressAsync("Tab");
        await page.Locator("#save").ClickAsync();

        using var json = await BoundJsonAsync(page);
        Assert.Equal(3, json.RootElement.GetProperty("Lines")[0].GetProperty("Ordered").GetDecimal());
    }
}

using Microsoft.Playwright;

namespace NetOpenEditor.E2E.Tests;

[Collection("sample")]
public sealed class DarkModeTests(SampleServerFixture server)
{
    private static async Task<string> HeadBackgroundAsync(IPage page) =>
        await page.Locator(".noe-th").First.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");

    private async Task<IPage> OpenWithSystemDarkAsync()
    {
        // The fixture's NewPageAsync navigates, so emulate the OS setting on a page of our own first.
        var page = await server.Browser.NewPageAsync();
        await page.EmulateMediaAsync(new() { ColorScheme = ColorScheme.Dark });
        await page.GotoAsync($"{server.BaseUrl}/journal/edit");
        await page.WaitForSelectorAsync(".noe-th");
        return page;
    }

    [Fact]
    public async Task DarkClassOnTheDocument_SwitchesThePalette()
    {
        var page = await server.NewPageAsync("/journal/edit");
        var light = await HeadBackgroundAsync(page);

        await page.EvaluateAsync("() => document.documentElement.classList.add('dark')");
        var dark = await HeadBackgroundAsync(page);

        Assert.NotEqual(light, dark);
    }

    [Fact]
    public async Task SystemDarkMode_AloneDoesNotDarkenTheEditor()
    {
        // The bug this pins: a light host page on a dark-themed machine used to render a dark
        // editor inside it. The host's theme decides, not the OS.
        var page = await OpenWithSystemDarkAsync();
        var systemDark = await HeadBackgroundAsync(page);

        await page.EvaluateAsync("() => document.documentElement.classList.add('dark')");
        var hostDark = await HeadBackgroundAsync(page);

        Assert.NotEqual(hostDark, systemDark);
    }

    [Fact]
    public async Task NoeAuto_OptsIntoTheSystemSetting()
    {
        var page = await OpenWithSystemDarkAsync();
        var before = await HeadBackgroundAsync(page);

        await page.EvaluateAsync("() => document.body.classList.add('noe-auto')");
        var after = await HeadBackgroundAsync(page);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public async Task NoeLight_ForcesLightInsideADarkDocument()
    {
        var page = await server.NewPageAsync("/journal/edit");
        await page.EvaluateAsync("() => document.documentElement.classList.add('dark')");
        var dark = await HeadBackgroundAsync(page);

        await page.EvaluateAsync("() => document.querySelector('.noe').classList.add('noe-light')");
        var forced = await HeadBackgroundAsync(page);

        Assert.NotEqual(dark, forced);
    }
}

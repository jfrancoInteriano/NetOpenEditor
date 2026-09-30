using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NetOpenEditor.Example;

namespace NetOpenEditor.E2E.Tests;

/// <summary>Boots the sample app on Kestrel (random port) and one headless Chromium for the whole collection.</summary>
public sealed class SampleServerFixture : IAsyncLifetime
{
    private WebApplication? _app;
    private IPlaywright? _playwright;

    public string BaseUrl { get; private set; } = string.Empty;
    public IBrowser Browser { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _app = SampleApp.Build(new WebApplicationOptions
        {
            Args = ["--urls", "http://127.0.0.1:0"],
            // MVC discovers controllers and compiled views from the assembly named here (not from the test host).
            ApplicationName = typeof(SampleApp).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
        });
        await _app.StartAsync();
        BaseUrl = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        _playwright?.Dispose();
        if (_app is not null) await _app.StopAsync();
    }

    /// <summary>Opens a fresh page, navigates, and waits until Alpine rendered the first editor row.</summary>
    public async Task<IPage> NewPageAsync(string path)
    {
        var page = await Browser.NewPageAsync();
        page.Console += (_, message) =>
        {
            if (message.Type == "error") Console.WriteLine("[browser] " + message.Text);
        };
        page.PageError += (_, error) => Console.WriteLine("[browser:pageerror] " + error);
        await page.GotoAsync(BaseUrl + path);
        await page.WaitForSelectorAsync("[data-noe-row='0']");
        return page;
    }

    public static string Cell(int row, string field) => $"[data-noe-row='{row}'] [data-noe-field='{field}']";

    public static async Task<JsonDocument> BoundJsonAsync(IPage page)
    {
        await page.WaitForSelectorAsync("#bound-json");
        return JsonDocument.Parse(await page.Locator("#bound-json").InnerTextAsync());
    }

    public static Task<string> ActiveCellAsync(IPage page) =>
        page.EvaluateAsync<string>("() => { const el = document.activeElement; const tr = el && el.closest('tr'); return (tr ? tr.getAttribute('data-noe-row') : '-') + ':' + (el ? el.getAttribute('data-noe-field') : '-'); }");
}

[CollectionDefinition("sample")]
public sealed class SampleCollection : ICollectionFixture<SampleServerFixture>;

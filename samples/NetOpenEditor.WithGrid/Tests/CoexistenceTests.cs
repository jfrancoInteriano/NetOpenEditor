using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;

namespace NetOpenEditor.WithGrid.Tests;

/// <summary>Boots this demo on Kestrel plus one headless Chromium, like the main E2E fixture does.</summary>
public sealed class DemoServerFixture : IAsyncLifetime
{
    private WebApplication? _app;
    private IPlaywright? _playwright;

    public string BaseUrl { get; private set; } = string.Empty;
    public IBrowser Browser { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _app = DemoApp.Build(new WebApplicationOptions
        {
            Args = ["--urls", "http://127.0.0.1:0"],
            ApplicationName = typeof(DemoApp).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
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
}

/// <summary>
/// The editor lives inside the grid: expanding a row inserts a detail row into the grid's own
/// tbody and renders the editor there, on top of NetOpenGrid 1.0.3's fragment rendering.
/// </summary>
public sealed class CoexistenceTests(DemoServerFixture server) : IClassFixture<DemoServerFixture>
{
    private static string Toggle(string id) => $".doc-toggle[data-doc-id='{id}']";
    private static string Detail(string id) => $"tr[data-detail-for='{id}']";

    private async Task<IPage> OpenGridAsync()
    {
        var page = await server.Browser.NewPageAsync();
        await page.GotoAsync(server.BaseUrl + "/documents");
        await page.WaitForSelectorAsync(".doc-toggle");
        return page;
    }

    private static async Task ExpandAsync(IPage page, string id)
    {
        await page.Locator(Toggle(id)).ClickAsync();
        await page.WaitForSelectorAsync($"{Detail(id)} [data-noe-row='0']");
    }

    [Fact]
    public async Task ExpandingARow_RendersTheEditorInsideTheGrid()
    {
        var page = await OpenGridAsync();

        // Nothing is expanded on load, and the grid is a fragment of this page, not an iframe.
        Assert.Equal(0, await page.Locator("iframe").CountAsync());
        Assert.Equal(0, await page.Locator("[data-noe-row]").CountAsync());

        await ExpandAsync(page, "DOC-3");

        // The detail row belongs to the grid's own tbody, and the editor renders inside it.
        Assert.Equal(1, await page.Locator($"#documents-body > {Detail("DOC-3")}").CountAsync());
        Assert.Equal("Fertilizante 50kg",
            await page.Locator($"{Detail("DOC-3")} [data-noe-row='0'] [data-noe-field='Description']").InputValueAsync());
        Assert.Equal("Cerrar", await page.Locator(Toggle("DOC-3")).InnerTextAsync());
    }

    [Fact]
    public async Task ExpandingASecondRow_ClosesTheFirst()
    {
        var page = await OpenGridAsync();
        await ExpandAsync(page, "DOC-1");

        await ExpandAsync(page, "DOC-5");

        Assert.Equal(0, await page.Locator(Detail("DOC-1")).CountAsync());
        Assert.Equal(1, await page.Locator(Detail("DOC-5")).CountAsync());
    }

    [Fact]
    public async Task ClickingTheSameRowAgain_CollapsesIt()
    {
        var page = await OpenGridAsync();
        await ExpandAsync(page, "DOC-2");

        await page.Locator(Toggle("DOC-2")).ClickAsync();
        await page.WaitForTimeoutAsync(200);

        Assert.Equal(0, await page.Locator(Detail("DOC-2")).CountAsync());
        Assert.Equal("Editar líneas", await page.Locator(Toggle("DOC-2")).InnerTextAsync());
    }

    [Theory]
    [InlineData("DOC-1")]
    [InlineData("DOC-2")]
    [InlineData("DOC-3")]
    [InlineData("DOC-4")]
    [InlineData("DOC-5")]
    public async Task EveryRow_ExpandsWithLinesToEdit(string id)
    {
        var page = await OpenGridAsync();

        await ExpandAsync(page, id);

        Assert.NotEmpty(await page.Locator($"{Detail(id)} [data-noe-row='0'] [data-noe-field='Description']").InputValueAsync());
    }

    [Fact]
    public async Task Saving_PostsTheLines_AndRefreshesTheRowTotal()
    {
        var page = await OpenGridAsync();
        await ExpandAsync(page, "DOC-4");
        Assert.Equal("560.00", await page.Locator("[data-total-for='DOC-4']").InnerTextAsync());

        // 2 x 300 + 1 x 60 = 660
        await page.Locator($"{Detail("DOC-4")} [data-noe-row='0'] [data-noe-field='UnitPrice']").FillAsync("300");
        await page.Keyboard.PressAsync("Tab");
        await page.Locator($"{Detail("DOC-4")} button[type='submit']").ClickAsync();

        await page.WaitForFunctionAsync(
            "() => document.querySelector(\"[data-total-for='DOC-4']\")?.textContent === '660.00'");
    }

    [Fact]
    public async Task OnePage_LoadsOneAlpineAndOneHtmx()
    {
        var page = await OpenGridAsync();

        Assert.Equal(1, await page.EvaluateAsync<int>("() => document.querySelectorAll('script[src*=\"alpine\"]').length"));
        Assert.Equal(1, await page.EvaluateAsync<int>("() => document.querySelectorAll('script[src*=\"htmx\"]').length"));
        Assert.True(await page.EvaluateAsync<bool>("() => !!window.Alpine && !!window.Alpine.version"));
        Assert.True(await page.EvaluateAsync<bool>("() => !!window.htmx"));
    }

    [Fact]
    public async Task TheDarkClass_SwitchesTheEmbeddedEditorToo()
    {
        var page = await OpenGridAsync();
        await ExpandAsync(page, "DOC-1");

        var light = await page.Locator(".noe-th").First.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        await page.EvaluateAsync("() => document.documentElement.classList.add('dark')");
        var dark = await page.Locator(".noe-th").First.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");

        Assert.NotEqual(light, dark);
    }

    [Fact]
    public async Task EditorTotals_ArePaintedWhenTheRowExpands()
    {
        var page = await OpenGridAsync();

        await ExpandAsync(page, "DOC-1");
        await page.WaitForTimeoutAsync(300);

        // 1200.00 + 25.50, summed by the editor and painted by the host through noe:ready.
        Assert.Equal("1225.50", await page.Locator($"{Detail("DOC-1")} [data-doc-total]").InnerTextAsync());
    }
}

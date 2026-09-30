using System.Net;
using NetOpenEditor.Options;
using Microsoft.Extensions.DependencyInjection;
using NetOpenEditor.Assets;

namespace NetOpenEditor.Integration.Tests;

public sealed class AssetEndpointTests : IClassFixture<SampleAppFactory>
{
    private readonly HttpClient _client;
    private readonly SampleAppFactory _factory;

    public AssetEndpointTests(SampleAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Script_IsServedImmutableWhenVersionMatches()
    {
        var response = await _client.GetAsync($"/_noe/netopeneditor.js?v={EmbeddedEditorAssets.Script.Version}");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/javascript", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("immutable", response.Headers.CacheControl!.ToString());
        Assert.Equal($"\"{EmbeddedEditorAssets.Script.Version}\"", response.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task Stylesheet_IsNoCacheWithoutVersion()
    {
        var response = await _client.GetAsync("/_noe/netopeneditor.css");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/css", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoCache);
    }

    [Fact]
    public async Task EmbeddedStylesheet_StaysMounted_EvenWhenCssPathIsSet()
    {
        var options = _factory.Services.GetRequiredService<NetOpenEditorAssetOptions>();
        options.CssPath = "/css";
        try
        {
            var response = await _client.GetAsync("/_noe/netopeneditor.css");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            options.CssPath = null;
        }
    }
}

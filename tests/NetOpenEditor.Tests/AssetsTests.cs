using System.Text.RegularExpressions;
using NetOpenEditor.Assets;
using NetOpenEditor.Options;
using NetOpenEditor.Rendering;

namespace NetOpenEditor.Tests;

public sealed class AssetsTests
{
    [Fact]
    public void EmbeddedAssets_AreLoadedWithShortShaVersion()
    {
        Assert.NotEmpty(EmbeddedEditorAssets.Script.Bytes);
        Assert.NotEmpty(EmbeddedEditorAssets.Stylesheet.Bytes);
        Assert.Matches("^[0-9a-f]{12}$", EmbeddedEditorAssets.Script.Version);
        Assert.Matches("^[0-9a-f]{12}$", EmbeddedEditorAssets.Stylesheet.Version);
        Assert.Equal("text/javascript; charset=utf-8", EmbeddedEditorAssets.Script.ContentType);
        Assert.Equal("text/css; charset=utf-8", EmbeddedEditorAssets.Stylesheet.ContentType);
    }

    [Fact]
    public void Head_EmitsLinkAndDeferredScriptWithVersion()
    {
        var html = EditorAssetTags.Head(new NetOpenEditorAssetOptions { AssetPrefix = "/assets/noe" });

        Assert.Equal(
            $"<link rel=\"stylesheet\" href=\"/assets/noe/netopeneditor.css?v={EmbeddedEditorAssets.Stylesheet.Version}\">" +
            $"<script src=\"/assets/noe/netopeneditor.js?v={EmbeddedEditorAssets.Script.Version}\" defer></script>",
            html);
    }

    [Fact]
    public void Stylesheet_HidesCloakedElements()
    {
        var css = System.Text.Encoding.UTF8.GetString(EmbeddedEditorAssets.Stylesheet.Bytes);
        Assert.True(Regex.IsMatch(css, @"\[x-cloak\]\s*\{\s*display\s*:\s*none"), "stylesheet must hide [x-cloak] elements");
    }

    [Fact]
    public void Head_WithoutCssPath_LinksTheEmbeddedStylesheet()
    {
        var html = EditorAssetTags.Head(new NetOpenEditorAssetOptions());

        Assert.Contains("/_noe/netopeneditor.css?v=", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/css", "/css/netopeneditor-default.css")]
    [InlineData("css/", "/css/netopeneditor-default.css")]
    public void Head_WithCssPath_LinksTheHostStylesheet(string cssPath, string expected)
    {
        var html = EditorAssetTags.Head(new NetOpenEditorAssetOptions { CssPath = cssPath });

        Assert.Contains(expected, html, StringComparison.Ordinal);
        Assert.DoesNotContain("/_noe/netopeneditor.css", html, StringComparison.Ordinal);
        Assert.Contains("/_noe/netopeneditor.js?v=", html, StringComparison.Ordinal);   // el script nunca cambia de origen
    }

    [Fact]
    public void CssFilePrefix_AppliesOnlyWithCssPath()
    {
        var options = new NetOpenEditorAssetOptions { CssFilePrefix = "erp-" };
        Assert.Contains("/_noe/netopeneditor.css", EditorAssetTags.Head(options), StringComparison.Ordinal);

        options.CssPath = "/css";
        Assert.Contains("/css/erp-default.css", EditorAssetTags.Head(options), StringComparison.Ordinal);
    }

    [Fact]
    public void CssPath_EmptyOrWhitespace_FallsBackToEmbedded()
    {
        Assert.Null(new NetOpenEditorAssetOptions { CssPath = "   " }.CssPath);
    }
}

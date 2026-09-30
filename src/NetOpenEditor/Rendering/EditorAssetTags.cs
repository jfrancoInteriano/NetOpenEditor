using NetOpenEditor.Assets;
using NetOpenEditor.Options;

namespace NetOpenEditor.Rendering;

public static class EditorAssetTags
{
    /// <summary>Stylesheet link + deferred runtime script. Place in &lt;head&gt;; Alpine.js must load after it (deferred, end of body).</summary>
    public static string Head(NetOpenEditorAssetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var prefix = options.AssetPrefix;
        var href = options.CssPath is { } cssPath
            ? $"{cssPath}/{options.CssFilePrefix}default.css"
            : $"{prefix}/netopeneditor.css?v={EmbeddedEditorAssets.Stylesheet.Version}";
        return
            $"<link rel=\"stylesheet\" href=\"{href}\">" +
            $"<script src=\"{prefix}/netopeneditor.js?v={EmbeddedEditorAssets.Script.Version}\" defer></script>";
    }
}

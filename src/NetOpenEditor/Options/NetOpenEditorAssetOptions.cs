namespace NetOpenEditor.Options;

public sealed class NetOpenEditorAssetOptions
{
    private string _assetPrefix = "/_noe";
    private string? _cssPath;

    /// <summary>Route prefix for netopeneditor.js / netopeneditor.css. Normalized to "/x/y" (leading slash, no trailing slash).</summary>
    public string AssetPrefix
    {
        get => _assetPrefix;
        set
        {
            var trimmed = (value ?? string.Empty).Trim().Trim('/');
            _assetPrefix = trimmed.Length == 0 ? "/_noe" : "/" + trimmed;
        }
    }

    /// <summary>
    /// When set, the stylesheet link points at this host-served path instead of the embedded CSS
    /// (the embedded route stays mounted either way). Normalized to "/x/y"; empty means null.
    /// </summary>
    public string? CssPath
    {
        get => _cssPath;
        set
        {
            var trimmed = (value ?? string.Empty).Trim().Trim('/');
            _cssPath = trimmed.Length == 0 ? null : "/" + trimmed;
        }
    }

    /// <summary>File-name prefix of your own stylesheet. Only applies when <see cref="CssPath"/> is set.</summary>
    public string CssFilePrefix { get; set; } = "netopeneditor-";
}

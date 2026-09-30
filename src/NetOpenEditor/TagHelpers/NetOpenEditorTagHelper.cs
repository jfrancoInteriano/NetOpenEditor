using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using NetOpenEditor.Rendering;
using NetOpenEditor.Runtime;

namespace NetOpenEditor.TagHelpers;

/// <summary>
/// Renders one registered editor inside a view. Emits no wrapper element of its own; the host's
/// card/section controls layout. Head assets belong once per page via <see cref="EditorAssetTags.Head"/>.
/// </summary>
[HtmlTargetElement("netopen-editor", TagStructure = TagStructure.WithoutEndTag)]
public sealed class NetOpenEditorTagHelper : TagHelper
{
    /// <summary>Id passed to <c>AddEditor&lt;TLine&gt;(id, ...)</c>.</summary>
    [HtmlAttributeName("editor-id")]
    public string EditorId { get; set; } = string.Empty;

    /// <summary>The line collection (IEnumerable of the registered line type). Null renders only the phantom row.</summary>
    [HtmlAttributeName("rows")]
    public object? Rows { get; set; }

    /// <summary>Form prefix: inputs post as "{NamePrefix}[i].{Field}". Must match the header DTO's list property.</summary>
    [HtmlAttributeName("name-prefix")]
    public string NamePrefix { get; set; } = "Lines";

    /// <summary>Document key handed to the editor's keyed <see cref="Runtime.IEditorLineSource{TLine}"/> when the view passes no rows.</summary>
    [HtmlAttributeName("key")]
    public string? Key { get; set; }

    /// <summary>Comma-separated fields rendered as hidden inputs (still posted) in this view.</summary>
    [HtmlAttributeName("hide-columns")]
    public string? HideColumns { get; set; }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = default!;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.TagName = null;

        if (string.IsNullOrWhiteSpace(EditorId))
        {
            throw new InvalidOperationException(
                "<netopen-editor> requires an editor-id attribute naming an editor registered with AddNetOpenEditor().AddEditor<TLine>(id, ...).");
        }

        var http = ViewContext?.HttpContext
            ?? throw new InvalidOperationException("<netopen-editor> requires an active ViewContext with an HttpContext.");

        var runtime = http.RequestServices.GetKeyedService<IEditorRuntime>(EditorId)
            ?? throw new InvalidOperationException(
                $"No editor registered with id '{EditorId}'. Register it with AddNetOpenEditor().AddEditor<TLine>(\"{EditorId}\", ...).");

        var hidden = (HideColumns ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        var renderContext = new EditorRenderContext
        {
            NamePrefix = string.IsNullOrWhiteSpace(NamePrefix) ? "Lines" : NamePrefix,
            Errors = ModelStateErrors.Collect(ViewContext.ViewData?.ModelState ?? ViewContext.ModelState, string.IsNullOrWhiteSpace(NamePrefix) ? "Lines" : NamePrefix),
            HiddenColumns = hidden,
        };

        var hasKey = !string.IsNullOrWhiteSpace(Key);
        if (Rows is not null && hasKey)
        {
            throw new InvalidOperationException(
                $"<netopen-editor editor-id=\"{EditorId}\"> got both rows and key. Pass rows (the bound model) or key (load from the line source), not both.");
        }

        var html = Rows is null && hasKey
            ? await runtime.RenderFromSourceAsync(Key!, renderContext, http.RequestServices, http.RequestAborted)
            : await runtime.RenderAsync(Rows, renderContext, http.RequestAborted);
        output.Content.SetHtmlContent(html);
    }
}

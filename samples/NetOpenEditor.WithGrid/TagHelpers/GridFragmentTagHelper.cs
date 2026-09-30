using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using NetOpenGrid.Infrastructure.Binding;
using NetOpenGrid.Infrastructure.Runtime;

namespace NetOpenEditor.WithGrid.TagHelpers;

/// <summary>
/// Renders one NetOpenGrid instance inside this app's own view, the way a host app would wrap
/// NetOpenGrid in its own tag helper. Emits no wrapper element and no head assets: those
/// belong once per page, in _Layout, so two grids never load the runtime twice.
/// </summary>
[HtmlTargetElement("grid-fragment", TagStructure = TagStructure.NormalOrSelfClosing)]
public sealed class GridFragmentTagHelper(IServiceProvider services, IHttpContextAccessor http) : TagHelper
{
    [HtmlAttributeName("grid-id")]
    public string GridId { get; set; } = string.Empty;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;   // the host view's markup owns the layout

        var runtime = services.GetKeyedService<IGridRuntime>(GridId)
            ?? throw new InvalidOperationException(
                $"No grid registered with id '{GridId}'. Register it with AddNetOpenGrid().AddGrid<T>(\"{GridId}\", ...).");

        var httpContext = http.HttpContext
            ?? throw new InvalidOperationException("<grid-fragment> requires an active HttpContext.");

        // Read the query string so a deep link (?page=2&sort=…) server-renders on first paint.
        var values = httpContext.Request.ToGridRequestValues();
        output.Content.SetHtmlContent(await runtime.RenderFragmentAsync(values, httpContext.RequestAborted));
    }
}

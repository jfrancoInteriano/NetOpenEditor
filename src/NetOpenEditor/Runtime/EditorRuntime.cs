using Microsoft.Extensions.DependencyInjection;
using NetOpenEditor.Options;
using NetOpenEditor.Rendering;

namespace NetOpenEditor.Runtime;

public sealed class EditorRuntime<TLine> : IEditorRuntime
{
    private readonly EditorHtmlRenderer<TLine> _renderer;

    public EditorRuntime(EditorOptions<TLine> options, NetOpenEditorLocalizationOptions localization)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentNullException.ThrowIfNull(localization);
        _renderer = new EditorHtmlRenderer<TLine>(options, localization.Effective);
    }

    public EditorOptions<TLine> Options { get; }

    public string Id => Options.Id;

    public ValueTask<string> RenderAsync(object? rows, EditorRenderContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var typed = rows switch
        {
            null => [],
            IEnumerable<TLine> lines => lines,
            _ => throw new InvalidOperationException(
                $"Editor '{Id}' expects rows of type IEnumerable<{typeof(TLine).Name}> but received {rows.GetType().Name}."),
        };

        return ValueTask.FromResult(_renderer.Render(typed, context));
    }

    public async ValueTask<string> RenderFromSourceAsync(
        string key, EditorRenderContext context, IServiceProvider requestServices, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requestServices);

        var source = requestServices.GetKeyedService<IEditorLineSource<TLine>>(Id)
            ?? throw new InvalidOperationException(
                $"Editor '{Id}' was rendered with a key but no IEditorLineSource<{typeof(TLine).Name}> is registered for it. " +
                $"Register it with .AddEditor<{typeof(TLine).Name}>(\"{Id}\", ...).FromSource<TSource>().");

        var rows = await source.LoadAsync(key, cancellationToken).ConfigureAwait(false);
        return await RenderAsync(rows, context, cancellationToken).ConfigureAwait(false);
    }
}

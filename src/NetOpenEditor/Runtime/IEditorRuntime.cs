namespace NetOpenEditor.Runtime;

/// <summary>One registered editor. Resolved from keyed DI by editor id.</summary>
public interface IEditorRuntime
{
    string Id { get; }

    /// <summary>Renders the editor fragment. <paramref name="rows"/> must be an IEnumerable of the registered line type (null = no rows).</summary>
    ValueTask<string> RenderAsync(object? rows, EditorRenderContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders the editor with the lines its keyed <see cref="IEditorLineSource{TLine}"/> returns for
    /// <paramref name="key"/>. The source is scoped, so it is resolved from
    /// <paramref name="requestServices"/> per request, never cached in this singleton.
    /// </summary>
    ValueTask<string> RenderFromSourceAsync(
        string key, EditorRenderContext context, IServiceProvider requestServices, CancellationToken cancellationToken = default);
}

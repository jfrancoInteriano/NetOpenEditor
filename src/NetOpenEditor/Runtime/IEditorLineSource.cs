namespace NetOpenEditor.Runtime;

/// <summary>
/// Provides the initial lines for an editor when the view does not pass them explicitly.
/// Registered keyed by editor id with <see cref="DependencyInjection.NetOpenEditorBuilder{TLine}.FromSource{TSource}"/>.
/// </summary>
public interface IEditorLineSource<TLine>
{
    /// <summary>Loads the lines for <paramref name="key"/>: an opaque document key the host defines.</summary>
    ValueTask<IReadOnlyList<TLine>> LoadAsync(string key, CancellationToken cancellationToken = default);
}

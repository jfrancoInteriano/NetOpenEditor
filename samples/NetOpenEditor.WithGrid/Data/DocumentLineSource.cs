using NetOpenEditor.Runtime;
using NetOpenEditor.WithGrid.Models;

namespace NetOpenEditor.WithGrid.Data;

/// <summary>The editor pulls its lines from here, the same way the grid pulls rows from its data source.</summary>
public sealed class DocumentLineSource : IEditorLineSource<DocumentLine>
{
    public ValueTask<IReadOnlyList<DocumentLine>> LoadAsync(string key, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(DocumentCatalog.LinesOf(key));
}

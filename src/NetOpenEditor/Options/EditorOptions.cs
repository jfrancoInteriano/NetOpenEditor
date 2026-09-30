using NetOpenEditor.Columns;

namespace NetOpenEditor.Options;

public sealed class EditorOptions<TLine>
{
    public required string Id { get; init; }
    public required IReadOnlyList<EditorColumn<TLine>> Columns { get; init; }
    public int MinRows { get; init; }
}

using NetOpenEditor.Columns;

namespace NetOpenEditor.Options;

public sealed class EditorOptions<TLine>
{
    public required string Id { get; init; }
    public required IReadOnlyList<EditorColumn<TLine>> Columns { get; init; }
    public int MinRows { get; init; }

    /// <summary>Whether the user can create rows. Default true; false hides the trailing empty row and blocks every add path.</summary>
    public bool AllowAdd { get; init; } = true;
}

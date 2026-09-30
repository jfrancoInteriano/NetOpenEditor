using NetOpenEditor.Columns;

namespace NetOpenEditor.Options;

internal interface IEditorColumnBuilder<TLine>
{
    string Field { get; }
    IReadOnlyList<(string Field, Func<TLine, object?> Getter)> Companions { get; }
    EditorColumn<TLine> Build(string editorId);
}

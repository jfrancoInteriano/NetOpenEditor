using System.Linq.Expressions;
using System.Text.RegularExpressions;
using NetOpenEditor.Columns;

namespace NetOpenEditor.Options;

public sealed partial class EditorOptionsBuilder<TLine>
{
    private readonly string _id;
    private readonly List<IEditorColumnBuilder<TLine>> _columns = [];
    private int _minRows;

    public EditorOptionsBuilder(string id)
    {
        if (id is null || !IdPattern().IsMatch(id))
        {
            throw new EditorConfigurationException($"Editor id '{id}' is invalid. Use ^[A-Za-z][A-Za-z0-9_-]{{0,63}}$.");
        }

        _id = id;
    }

    public string Id => _id;

    public EditorOptionsBuilder<TLine> Column<TProp>(
        Expression<Func<TLine, TProp>> selector,
        Action<EditorColumnBuilder<TLine, TProp>>? configure = null)
    {
        var (field, getter) = MemberSelector.Compile(selector);
        var builder = new EditorColumnBuilder<TLine, TProp>(field, getter, fixedKind: null);
        configure?.Invoke(builder);
        _columns.Add(builder);
        return this;
    }

    /// <summary>Client-computed column (filled by the <c>compute</c> hook). Never posted.</summary>
    public EditorOptionsBuilder<TLine> Computed(
        string field,
        string header,
        Action<EditorColumnBuilder<TLine, decimal>>? configure = null)
    {
        if (field is null || !IdentifierPattern().IsMatch(field))
        {
            throw new EditorConfigurationException($"Editor '{_id}': computed field '{field}' must be a valid identifier.");
        }

        var builder = new EditorColumnBuilder<TLine, decimal>(field, _ => null, EditorKind.Computed);
        builder.Header(header);
        configure?.Invoke(builder);
        _columns.Add(builder);
        return this;
    }

    public EditorOptionsBuilder<TLine> MinRows(int minRows)
    {
        if (minRows < 0) throw new EditorConfigurationException($"Editor '{_id}': MinRows cannot be negative.");
        _minRows = minRows;
        return this;
    }

    public EditorOptions<TLine> Build()
    {
        var built = new List<EditorColumn<TLine>>(_columns.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var column in _columns)
        {
            if (!seen.Add(column.Field))
            {
                throw new EditorConfigurationException($"Editor '{_id}': column '{column.Field}' is declared twice.");
            }

            built.Add(column.Build(_id));
        }

        foreach (var column in _columns)
        {
            foreach (var (field, getter) in column.Companions)
            {
                if (!seen.Add(field)) continue;
                built.Add(new EditorColumn<TLine> { Field = field, Header = field, Kind = EditorKind.Hidden, Getter = getter });
            }
        }

        if (built.Count == 0)
        {
            throw new EditorConfigurationException($"Editor '{_id}' needs at least one column.");
        }

        return new EditorOptions<TLine> { Id = _id, Columns = built, MinRows = _minRows };
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]{0,63}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex IdentifierPattern();
}

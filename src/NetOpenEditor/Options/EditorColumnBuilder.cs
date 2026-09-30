using System.Text;
using NetOpenEditor.Columns;

namespace NetOpenEditor.Options;

public sealed class EditorColumnBuilder<TLine, TProp> : IEditorColumnBuilder<TLine>
{
    private readonly string _field;
    private readonly Func<TLine, object?> _getter;
    private readonly bool _kindLocked;
    private EditorKind? _kind;
    private string? _header;
    private string? _placeholder;
    private string? _width;
    private CellAlign? _align;
    private int _decimals = 2;
    private bool _required;
    private bool _total;
    private IReadOnlyList<SelectOption> _options = [];
    private LookupBuilder<TLine>? _lookup;

    internal EditorColumnBuilder(string field, Func<TLine, object?> getter, EditorKind? fixedKind)
    {
        _field = field;
        _getter = getter;
        _kind = fixedKind;
        _kindLocked = fixedKind is not null;
    }

    public string Field => _field;

    public EditorColumnBuilder<TLine, TProp> Header(string header) { _header = header; return this; }
    public EditorColumnBuilder<TLine, TProp> Placeholder(string placeholder) { _placeholder = placeholder; return this; }
    public EditorColumnBuilder<TLine, TProp> Width(string css) { _width = css; return this; }
    public EditorColumnBuilder<TLine, TProp> Align(CellAlign align) { _align = align; return this; }
    public EditorColumnBuilder<TLine, TProp> Required() { _required = true; return this; }
    public EditorColumnBuilder<TLine, TProp> Total() { _total = true; return this; }

    public EditorColumnBuilder<TLine, TProp> Decimals(int decimals)
    {
        if (decimals is < 0 or > 6) throw new EditorConfigurationException($"Column '{_field}': Decimals must be between 0 and 6.");
        _decimals = decimals;
        return this;
    }

    public EditorColumnBuilder<TLine, TProp> Text() => Kind(EditorKind.Text);
    public EditorColumnBuilder<TLine, TProp> Integer() { if (!_kindLocked) Kind(EditorKind.Integer); _decimals = 0; return this; }
    public EditorColumnBuilder<TLine, TProp> Decimal(int decimals = 2) { if (!_kindLocked) Kind(EditorKind.Decimal); return Decimals(decimals); }
    public EditorColumnBuilder<TLine, TProp> Date() => Kind(EditorKind.Date);
    public EditorColumnBuilder<TLine, TProp> Toggle() => Kind(EditorKind.Toggle);
    public EditorColumnBuilder<TLine, TProp> ReadOnly() => Kind(EditorKind.ReadOnly);
    public EditorColumnBuilder<TLine, TProp> Hidden() => Kind(EditorKind.Hidden);

    public EditorColumnBuilder<TLine, TProp> Select(IEnumerable<SelectOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Kind(EditorKind.Select);
        _options = options.ToArray();
        return this;
    }

    public EditorColumnBuilder<TLine, TProp> Lookup(string url, Action<LookupBuilder<TLine>>? configure = null)
    {
        Kind(EditorKind.Lookup);
        _lookup = new LookupBuilder<TLine>(url);
        configure?.Invoke(_lookup);
        return this;
    }

    IReadOnlyList<(string Field, Func<TLine, object?> Getter)> IEditorColumnBuilder<TLine>.Companions => _lookup?.Companions ?? [];

    EditorColumn<TLine> IEditorColumnBuilder<TLine>.Build(string editorId)
    {
        var kind = _kind ?? InferKind(editorId);

        if (kind == EditorKind.Toggle && _required)
            throw new EditorConfigurationException($"Editor '{editorId}': column '{_field}' is a Toggle and cannot be Required.");
        if (_total && kind is not (EditorKind.Integer or EditorKind.Decimal or EditorKind.Computed))
            throw new EditorConfigurationException($"Editor '{editorId}': column '{_field}' has Total() but is not numeric.");
        if (kind == EditorKind.Select && _options.Count == 0)
            throw new EditorConfigurationException($"Editor '{editorId}': Select column '{_field}' needs at least one option.");

        return new EditorColumn<TLine>
        {
            Field = _field,
            Header = _header ?? Humanize(_field),
            Kind = kind,
            Getter = _getter,
            Label = _lookup?.LabelSelector,
            Align = _align ?? (kind is EditorKind.Integer or EditorKind.Decimal or EditorKind.Computed ? CellAlign.End : CellAlign.Start),
            WidthCss = _width,
            Decimals = kind == EditorKind.Integer ? 0 : _decimals,
            Required = _required,
            Total = _total,
            Placeholder = _placeholder,
            Options = _options,
            Lookup = _lookup?.Build(),
        };
    }

    private EditorColumnBuilder<TLine, TProp> Kind(EditorKind kind)
    {
        if (_kindLocked)
        {
            throw new EditorConfigurationException($"Column '{_field}' is a computed column; its editor kind cannot change.");
        }

        _kind = kind;
        return this;
    }

    private EditorKind InferKind(string editorId)
    {
        var type = Nullable.GetUnderlyingType(typeof(TProp)) ?? typeof(TProp);
        if (type == typeof(string)) return EditorKind.Text;
        if (type == typeof(int) || type == typeof(long) || type == typeof(short)) return EditorKind.Integer;
        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return EditorKind.Decimal;
        if (type == typeof(DateOnly) || type == typeof(DateTime)) return EditorKind.Date;
        if (type == typeof(bool)) return EditorKind.Toggle;

        throw new EditorConfigurationException(
            $"Editor '{editorId}': column '{_field}' has type {type.Name}, which has no default editor. " +
            "Call .Select(...), .Lookup(...), .ReadOnly() or .Hidden() explicitly.");
    }

    internal static string Humanize(string field)
    {
        var sb = new StringBuilder(field.Length + 4);
        for (var i = 0; i < field.Length; i++)
        {
            var ch = field[i];
            if (i > 0 && char.IsUpper(ch) && !char.IsUpper(field[i - 1]))
            {
                sb.Append(' ').Append(char.ToLowerInvariant(ch));
            }
            else
            {
                sb.Append(i == 0 ? char.ToUpperInvariant(ch) : ch);
            }
        }

        return sb.ToString();
    }
}

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
    private decimal? _min;
    private Func<IServiceProvider, IReadOnlyList<SelectOption>>? _optionsFactory;
    private SuggestBuilder? _suggest;
    private bool _adornment;
    private bool _editable;
    private decimal? _max;
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

    /// <summary>Lowest accepted value. Reports a cell error below it; it never rewrites what the user typed.</summary>
    public EditorColumnBuilder<TLine, TProp> Min(decimal min) { _min = min; return this; }

    /// <summary>Highest accepted value. Reports a cell error above it; it never rewrites what the user typed.</summary>
    public EditorColumnBuilder<TLine, TProp> Max(decimal max) { _max = max; return this; }

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

    /// <summary>
    /// Select whose options are resolved on every render against the request's services, for lists
    /// that vary per request. The fixed overload is unaffected.
    /// </summary>
    public EditorColumnBuilder<TLine, TProp> Select(Func<IServiceProvider, IEnumerable<SelectOption>> optionsFactory)
    {
        ArgumentNullException.ThrowIfNull(optionsFactory);
        Kind(EditorKind.Select);
        _optionsFactory = sp => [.. optionsFactory(sp)];
        return this;
    }

    /// <summary>
    /// Text column with a remote type-ahead. The posted value is what the user types; picking a
    /// suggestion writes the configured label into the cell and runs the <c>onSuggestionSelected</c>
    /// hook, which decides what to write on the sibling fields. Typing something no suggestion
    /// matches is valid and is posted as typed.
    /// </summary>
    /// <summary>
    /// Adds a trailing button to the cell: its label comes from the <c>adornmentLabel</c> hook and
    /// clicking it runs <c>onAdornment</c>. Nothing about it is posted; keep per-row state in
    /// <c>row.__host</c>, a bag the editor never serializes.
    /// </summary>
    /// <summary>
    /// Lets a computed column accept typing. It still never posts: the typed text goes to the
    /// <c>onComputedInput</c> hook and the host decides what to write on the row, so the next
    /// recalculation does not fight it.
    /// </summary>
    public EditorColumnBuilder<TLine, TProp> Editable(bool editable = true)
    {
        _editable = editable;
        return this;
    }

    public EditorColumnBuilder<TLine, TProp> Adornment(bool adornment = true)
    {
        _adornment = adornment;
        return this;
    }

    public EditorColumnBuilder<TLine, TProp> Suggest(string url, Action<SuggestBuilder>? configure = null)
    {
        Kind(EditorKind.Suggest);
        var builder = new SuggestBuilder(url);
        configure?.Invoke(builder);
        _suggest = builder;
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
        if (kind == EditorKind.Select && _options.Count == 0 && _optionsFactory is null)
            throw new EditorConfigurationException($"Editor '{editorId}': Select column '{_field}' needs at least one option, or an options factory.");
        if (_editable && kind is not EditorKind.Computed)
            throw new EditorConfigurationException($"Editor '{editorId}': column '{_field}' is Editable but is not a computed column.");
        // An editable computed column does render a control, so it can carry an adornment.
        if (_adornment && (kind is EditorKind.Hidden or EditorKind.ReadOnly || (kind is EditorKind.Computed && !_editable)))
            throw new EditorConfigurationException($"Editor '{editorId}': column '{_field}' has an Adornment but renders no control.");
        if (_suggest is not null && _lookup is not null)
            throw new EditorConfigurationException($"Editor '{editorId}': column '{_field}' declares both Suggest and Lookup; pick one.");
        if ((_min is not null || _max is not null) && kind is not (EditorKind.Integer or EditorKind.Decimal))
            throw new EditorConfigurationException($"Editor '{editorId}': column '{_field}' has Min/Max but is not numeric.");
        if (_min is { } lo && _max is { } hi && lo > hi)
            throw new EditorConfigurationException($"Editor '{editorId}': column '{_field}' has Min {lo} greater than Max {hi}.");

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
            Min = _min,
            Max = _max,
            Required = _required,
            Total = _total,
            Placeholder = _placeholder,
            Options = _options,
            OptionsFactory = _optionsFactory,
            Lookup = _lookup?.Build(),
            Suggest = _suggest?.Build(editorId, _field),
            Adornment = _adornment,
            Editable = _editable,
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

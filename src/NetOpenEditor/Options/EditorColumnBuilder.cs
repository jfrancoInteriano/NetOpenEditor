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
    private bool _readOnly;
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

    /// <summary>Column heading. Defaults to the property name split into words.</summary>
    public EditorColumnBuilder<TLine, TProp> Header(string header) { _header = header; return this; }

    /// <summary>Placeholder of the cell's control; on a Select it is the text of the empty option.</summary>
    public EditorColumnBuilder<TLine, TProp> Placeholder(string placeholder) { _placeholder = placeholder; return this; }

    /// <summary>Column width as CSS (for example 8rem or 15%). Without it the table distributes the space.</summary>
    public EditorColumnBuilder<TLine, TProp> Width(string css) { _width = css; return this; }

    /// <summary>Cell alignment. Numeric and computed columns are right-aligned unless told otherwise.</summary>
    public EditorColumnBuilder<TLine, TProp> Align(CellAlign align) { _align = align; return this; }

    /// <summary>
    /// The cell cannot be left empty: saving reports it and <c>validate()</c> returns false. A
    /// Toggle cannot be required, since an unchecked box is a valid answer.
    /// </summary>
    public EditorColumnBuilder<TLine, TProp> Required() { _required = true; return this; }

    /// <summary>
    /// Adds the column up in the footer and in the totals carried by the change events. Numeric or
    /// computed columns only, including a numeric column declared <see cref="ReadOnly"/>.
    /// </summary>
    public EditorColumnBuilder<TLine, TProp> Total() { _total = true; return this; }

    /// <summary>Number of decimals shown, and kept while typing. Between 0 and 6.</summary>
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

    /// <summary>Plain text cell. The default for a string property.</summary>
    public EditorColumnBuilder<TLine, TProp> Text() => Kind(EditorKind.Text);

    /// <summary>Whole-number cell: typing drops anything after the separator. The default for int, long and short.</summary>
    public EditorColumnBuilder<TLine, TProp> Integer() { if (!_kindLocked) Kind(EditorKind.Integer); _decimals = 0; return this; }

    /// <summary>Numeric cell with a fixed number of decimals. The default for decimal, double and float.</summary>
    public EditorColumnBuilder<TLine, TProp> Decimal(int decimals = 2) { if (!_kindLocked) Kind(EditorKind.Decimal); return Decimals(decimals); }

    /// <summary>Date cell, posted as yyyy-MM-dd. The default for DateOnly and DateTime.</summary>
    public EditorColumnBuilder<TLine, TProp> Date() => Kind(EditorKind.Date);

    /// <summary>Checkbox cell. The default for bool; it posts either way, checked or not.</summary>
    public EditorColumnBuilder<TLine, TProp> Toggle() => Kind(EditorKind.Toggle);
    /// <summary>
    /// Shows the value without accepting typing. Read-only is a state of editing, not a change of
    /// type: a numeric column stays numeric, keeps its decimals and can still carry
    /// <see cref="Total"/>. The value is posted by a hidden input either way.
    /// </summary>
    public EditorColumnBuilder<TLine, TProp> ReadOnly(bool readOnly = true) { _readOnly = readOnly; return this; }
    /// <summary>
    /// No cell at all: the value travels in a hidden input and is posted like any other column.
    /// A seeded value keeps the declared type of its property on the client.
    /// </summary>
    public EditorColumnBuilder<TLine, TProp> Hidden() => Kind(EditorKind.Hidden);

    /// <summary>Dropdown with a fixed list of options, resolved once at registration.</summary>
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
    /// Lets a computed column accept typing. It still never posts: the typed text goes to the
    /// <c>onComputedInput</c> hook and the host decides what to write on the row, so the next
    /// recalculation does not fight it.
    /// </summary>
    public EditorColumnBuilder<TLine, TProp> Editable(bool editable = true)
    {
        _editable = editable;
        return this;
    }

    /// <summary>
    /// Adds a button beside the cell's control, on the same line: its label comes from the
    /// <c>adornmentLabel</c> hook and clicking it runs <c>onAdornment</c>. Nothing about it is
    /// posted; keep per-row state in <c>row.__host</c>, a bag the editor never serializes. Only a
    /// column that renders a control can carry one.
    /// </summary>
    public EditorColumnBuilder<TLine, TProp> Adornment(bool adornment = true)
    {
        _adornment = adornment;
        return this;
    }

    /// <summary>
    /// Text column with a remote type-ahead. The posted value is what the user types; picking a
    /// suggestion writes the configured label into the cell and runs the <c>onSuggestionSelected</c>
    /// hook, which decides what to write on the sibling fields. Typing something no suggestion
    /// matches is valid and is posted as typed.
    /// </summary>
    public EditorColumnBuilder<TLine, TProp> Suggest(string url, Action<SuggestBuilder>? configure = null)
    {
        Kind(EditorKind.Suggest);
        var builder = new SuggestBuilder(url);
        configure?.Invoke(builder);
        _suggest = builder;
        return this;
    }

    /// <summary>
    /// Remote picker: the cell shows a label and posts the key. The search can carry values read
    /// from the page, and the endpoint itself can be replaced per search by the <c>lookupUrl</c> hook.
    /// </summary>
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
        // Read-only keeps a numeric column numeric — that is what lets it keep Total(), its format
        // and its JSON type. Every other kind collapses to the plain read-only cell, as before.
        if (_readOnly && kind is not (EditorKind.Integer or EditorKind.Decimal)) kind = EditorKind.ReadOnly;

        if (kind == EditorKind.Toggle && _required)
            throw new EditorConfigurationException($"Editor '{editorId}': column '{_field}' is a Toggle and cannot be Required.");
        if (_total && kind is not (EditorKind.Integer or EditorKind.Decimal or EditorKind.Computed))
            throw new EditorConfigurationException($"Editor '{editorId}': column '{_field}' has Total() but is not numeric.");
        if (kind == EditorKind.Select && _options.Count == 0 && _optionsFactory is null)
            throw new EditorConfigurationException($"Editor '{editorId}': Select column '{_field}' needs at least one option, or an options factory.");
        if (_editable && kind is not EditorKind.Computed)
            throw new EditorConfigurationException($"Editor '{editorId}': column '{_field}' is Editable but is not a computed column.");
        // An editable computed column does render a control, so it can carry an adornment.
        if (_adornment && (_readOnly || kind is EditorKind.Hidden or EditorKind.ReadOnly || (kind is EditorKind.Computed && !_editable)))
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
            ReadOnly = _readOnly || kind is EditorKind.ReadOnly,
            ValueType = typeof(TProp),
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

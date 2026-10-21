namespace NetOpenEditor.Columns;

public sealed class EditorColumn<TLine>
{
    /// <summary>C# property name. Used verbatim in JSON, in name="" and in ModelState keys.</summary>
    public required string Field { get; init; }
    /// <summary>Column heading as rendered in the header row.</summary>
    public required string Header { get; init; }
    public required EditorKind Kind { get; init; }
    /// <summary>Compiled once at registration; never reflection per request.</summary>
    public required Func<TLine, object?> Getter { get; init; }
    /// <summary>Lookup only: initial display label for a server-provided value.</summary>
    public Func<TLine, string?>? Label { get; init; }
    /// <summary>Cell alignment; numeric and computed columns default to the end.</summary>
    public CellAlign Align { get; init; }

    /// <summary>Column width as CSS, or null to let the table distribute the space.</summary>
    public string? WidthCss { get; init; }

    /// <summary>Decimals shown and kept while typing. Always 0 for an integer column.</summary>
    public int Decimals { get; init; } = 2;

    /// <summary>The cell cannot be left empty: saving reports it and validate() returns false.</summary>
    public bool Required { get; init; }

    /// <summary>
    /// The column is added up in the footer and in the totals carried by the change events.
    /// Numeric or computed columns only, read-only ones included.
    /// </summary>
    public bool Total { get; init; }

    /// <summary>Placeholder of the control; on a Select, the text of the empty option.</summary>
    public string? Placeholder { get; init; }

    /// <summary>Fixed Select options. Empty when <see cref="OptionsFactory"/> resolves them per render.</summary>
    public IReadOnlyList<SelectOption> Options { get; init; } = [];

    /// <summary>Remote picker settings, or null when the column is not a lookup.</summary>
    public LookupSettings? Lookup { get; init; }

    /// <summary>
    /// Renders a trailing button in the cell whose label comes from the <c>adornmentLabel</c> hook
    /// and whose click runs <c>onAdornment</c>. It posts nothing: per-row state belongs in row.__host.
    /// </summary>
    public bool Adornment { get; init; }

    /// <summary>
    /// A computed column that accepts typing. It still never posts and <c>compute</c> keeps filling
    /// it; what the user types goes to the <c>onComputedInput</c> hook, which decides what to write.
    /// </summary>
    public bool Editable { get; init; }

    /// <summary>Remote type-ahead over a text column. The posted value is the text, not a key.</summary>
    public SuggestSettings? Suggest { get; init; }

    /// <summary>
    /// Resolves the Select options once per render against the request's services, for lists that
    /// depend on per-request data. Null means the fixed <see cref="Options"/> list is used.
    /// </summary>
    public Func<IServiceProvider, IReadOnlyList<SelectOption>>? OptionsFactory { get; init; }

    /// <summary>Lowest accepted value; null means no floor. Numeric columns only.</summary>
    public decimal? Min { get; init; }

    /// <summary>Highest accepted value; null means no ceiling. Numeric columns only.</summary>
    public decimal? Max { get; init; }

    /// <summary>
    /// The cell shows its value but takes no typing. It is a state of editing, not a change of
    /// type: a numeric column stays numeric, keeps its format and can still carry a total. The
    /// value is posted by a hidden input, so the form sees the same field either way.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// Declared CLR type of the mapped property. Seeded rows are written to JSON with this type, so
    /// a hidden or read-only number reaches the client as a number and not as text.
    /// </summary>
    public Type? ValueType { get; init; }

    /// <summary>True when the column reaches the form: everything except a computed column.</summary>
    public bool Posts => Kind is not EditorKind.Computed;
}

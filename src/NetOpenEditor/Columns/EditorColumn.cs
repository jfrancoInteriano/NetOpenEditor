namespace NetOpenEditor.Columns;

public sealed class EditorColumn<TLine>
{
    /// <summary>C# property name. Used verbatim in JSON, in name="" and in ModelState keys.</summary>
    public required string Field { get; init; }
    public required string Header { get; init; }
    public required EditorKind Kind { get; init; }
    /// <summary>Compiled once at registration; never reflection per request.</summary>
    public required Func<TLine, object?> Getter { get; init; }
    /// <summary>Lookup only: initial display label for a server-provided value.</summary>
    public Func<TLine, string?>? Label { get; init; }
    public CellAlign Align { get; init; }
    public string? WidthCss { get; init; }
    public int Decimals { get; init; } = 2;
    public bool Required { get; init; }
    public bool Total { get; init; }
    public string? Placeholder { get; init; }
    public IReadOnlyList<SelectOption> Options { get; init; } = [];
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

    public bool Posts => Kind is not EditorKind.Computed;
}

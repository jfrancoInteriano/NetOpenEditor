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

    public bool Posts => Kind is not EditorKind.Computed;
}

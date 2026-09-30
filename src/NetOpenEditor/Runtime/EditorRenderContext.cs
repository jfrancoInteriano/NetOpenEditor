namespace NetOpenEditor.Runtime;

/// <summary>Per-render inputs supplied by the tag helper (or a direct caller).</summary>
public sealed class EditorRenderContext
{
    /// <summary>Form name prefix: inputs post as "{NamePrefix}[i].{Field}".</summary>
    public string NamePrefix { get; init; } = "Lines";

    /// <summary>ModelState errors keyed as "{NamePrefix}[i].{Field}" or "{NamePrefix}[i]".</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    /// <summary>Fields rendered as hidden inputs (no cell) in this render only.</summary>
    public IReadOnlySet<string> HiddenColumns { get; init; } = new HashSet<string>();
}

namespace NetOpenEditor.Runtime;

/// <summary>Per-render inputs supplied by the tag helper (or a direct caller).</summary>
public sealed class EditorRenderContext
{
    /// <summary>Form name prefix: inputs post as "{NamePrefix}[i].{Field}".</summary>
    public string NamePrefix { get; init; } = "Lines";

    /// <summary>ModelState errors keyed as "{NamePrefix}[i].{Field}" or "{NamePrefix}[i]".</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; init; } = new Dictionary<string, string[]>();

    /// <summary>
    /// The request's services, used by columns whose Select options are resolved per render.
    /// The tag helper fills it in; a render without them only fails if such a column exists.
    /// </summary>
    public IServiceProvider? Services { get; init; }

    /// <summary>Fields rendered as hidden inputs (no cell) in this render only.</summary>
    public IReadOnlySet<string> HiddenColumns { get; init; } = new HashSet<string>();
}

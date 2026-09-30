namespace NetOpenEditor.Columns;

/// <summary>Remote lookup configuration: GET {Url}?{TermParameter}=text → JSON array of objects.</summary>
public sealed class LookupSettings
{
    public required string Url { get; init; }
    public string TermParameter { get; init; } = "term";
    public string ValueField { get; init; } = "id";
    public string LabelField { get; init; } = "text";
    public IReadOnlyList<string> DisplayFields { get; init; } = [];
    public int MinLength { get; init; }
    public int DebounceMs { get; init; } = 220;

    /// <summary>Posted property name → JSON field copied from the picked item.</summary>
    public IReadOnlyDictionary<string, string> Companions { get; init; } = new Dictionary<string, string>();
}

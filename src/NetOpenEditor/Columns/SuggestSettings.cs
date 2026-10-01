namespace NetOpenEditor.Columns;

/// <summary>
/// Remote type-ahead for a text column: GET {Url}?{TermParameter}=text → JSON array of objects.
///
/// <para>Unlike <see cref="LookupSettings"/>, the posted value is the text the user types. Picking a
/// suggestion writes <see cref="LabelField"/> into the cell and hands the item to the
/// <c>onSuggestionSelected</c> hook, which decides what to write on the sibling fields; typing
/// something no suggestion matches is a valid answer and is posted as typed.</para>
/// </summary>
public sealed class SuggestSettings
{
    public required string Url { get; init; }

    public string TermParameter { get; init; } = "term";

    /// <summary>JSON field written into the cell when a suggestion is picked. There is no default: endpoints disagree on the name.</summary>
    public required string LabelField { get; init; }

    /// <summary>JSON fields shown in the dropdown, in order. Falls back to <see cref="LabelField"/> when empty.</summary>
    public IReadOnlyList<string> DisplayFields { get; init; } = [];

    public int MinLength { get; init; }

    public int DebounceMs { get; init; } = 220;

    /// <summary>Query parameters resolved in the browser on every search: name → CSS selector of the element whose value is sent.</summary>
    public IReadOnlyDictionary<string, string> Params { get; init; } = new Dictionary<string, string>();
}

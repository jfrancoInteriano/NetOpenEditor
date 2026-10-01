using NetOpenEditor.Columns;

namespace NetOpenEditor.Options;

/// <summary>Configures a text column's remote type-ahead. The posted value stays the typed text.</summary>
public sealed class SuggestBuilder
{
    private readonly string _url;
    private string _term = "term";
    private string? _label;
    private readonly List<string> _display = [];
    private int _minLength;
    private int _debounce = 220;
    private readonly Dictionary<string, string> _params = new(StringComparer.Ordinal);

    internal SuggestBuilder(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new EditorConfigurationException("Suggest url is required.");
        }

        _url = url;
    }

    /// <summary>JSON field written into the cell when a suggestion is picked. Required.</summary>
    public SuggestBuilder LabelField(string field) { _label = Require(field, nameof(field)); return this; }

    public SuggestBuilder TermParameter(string name) { _term = Require(name, nameof(name)); return this; }

    /// <summary>JSON fields shown in the dropdown, in order.</summary>
    public SuggestBuilder Display(params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _display.Clear();
        foreach (var field in fields) _display.Add(Require(field, nameof(fields)));
        return this;
    }

    /// <summary>Characters needed before searching. 0 searches on the first keystroke.</summary>
    public SuggestBuilder MinLength(int length)
    {
        if (length < 0) throw new EditorConfigurationException("Suggest MinLength cannot be negative.");
        _minLength = length;
        return this;
    }

    public SuggestBuilder Debounce(int milliseconds)
    {
        if (milliseconds < 0) throw new EditorConfigurationException("Suggest Debounce cannot be negative.");
        _debounce = milliseconds;
        return this;
    }

    /// <summary>Query parameter resolved from the page on every search: <paramref name="selector"/> is a CSS selector.</summary>
    public SuggestBuilder Param(string name, string selector)
    {
        var key = Require(name, nameof(name));
        if (!_params.TryAdd(key, Require(selector, nameof(selector))))
        {
            throw new EditorConfigurationException($"Suggest parameter '{key}' is declared twice.");
        }

        return this;
    }

    internal SuggestSettings Build(string editorId, string field)
    {
        if (_label is null)
        {
            throw new EditorConfigurationException(
                $"Editor '{editorId}': Suggest column '{field}' needs LabelField: the field written into the cell when a suggestion is picked.");
        }

        if (_params.ContainsKey(_term))
        {
            throw new EditorConfigurationException(
                $"Editor '{editorId}': Suggest column '{field}' has a parameter '{_term}' that collides with the term parameter.");
        }

        return new SuggestSettings
        {
            Url = _url,
            TermParameter = _term,
            LabelField = _label,
            DisplayFields = _display.ToArray(),
            MinLength = _minLength,
            DebounceMs = _debounce,
            Params = new Dictionary<string, string>(_params, StringComparer.Ordinal),
        };
    }

    private static string Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new EditorConfigurationException($"Suggest '{name}' cannot be empty.");
        }

        return value.Trim();
    }
}

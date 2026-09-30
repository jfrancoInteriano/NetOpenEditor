using System.Linq.Expressions;
using NetOpenEditor.Columns;

namespace NetOpenEditor.Options;

public sealed class LookupBuilder<TLine>
{
    private readonly string _url;
    private string _term = "term";
    private string _value = "id";
    private string _label = "text";
    private readonly List<string> _display = [];
    private int _minLength;
    private int _debounce = 220;
    private Func<TLine, string?>? _labelSelector;
    private readonly Dictionary<string, string> _companions = new(StringComparer.Ordinal);
    private readonly List<(string Field, Func<TLine, object?> Getter)> _companionColumns = [];

    internal LookupBuilder(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new EditorConfigurationException("Lookup url is required.");
        }

        _url = url;
    }

    public LookupBuilder<TLine> TermParameter(string name) { _term = Require(name, nameof(name)); return this; }
    public LookupBuilder<TLine> ValueField(string field) { _value = Require(field, nameof(field)); return this; }
    public LookupBuilder<TLine> LabelField(string field) { _label = Require(field, nameof(field)); return this; }

    public LookupBuilder<TLine> Display(params string[] fields)
    {
        _display.Clear();
        _display.AddRange(fields.Select(f => Require(f, nameof(fields))));
        return this;
    }

    public LookupBuilder<TLine> MinLength(int length)
    {
        if (length < 0) throw new EditorConfigurationException("Lookup MinLength cannot be negative.");
        _minLength = length;
        return this;
    }

    public LookupBuilder<TLine> Debounce(int milliseconds)
    {
        if (milliseconds < 0) throw new EditorConfigurationException("Lookup Debounce cannot be negative.");
        _debounce = milliseconds;
        return this;
    }

    /// <summary>Server-side label for a value that already exists on the row (e.g. "1101 - Caja").</summary>
    public LookupBuilder<TLine> Label(Func<TLine, string?> selector)
    {
        _labelSelector = selector ?? throw new ArgumentNullException(nameof(selector));
        return this;
    }

    /// <summary>Copies <paramref name="jsonField"/> from the picked item into the row property and posts it as a hidden input.</summary>
    public LookupBuilder<TLine> Companion<TProp>(Expression<Func<TLine, TProp>> target, string jsonField)
    {
        var (field, getter) = MemberSelector.Compile(target);
        _companions[field] = Require(jsonField, nameof(jsonField));
        _companionColumns.Add((field, getter));
        return this;
    }

    internal Func<TLine, string?>? LabelSelector => _labelSelector;
    internal IReadOnlyList<(string Field, Func<TLine, object?> Getter)> Companions => _companionColumns;

    internal LookupSettings Build() => new()
    {
        Url = _url,
        TermParameter = _term,
        ValueField = _value,
        LabelField = _label,
        DisplayFields = _display.ToArray(),
        MinLength = _minLength,
        DebounceMs = _debounce,
        Companions = new Dictionary<string, string>(_companions, StringComparer.Ordinal),
    };

    private static string Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new EditorConfigurationException($"Lookup '{name}' cannot be empty.");
        }

        return value;
    }
}

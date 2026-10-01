namespace NetOpenEditor.Options;

/// <summary>Client UI strings. English defaults, Spanish preset via <see cref="UseCulture"/>, per-key overrides via <see cref="Set"/>.</summary>
public sealed class NetOpenEditorLocalizationOptions
{
    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["remove"] = "Remove line",
        ["lookup.searching"] = "Searching...",
        ["lookup.empty"] = "No results",
        ["totals"] = "Totals",
        ["rows.one"] = "1 line",
        ["rows.many"] = "{n} lines",
        ["required"] = "Required",
        ["minRows"] = "At least {n} line(s) required",
        ["rows.empty"] = "No lines",
        ["min"] = "Minimum {n}",
        ["max"] = "Maximum {n}",
        ["paste.truncated"] = "Only the first {n} rows were pasted.",
        ["lookup.notFound"] = "'{term}' was not found",
        ["lookup.ambiguous"] = "'{term}' matches more than one item",
    };

    private static readonly Dictionary<string, string> Spanish = new(StringComparer.Ordinal)
    {
        ["remove"] = "Eliminar línea",
        ["lookup.searching"] = "Buscando...",
        ["lookup.empty"] = "Sin resultados",
        ["totals"] = "Totales",
        ["rows.one"] = "1 línea",
        ["rows.many"] = "{n} líneas",
        ["required"] = "Requerido",
        ["minRows"] = "Se requiere al menos {n} línea(s)",
        ["rows.empty"] = "Sin líneas",
        ["min"] = "Mínimo {n}",
        ["max"] = "Máximo {n}",
        ["paste.truncated"] = "Solo se pegaron las primeras {n} filas.",
        ["lookup.notFound"] = "No se encontró '{term}'",
        ["lookup.ambiguous"] = "'{term}' coincide con varios",
    };

    private readonly Dictionary<string, string> _overrides = new(StringComparer.Ordinal);
    private Dictionary<string, string> _preset = English;

    public NetOpenEditorLocalizationOptions UseCulture(string culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        _preset = culture.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? Spanish : English;
        return this;
    }

    public NetOpenEditorLocalizationOptions Set(string key, string value)
    {
        if (key is null || !English.ContainsKey(key))
        {
            throw new ArgumentException($"Unknown localization key '{key}'. Known keys: {string.Join(", ", English.Keys)}.", nameof(key));
        }

        _overrides[key] = value ?? throw new ArgumentNullException(nameof(value));
        return this;
    }

    public IReadOnlyDictionary<string, string> Effective
    {
        get
        {
            var effective = new Dictionary<string, string>(_preset, StringComparer.Ordinal);
            foreach (var (key, value) in _overrides)
            {
                effective[key] = value;
            }

            return effective;
        }
    }
}

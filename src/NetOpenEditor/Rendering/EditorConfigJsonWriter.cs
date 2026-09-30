using System.Text.Json;
using NetOpenEditor.Columns;
using NetOpenEditor.Options;
using NetOpenEditor.Runtime;

namespace NetOpenEditor.Rendering;

public static class EditorConfigJsonWriter
{
    public static void Write<TLine>(
        Utf8JsonWriter writer,
        EditorOptions<TLine> options,
        EditorRenderContext context,
        IReadOnlyDictionary<string, string> locale)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(locale);

        writer.WriteStartObject();
        writer.WriteString("id", options.Id);
        writer.WriteString("prefix", context.NamePrefix);
        writer.WriteNumber("minRows", options.MinRows);

        writer.WritePropertyName("locale");
        writer.WriteStartObject();
        foreach (var (key, value) in locale) writer.WriteString(key, value);
        writer.WriteEndObject();

        writer.WritePropertyName("columns");
        writer.WriteStartArray();
        foreach (var column in options.Columns)
        {
            var kind = EffectiveKind(column, context);
            writer.WriteStartObject();
            writer.WriteString("field", column.Field);
            writer.WriteString("kind", KindName(kind));
            writer.WriteString("header", column.Header);
            writer.WriteBoolean("required", column.Required);
            writer.WriteNumber("decimals", column.Decimals);
            writer.WriteBoolean("total", column.Total);
            writer.WriteString("align", column.Align switch { CellAlign.End => "end", CellAlign.Center => "center", _ => "start" });
            if (column.WidthCss is null) writer.WriteNull("width"); else writer.WriteString("width", column.WidthCss);
            if (column.Placeholder is null) writer.WriteNull("placeholder"); else writer.WriteString("placeholder", column.Placeholder);

            if (column.Lookup is { } lookup)
            {
                writer.WritePropertyName("lookup");
                writer.WriteStartObject();
                writer.WriteString("url", lookup.Url);
                writer.WriteString("term", lookup.TermParameter);
                writer.WriteString("value", lookup.ValueField);
                writer.WriteString("label", lookup.LabelField);
                writer.WritePropertyName("display");
                writer.WriteStartArray();
                foreach (var field in lookup.DisplayFields) writer.WriteStringValue(field);
                writer.WriteEndArray();
                writer.WriteNumber("minLength", lookup.MinLength);
                writer.WriteNumber("debounce", lookup.DebounceMs);
                writer.WritePropertyName("companions");
                writer.WriteStartObject();
                foreach (var (posted, json) in lookup.Companions) writer.WriteString(posted, json);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    public static void WriteErrors(Utf8JsonWriter writer, IReadOnlyDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(errors);

        writer.WriteStartObject();
        foreach (var (key, messages) in errors)
        {
            writer.WritePropertyName(key);
            writer.WriteStartArray();
            foreach (var message in messages) writer.WriteStringValue(message);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    /// <summary>A column listed in <see cref="EditorRenderContext.HiddenColumns"/> renders as Hidden; computed columns cannot be hidden that way.</summary>
    public static EditorKind EffectiveKind<TLine>(EditorColumn<TLine> column, EditorRenderContext context) =>
        context.HiddenColumns.Contains(column.Field) && column.Kind != EditorKind.Computed ? EditorKind.Hidden : column.Kind;

    public static string KindName(EditorKind kind) => kind switch
    {
        EditorKind.Text => "text",
        EditorKind.Integer => "integer",
        EditorKind.Decimal => "decimal",
        EditorKind.Date => "date",
        EditorKind.Select => "select",
        EditorKind.Lookup => "lookup",
        EditorKind.Toggle => "toggle",
        EditorKind.ReadOnly => "readonly",
        EditorKind.Computed => "computed",
        EditorKind.Hidden => "hidden",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

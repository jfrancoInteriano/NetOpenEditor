using System.Globalization;
using System.Text.Json;
using NetOpenEditor.Columns;

namespace NetOpenEditor.Rendering;

/// <summary>Serializes rows using only the declared columns (data minimization) plus lookup labels under "__labels".</summary>
public static class EditorRowsJsonWriter
{
    public static void Write<TLine>(Utf8JsonWriter writer, IEnumerable<TLine> rows, IReadOnlyList<EditorColumn<TLine>> columns)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);

        var labeled = columns.Where(c => c.Kind == EditorKind.Lookup && c.Label is not null).ToArray();

        writer.WriteStartArray();
        foreach (var line in rows)
        {
            writer.WriteStartObject();
            foreach (var column in columns)
            {
                if (column.Kind == EditorKind.Computed) continue;
                writer.WritePropertyName(column.Field);
                WriteValue(writer, column, column.Getter(line));
            }

            if (labeled.Length > 0)
            {
                writer.WritePropertyName("__labels");
                writer.WriteStartObject();
                foreach (var column in labeled)
                {
                    var label = column.Label!(line);
                    if (label is null) writer.WriteNull(column.Field);
                    else writer.WriteString(column.Field, label);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    internal static void WriteValue<TLine>(Utf8JsonWriter writer, EditorColumn<TLine> column, object? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        switch (column.Kind)
        {
            case EditorKind.Integer:
            case EditorKind.Decimal:
                WriteNumber(writer, value);
                break;
            case EditorKind.Toggle:
                writer.WriteBooleanValue(value is true);
                break;
            case EditorKind.Date:
                writer.WriteStringValue(value switch
                {
                    DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
                });
                break;
            default:
                // Text, Select, Lookup, ReadOnly, Hidden: strings (Guid, enum, int → invariant string)
                writer.WriteStringValue(value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                break;
        }
    }

    private static void WriteNumber(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case decimal d: writer.WriteNumberValue(d); break;
            case int i: writer.WriteNumberValue(i); break;
            case long l: writer.WriteNumberValue(l); break;
            case short s: writer.WriteNumberValue(s); break;
            case double db: writer.WriteNumberValue(db); break;
            case float f: writer.WriteNumberValue(f); break;
            default: writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture)); break;
        }
    }
}

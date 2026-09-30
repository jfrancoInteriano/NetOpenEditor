using System.Text.Unicode;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using NetOpenEditor.Columns;
using NetOpenEditor.Options;
using NetOpenEditor.Runtime;

namespace NetOpenEditor.Rendering;

/// <summary>
/// Emits the editor shell: three JSON scripts (config, rows, errors) and a table whose single row
/// template is an Alpine <c>x-for</c>. Field names are C# identifiers, so they are safe inside the
/// Alpine expressions; every human-readable string goes through <see cref="HtmlEncoder"/>.
/// </summary>
public sealed class EditorHtmlRenderer<TLine>
{
    private const string TrashSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 20 20\" fill=\"currentColor\" width=\"14\" height=\"14\" aria-hidden=\"true\">" +
        "<path fill-rule=\"evenodd\" d=\"M8.75 1A2.75 2.75 0 0 0 6 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 1 0 .23 1.482l.149-.022.841 10.518A2.75 2.75 0 0 0 7.596 19h4.807a2.75 2.75 0 0 0 2.742-2.53l.841-10.52.149.023a.75.75 0 0 0 .23-1.482A41.03 41.03 0 0 0 14 4.193V3.75A2.75 2.75 0 0 0 11.25 1h-2.5ZM10 4c.84 0 1.673.025 2.5.075V3.75c0-.69-.56-1.25-1.25-1.25h-2.5c-.69 0-1.25.56-1.25 1.25v.325C8.327 4.025 9.16 4 10 4ZM8.58 7.72a.75.75 0 0 0-1.5.06l.3 7.5a.75.75 0 1 0 1.5-.06l-.3-7.5Zm4.34.06a.75.75 0 1 0-1.5-.06l-.3 7.5a.75.75 0 1 0 1.5.06l.3-7.5Z\" clip-rule=\"evenodd\"/></svg>";

    private const string LookupPanel =
        """<div class="noe-lookup-panel" x-show="lookup.open" x-cloak :style="lookup.style" @mousedown.prevent>""" +
        """<div class="noe-lookup-status" x-show="lookup.loading" x-text="t('lookup.searching')"></div>""" +
        """<div class="noe-lookup-status" x-show="!lookup.loading && lookup.items.length === 0" x-text="t('lookup.empty')"></div>""" +
        """<template x-for="(item, k) in lookup.items" :key="k"><div class="noe-lookup-item" :class="{ 'is-active': k === lookup.active }" @mousedown.prevent="lookupPick(k)" @mousemove="lookup.active = k">""" +
        """<template x-for="(part, p) in lookupDisplay(item)" :key="p"><span :class="p === 0 ? 'noe-lookup-primary' : 'noe-lookup-secondary'" x-text="part"></span></template>""" +
        """</div></template></div>""";

    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    private readonly EditorOptions<TLine> _options;
    private readonly IReadOnlyDictionary<string, string> _locale;

    public EditorHtmlRenderer(EditorOptions<TLine> options, IReadOnlyDictionary<string, string> locale)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _locale = locale ?? throw new ArgumentNullException(nameof(locale));
    }

    public string Render(IEnumerable<TLine> rows, EditorRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(context);

        var visible = new List<EditorColumn<TLine>>();
        var hidden = new List<EditorColumn<TLine>>();
        foreach (var column in _options.Columns)
        {
            if (EditorConfigJsonWriter.EffectiveKind(column, context) == EditorKind.Hidden) hidden.Add(column);
            else visible.Add(column);
        }

        var id = Html.Encode(_options.Id);
        var sb = new StringBuilder(8 * 1024);

        sb.Append($$"""<div class="noe" id="noe-{{id}}" data-noe-id="{{id}}" x-data="netopenEditor('{{id}}')" x-cloak>""");
        AppendJsonScript(sb, "data-noe-config", w => EditorConfigJsonWriter.Write(w, _options, context, _locale));
        AppendJsonScript(sb, "data-noe-rows", w => EditorRowsJsonWriter.Write(w, rows, _options.Columns));
        AppendJsonScript(sb, "data-noe-errors", w => EditorConfigJsonWriter.WriteErrors(w, context.Errors));
        sb.Append("""<div class="noe-form-error" role="alert" x-show="formError" x-text="formError"></div>""");

        var colIndex = 2;
        sb.Append("""<table class="noe-table" role="grid" :aria-rowcount="count() + 1"><thead><tr role="row" aria-rowindex="1"><th class="noe-th noe-th-index" role="columnheader" scope="col" aria-colindex="1">#</th>""");
        foreach (var column in visible)
        {
            sb.Append("<th class=\"noe-th").Append(AlignClass(column.Align)).Append('"')
              .Append(" role=\"columnheader\" scope=\"col\" aria-colindex=\"").Append(colIndex++).Append('"');
            if (column.WidthCss is not null) sb.Append(" style=\"width:").Append(Html.Encode(column.WidthCss)).Append('"');
            sb.Append('>').Append(Html.Encode(column.Header));
            if (column.Required) sb.Append("""<span class="noe-required">*</span>""");
            sb.Append("</th>");
        }

        sb.Append("""<th class="noe-th noe-th-actions" role="columnheader" scope="col"></th></tr></thead><tbody>""");
        sb.Append("""<template x-for="(row, i) in rows" :key="row.__key"><tr role="row" :aria-rowindex="i + 2" :data-noe-row="i" :class="{ 'noe-row-phantom': row.__phantom, 'noe-row-locked': locked(row), 'noe-row-invalid': hasErrors(row) }">""");
        sb.Append("""<td class="noe-td noe-td-index" role="gridcell" aria-colindex="1"><span x-text="row.__phantom ? '' : (i + 1)"></span></td>""");
        for (var k = 0; k < visible.Count; k++) AppendCell(sb, visible[k], k + 2, id);

        sb.Append("""<td class="noe-td noe-td-actions" role="gridcell"><span class="noe-row-error" x-show="rowError(row)" :title="rowError(row)">!</span>""");
        sb.Append("""<button type="button" class="noe-btn-remove" x-show="canRemove(row)" @click="removeRow(i)" :title="t('remove')" :aria-label="t('remove')">""").Append(TrashSvg).Append("</button>");
        foreach (var column in hidden)
        {
            sb.Append($$"""<input type="hidden" :name="nameFor(i, '{{column.Field}}')" :value="row['{{column.Field}}'] ?? ''">""");
        }

        sb.Append("</td></tr></template></tbody>");
        AppendFooter(sb, visible);
        sb.Append("</table>");
        sb.Append(LookupPanel);
        sb.Append("</div>");
        return sb.ToString();
    }

    private static void AppendCell(StringBuilder sb, EditorColumn<TLine> column, int columnIndex, string editorId)
    {
        var f = column.Field;
        var d = column.Decimals;
        var placeholder = column.Placeholder is null ? string.Empty : $" placeholder=\"{Html.Encode(column.Placeholder)}\"";
        var required = column.Required ? " data-noe-required" : string.Empty;
        var invalidClass = $":class=\"{{ 'noe-invalid': cellError(row, '{f}') }}\"";
        var errorId = $"'{editorId}-r' + i + '-{f}-err'";
        var ariaInvalid = $":aria-invalid=\"cellError(row, '{f}') ? 'true' : 'false'\"";
        // Only point at the message while there is one: a dangling id is worse than no hint.
        var describedBy = $":aria-describedby=\"cellError(row, '{f}') ? {errorId} : null\"";
        var ariaRequired = column.Required ? " aria-required=\"true\"" : string.Empty;
        var onKey = $"@keydown=\"onKey($event, i, '{f}')\"";
        var onPaste = $"@paste=\"onPaste($event, i, '{f}')\"";
        var onFocus = $"@focus=\"onFocus($event, i, '{f}')\"";
        var name = $":name=\"nameFor(i, '{f}')\"";

        sb.Append("<td class=\"noe-td").Append(AlignClass(column.Align))
          .Append("\" role=\"gridcell\" aria-colindex=\"").Append(columnIndex).Append("\">");

        switch (column.Kind)
        {
            case EditorKind.Text:
                sb.Append($"<input type=\"text\" class=\"noe-input\" autocomplete=\"off\" data-noe-field=\"{f}\"{required} {name} x-model=\"row['{f}']\" :readonly=\"locked(row)\" {invalidClass} {ariaInvalid} {describedBy}{ariaRequired} {onFocus} @input=\"onInput(i, '{f}')\" {onKey} {onPaste}{placeholder}>");
                break;

            case EditorKind.Integer:
            case EditorKind.Decimal:
                var mode = d == 0 ? "numeric" : "decimal";
                sb.Append($"<input type=\"text\" inputmode=\"{mode}\" class=\"noe-input noe-num\" autocomplete=\"off\" data-noe-num data-noe-decimals=\"{d}\" data-noe-field=\"{f}\"{required} {name} :readonly=\"locked(row)\" {invalidClass} {ariaInvalid} {describedBy}{ariaRequired} x-effect=\"syncNumber($el, row, '{f}', {d})\" {onFocus} @input=\"onNumberInput($event, i, '{f}', {d})\" @blur=\"onNumberBlur($event, i, '{f}', {d})\" {onKey} {onPaste}{placeholder}>");
                break;

            case EditorKind.Date:
                sb.Append($"<input type=\"date\" class=\"noe-input\" data-noe-field=\"{f}\"{required} {name} x-model=\"row['{f}']\" :readonly=\"locked(row)\" {invalidClass} {ariaInvalid} {describedBy}{ariaRequired} {onFocus} @change=\"onChange(i, '{f}')\" {onKey} {onPaste}>");
                break;

            case EditorKind.Select:
                var emptyOption = column.Placeholder is null ? string.Empty : Html.Encode(column.Placeholder);
                sb.Append($"<select class=\"noe-input\" data-noe-field=\"{f}\"{required} {name} x-model=\"row['{f}']\" {invalidClass} {ariaInvalid} {describedBy}{ariaRequired} :style=\"locked(row) ? 'pointer-events:none' : ''\" {onFocus} @change=\"onChange(i, '{f}')\" {onKey}><option value=\"\">{emptyOption}</option>");
                foreach (var option in column.Options)
                {
                    sb.Append("<option value=\"").Append(Html.Encode(option.Value)).Append("\">").Append(Html.Encode(option.Label)).Append("</option>");
                }

                sb.Append("</select>");
                break;

            case EditorKind.Lookup:
                sb.Append($"<div class=\"noe-lookup\"><input type=\"text\" class=\"noe-input\" autocomplete=\"off\" data-noe-field=\"{f}\"{required} :value=\"lookupText(row, '{f}')\" :readonly=\"locked(row)\" {invalidClass} {ariaInvalid} {describedBy}{ariaRequired} @focus=\"onFocus($event, i, '{f}'); lookupOpen($event, row, '{f}')\" @input=\"lookupSearch($event, row, '{f}')\" {onPaste} @keydown=\"lookupKey($event, row, i, '{f}')\" @blur=\"lookupBlur(row, '{f}')\"{placeholder}>");
                sb.Append($"<input type=\"hidden\" {name} :value=\"row['{f}'] ?? ''\"></div>");
                break;

            case EditorKind.Toggle:
                sb.Append($"<input type=\"checkbox\" class=\"noe-check\" value=\"true\" data-noe-field=\"{f}\" {name} x-model=\"row['{f}']\" :style=\"locked(row) ? 'pointer-events:none' : ''\" {onFocus} @change=\"onChange(i, '{f}')\" {onKey}>");
                sb.Append($"<input type=\"hidden\" {name} value=\"false\">");
                break;

            case EditorKind.ReadOnly:
                sb.Append($"<span class=\"noe-text\" x-text=\"row['{f}'] ?? ''\"></span><input type=\"hidden\" {name} :value=\"row['{f}'] ?? ''\">");
                break;

            case EditorKind.Computed:
                sb.Append($"<span class=\"noe-text noe-num-text\" x-text=\"fmt(row['{f}'], {d})\"></span>");
                break;

            default:
                throw new InvalidOperationException($"Column '{f}' has kind {column.Kind}, which is not a visible cell.");
        }

        if (column.Kind is not (EditorKind.Computed or EditorKind.ReadOnly))
        {
            sb.Append($"<div class=\"noe-error\" :id=\"{errorId}\" x-show=\"cellError(row, '{f}')\" x-text=\"cellError(row, '{f}')\"></div>");
        }

        sb.Append("</td>");
    }

    private static void AppendFooter(StringBuilder sb, List<EditorColumn<TLine>> visible)
    {
        var firstTotal = visible.FindIndex(c => c.Total);
        if (firstTotal < 0) return;

        sb.Append("""<tfoot class="noe-tfoot"><tr role="row" aria-live="polite">""");
        sb.Append($"<td class=\"noe-td noe-tfoot-label\" colspan=\"{firstTotal + 1}\"><span x-text=\"t('totals')\"></span> <span class=\"noe-muted\" x-text=\"countLabel()\"></span></td>");
        for (var k = firstTotal; k < visible.Count; k++)
        {
            var column = visible[k];
            sb.Append("<td class=\"noe-td").Append(AlignClass(column.Align)).Append("\">");
            if (column.Total)
            {
                sb.Append($"<span class=\"noe-total\" data-noe-total=\"{column.Field}\" x-text=\"fmt(totals['{column.Field}'], {column.Decimals})\"></span>");
            }

            sb.Append("</td>");
        }

        sb.Append("""<td class="noe-td noe-td-actions"></td></tr></tfoot>""");
    }

    private static string AlignClass(CellAlign align) => align switch
    {
        CellAlign.End => " noe-align-end",
        CellAlign.Center => " noe-align-center",
        _ => string.Empty,
    };

    private static void AppendJsonScript(StringBuilder sb, string marker, Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            write(writer);
        }

        sb.Append("<script type=\"application/json\" ").Append(marker).Append('>');
        sb.Append(Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length));
        sb.Append("</script>");
    }
}

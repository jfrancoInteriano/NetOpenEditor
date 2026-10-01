using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.RegularExpressions;
using NetOpenEditor.Columns;
using NetOpenEditor.Options;
using NetOpenEditor.Rendering;
using NetOpenEditor.Runtime;

namespace NetOpenEditor.Tests;

public sealed class EditorHtmlRendererTests
{
    private static readonly Dictionary<string, string> Locale = new() { ["totals"] = "Totales" };

    private static EditorOptions<TestLine> JournalOptions() => new EditorOptionsBuilder<TestLine>("journal")
        .Column(l => l.AccountId, c => c.Header("Cuenta").Required().Width("20rem").Lookup("/accounts/lookup", lk => lk
            .ValueField("accountId").LabelField("display").Display("code", "name")
            .Label(l => l.AccountCode is null ? null : $"{l.AccountCode} - {l.AccountName}")
            .Companion(l => l.AccountCode, "code")
            .Companion(l => l.AccountName, "name")))
        .Column(l => l.Description, c => c.Header("<b>Desc</b>").Placeholder("Detalle"))
        .Column(l => l.DebitAmount, c => c.Header("Débito").Decimal(2).Total())
        .Column(l => l.CreditAmount, c => c.Header("Crédito").Decimal(2).Total())
        .Column(l => l.Quantity)
        .Column(l => l.DueDate)
        .Column(l => l.Active)
        .Column(l => l.Unit, c => c.Select([new SelectOption("UND", "Unidad"), new SelectOption("KG", "Kilo")]))
        .Computed("LineTotal", "Total", c => c.Decimal(2))
        .Build();

    private static string Render(EditorOptions<TestLine> options, EditorRenderContext? ctx = null, IEnumerable<TestLine>? rows = null) =>
        new EditorHtmlRenderer<TestLine>(options, Locale).Render(rows ?? [], ctx ?? new EditorRenderContext());

    private static JsonDocument Script(string html, string marker)
    {
        var match = Regex.Match(html, $"<script type=\"application/json\" {marker}>(.*?)</script>", RegexOptions.Singleline);
        Assert.True(match.Success, $"missing {marker}");
        return JsonDocument.Parse(match.Groups[1].Value);
    }

    [Fact]
    public void Root_HasAlpineComponentAndThreeJsonScripts()
    {
        var html = Render(JournalOptions(), rows: [new TestLine { Description = "Apertura" }]);

        Assert.Contains("""<div class="noe" id="noe-journal" data-noe-id="journal" x-data="netopenEditor('journal')" x-cloak>""", html);
        using var config = Script(html, "data-noe-config");
        using var rowsJson = Script(html, "data-noe-rows");
        using var errors = Script(html, "data-noe-errors");
        Assert.Equal("journal", config.RootElement.GetProperty("id").GetString());
        Assert.Equal("Apertura", rowsJson.RootElement[0].GetProperty("Description").GetString());
        Assert.Equal(JsonValueKind.Object, errors.RootElement.ValueKind);
        Assert.Contains("""<div class="noe-form-error" role="alert" x-show="formError" x-text="formError"></div>""", html);
    }

    [Fact]
    public void Header_HasOneThPerVisibleColumnPlusIndexAndActions()
    {
        var html = Render(JournalOptions());

        Assert.Equal(9 + 2, Regex.Matches(html, "<th ").Count);
        Assert.Contains("""<th class="noe-th noe-th-index" role="columnheader" scope="col" aria-colindex="1">#</th>""", html);
        Assert.Contains("""<th class="noe-th" role="columnheader" scope="col" aria-colindex="2" style="width:20rem">Cuenta<span class="noe-required">*</span></th>""", html);
        Assert.Matches("""<th class="noe-th noe-align-end" role="columnheader" scope="col" aria-colindex="\d+">Débito</th>""", html);
        Assert.Contains("&lt;b&gt;Desc&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>Desc</b>", html);
    }

    [Fact]
    public void RowTemplate_IsAlpineForWithIndexAndActions()
    {
        var html = Render(JournalOptions());

        Assert.Contains("""<template x-for="(row, i) in rows" :key="row.__key"><tr role="row" :aria-rowindex="i + 2" :data-noe-row="i" :class="{ 'noe-row-phantom': row.__phantom, 'noe-row-locked': locked(row), 'noe-row-invalid': hasErrors(row) }">""", html);
        Assert.Contains("""<td class="noe-td noe-td-index" role="gridcell" aria-colindex="1"><span x-text="row.__phantom ? '' : (i + 1)"></span></td>""", html);
        Assert.Contains("""<button type="button" class="noe-btn-remove" x-show="canRemove(row)" @click="removeRow(i)" :title="t('remove')" :aria-label="t('remove')">""", html);
        Assert.Contains("""<input type="hidden" :name="nameFor(i, 'AccountCode')" :value="row['AccountCode'] ?? ''">""", html);
        Assert.Contains("""<input type="hidden" :name="nameFor(i, 'AccountName')" :value="row['AccountName'] ?? ''">""", html);
    }

    [Fact]
    public void Cells_RenderEditorPerKind()
    {
        var html = Render(JournalOptions());

        // text
        Assert.Contains("""<input type="text" class="noe-input" autocomplete="off" data-noe-field="Description" :name="nameFor(i, 'Description')" x-model="row['Description']" :readonly="locked(row)" :class="{ 'noe-invalid': cellError(row, 'Description') }""", html);
        Assert.Contains("""@input="onInput(i, 'Description')" @keydown="onKey($event, i, 'Description')" @paste="onPaste($event, i, 'Description')" placeholder="Detalle">""", html);
        // decimal
        Assert.Contains("""inputmode="decimal" class="noe-input noe-num" autocomplete="off" data-noe-num data-noe-decimals="2" data-noe-field="DebitAmount""", html);
        Assert.Contains("""x-effect="syncNumber($el, row, 'DebitAmount', 2)""", html);
        // integer
        Assert.Contains("""inputmode="numeric" class="noe-input noe-num" autocomplete="off" data-noe-num data-noe-decimals="0" data-noe-field="Quantity" """.TrimEnd(), html);
        // date
        Assert.Contains("""<input type="date" class="noe-input" data-noe-field="DueDate" :name="nameFor(i, 'DueDate')" x-model="row['DueDate']" """.TrimEnd(), html);
        // select
        Assert.Contains("""<select class="noe-input" data-noe-field="Unit" :name="nameFor(i, 'Unit')" x-model="row['Unit']" """.TrimEnd(), html);
        Assert.Contains("""<option value=""></option><option value="UND">Unidad</option><option value="KG">Kilo</option></select>""", html);
        // lookup
        Assert.Contains("""<div class="noe-lookup"><input type="text" class="noe-input" autocomplete="off" data-noe-field="AccountId" data-noe-required :value="lookupText(row, 'AccountId')" """.TrimEnd(), html);
        Assert.Contains("""@focus="onFocus($event, i, 'AccountId'); lookupOpen($event, row, 'AccountId')" @input="lookupSearch($event, row, 'AccountId')" @paste="onPaste($event, i, 'AccountId')" @keydown="lookupKey($event, row, i, 'AccountId')" @blur="lookupBlur(row, 'AccountId')">""", html);
        Assert.Contains("""<input type="hidden" :name="nameFor(i, 'AccountId')" :value="row['AccountId'] ?? ''"></div>""", html);
        // toggle
        Assert.Contains("""<input type="checkbox" class="noe-check" value="true" data-noe-field="Active" :name="nameFor(i, 'Active')" x-model="row['Active']" """.TrimEnd(), html);
        Assert.Contains("""<input type="hidden" :name="nameFor(i, 'Active')" value="false">""", html);
        // computed
        Assert.Contains("""<span class="noe-text noe-num-text" x-text="fmt(row['LineTotal'], 2)"></span>""", html);
        Assert.DoesNotContain("nameFor(i, 'LineTotal')", html);
        // per-cell error
        Assert.Contains("""<div class="noe-error" :id="'journal-r' + i + '-Description-err'" x-show="cellError(row, 'Description')" x-text="cellError(row, 'Description')"></div>""", html);
        Assert.DoesNotContain("cellError(row, 'LineTotal')", html);
    }

    [Fact]
    public void ReadOnlyColumn_RendersTextAndHiddenInput()
    {
        var options = new EditorOptionsBuilder<TestLine>("ro").Column(l => l.Unit, c => c.ReadOnly()).Build();
        var html = Render(options);

        Assert.Contains("""<span class="noe-text" x-text="row['Unit'] ?? ''"></span><input type="hidden" :name="nameFor(i, 'Unit')" :value="row['Unit'] ?? ''">""", html);
    }

    [Fact]
    public void HideColumns_MovesColumnToHiddenInput()
    {
        var html = Render(JournalOptions(), new EditorRenderContext { HiddenColumns = new HashSet<string> { "Description" } });

        Assert.DoesNotContain("data-noe-field=\"Description\"", html);
        Assert.Contains("""<input type="hidden" :name="nameFor(i, 'Description')" :value="row['Description'] ?? ''">""", html);
        Assert.Equal(8 + 2, Regex.Matches(html, "<th ").Count);
    }

    [Fact]
    public void Footer_RendersOnlyWithTotalsAndSpansUpToFirstTotal()
    {
        var html = Render(JournalOptions());

        Assert.Contains("""<tfoot class="noe-tfoot"><tr role="row" aria-live="polite"><td class="noe-td noe-tfoot-label" colspan="3"><span x-text="t('totals')"></span> <span class="noe-muted" x-text="countLabel()"></span></td>""", html);
        Assert.Contains("""<span class="noe-total" data-noe-total="DebitAmount" x-text="fmt(totals['DebitAmount'], 2)"></span>""", html);
        Assert.Contains("""<span class="noe-total" data-noe-total="CreditAmount" x-text="fmt(totals['CreditAmount'], 2)"></span>""", html);

        var noTotals = Render(new EditorOptionsBuilder<TestLine>("x").Column(l => l.Description).Build());
        Assert.DoesNotContain("<tfoot", noTotals);
    }

    [Fact]
    public void LookupPanel_IsRenderedOncePerEditor()
    {
        var html = Render(JournalOptions());

        Assert.Single(Regex.Matches(html, "class=\"noe-lookup-panel\""));
        Assert.Contains("""<div class="noe-lookup-panel" x-show="lookup.open" x-cloak :style="lookup.style" @mousedown.prevent>""", html);
        Assert.Contains("""@mousedown.prevent="lookupPick(k)" """.TrimEnd(), html);
    }

    [Fact]
    public void Errors_AreEmbeddedFromContext()
    {
        var ctx = new EditorRenderContext { Errors = new Dictionary<string, string[]> { ["Lines[0].DebitAmount"] = ["Malo"] } };
        var html = Render(JournalOptions(), ctx);

        using var errors = Script(html, "data-noe-errors");
        Assert.Equal("Malo", errors.RootElement.GetProperty("Lines[0].DebitAmount")[0].GetString());
    }

    [Fact]
    public void SelectOptions_CanBeResolvedPerRender()
    {
        var options = new EditorOptionsBuilder<TestLine>("journal")
            .Column(l => l.Description, c => c.Select(sp => sp.GetRequiredService<FakeTaxes>().Current))
            .Build();
        var renderer = new EditorHtmlRenderer<TestLine>(options, new NetOpenEditorLocalizationOptions().Effective);

        var first = renderer.Render([], new EditorRenderContext { Services = Services("IVA 15") });
        var second = renderer.Render([], new EditorRenderContext { Services = Services("Exento") });

        Assert.Contains("IVA 15", first, StringComparison.Ordinal);
        Assert.DoesNotContain("Exento", first, StringComparison.Ordinal);
        Assert.Contains("Exento", second, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticSelectOptions_AreUnchanged()
    {
        var options = new EditorOptionsBuilder<TestLine>("journal")
            .Column(l => l.Description, c => c.Select([new SelectOption("a", "Alfa")]))
            .Build();
        var renderer = new EditorHtmlRenderer<TestLine>(options, new NetOpenEditorLocalizationOptions().Effective);

        // No services needed: an editor that does not use the factory behaves exactly as before.
        Assert.Contains("Alfa", renderer.Render([], new EditorRenderContext()), StringComparison.Ordinal);
    }

    [Fact]
    public void AResolvedSelect_WithoutRequestServices_ThrowsNamingTheColumn()
    {
        var options = new EditorOptionsBuilder<TestLine>("journal")
            .Column(l => l.Description, c => c.Select(sp => sp.GetRequiredService<FakeTaxes>().Current))
            .Build();
        var renderer = new EditorHtmlRenderer<TestLine>(options, new NetOpenEditorLocalizationOptions().Effective);

        var ex = Assert.Throws<InvalidOperationException>(() => renderer.Render([], new EditorRenderContext()));

        Assert.Contains("Description", ex.Message, StringComparison.Ordinal);
    }

    private static IServiceProvider Services(string label)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new FakeTaxes([new SelectOption("t", label)]));
        return services.BuildServiceProvider();
    }

    private sealed class FakeTaxes(IReadOnlyList<SelectOption> current)
    {
        public IReadOnlyList<SelectOption> Current { get; } = current;
    }
}

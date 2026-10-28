using System.Text;
using System.Text.Json;
using NetOpenEditor.Columns;
using NetOpenEditor.Options;
using NetOpenEditor.Rendering;
using NetOpenEditor.Runtime;

namespace NetOpenEditor.Tests;

/// <summary>
/// Read-only is a state of editing, not a change of type: a numeric column that cannot be typed
/// into is still numeric, so it keeps its format, its total and its JSON type.
/// </summary>
public sealed class ReadOnlyAndSeedingTests
{
    private static readonly Dictionary<string, string> Locale = new() { ["totals"] = "Totales" };

    private static string Render(EditorOptions<TestLine> options, EditorRenderContext? ctx = null, IEnumerable<TestLine>? rows = null) =>
        new EditorHtmlRenderer<TestLine>(options, Locale).Render(rows ?? [], ctx ?? new EditorRenderContext());

    private static string RowsJson(EditorOptions<TestLine> options, params TestLine[] rows)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) EditorRowsJsonWriter.Write(writer, rows, options.Columns);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public void AReadOnlyNumericColumn_KeepsItsKind_AndCanStillCarryTotalAndRange()
    {
        var options = new EditorOptionsBuilder<TestLine>("ro")
            .Column(l => l.Quantity, c => c.Integer().ReadOnly().Total())
            .Column(l => l.DebitAmount, c => c.Decimal(2).ReadOnly().Min(0m).Max(10m))
            .Build();

        Assert.Equal(EditorKind.Integer, options.Columns[0].Kind);
        Assert.True(options.Columns[0].ReadOnly);
        Assert.True(options.Columns[0].Total);
        Assert.Equal(EditorKind.Decimal, options.Columns[1].Kind);
        Assert.True(options.Columns[1].ReadOnly);
    }

    [Fact]
    public void AReadOnlyColumnThatIsNotNumeric_IsStillThePlainReadOnlyCell()
    {
        var options = new EditorOptionsBuilder<TestLine>("ro")
            .Column(l => l.Description, c => c.ReadOnly())
            .Build();

        Assert.Equal(EditorKind.ReadOnly, options.Columns[0].Kind);
    }

    [Fact]
    public void AReadOnlyNumericCell_ShowsTheFormattedValue_AndPostsItHidden()
    {
        var options = new EditorOptionsBuilder<TestLine>("ro")
            .Column(l => l.DebitAmount, c => c.Decimal(2).ReadOnly().Total())
            .Build();

        var html = Render(options);

        Assert.Contains("""<span class="noe-text noe-num-text" :title="cellTitle(row, 'DebitAmount')" x-text="fmtGrouped(row['DebitAmount'], 2)"></span>""", html);
        Assert.Contains("""<input type="hidden" :name="nameFor(i, 'DebitAmount')" :value="row['DebitAmount'] ?? ''">""", html);
        // It is numeric, so the footer carries its total.
        Assert.Contains("""data-noe-total="DebitAmount" """.TrimEnd(), html);
    }

    [Fact]
    public void SeededValues_KeepTheirDeclaredType_EvenWhenHiddenOrReadOnly()
    {
        var options = new EditorOptionsBuilder<TestLine>("seed")
            .Column(l => l.DebitAmount, c => c.Hidden())
            .Column(l => l.Active, c => c.Hidden())
            .Column(l => l.Quantity, c => c.ReadOnly())
            .Column(l => l.CreditAmount, c => c.Hidden())
            .Build();

        var json = RowsJson(options, new TestLine { DebitAmount = 15m, Active = false, Quantity = 3, CreditAmount = null });

        // "15.00" and "False" are truthy strings on the client: the host had to convert them by hand.
        Assert.Contains("\"DebitAmount\":15", json, StringComparison.Ordinal);
        Assert.Contains("\"Active\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"Quantity\":3", json, StringComparison.Ordinal);
        // An empty cell stays empty: converting null to 0 would pass a cleared required cell as filled.
        Assert.Contains("\"CreditAmount\":null", json, StringComparison.Ordinal);
    }

    [Fact]
    public void HideColumns_NamingAComputedColumn_TakesItOutOfTheView()
    {
        var options = new EditorOptionsBuilder<TestLine>("hc")
            .Column(l => l.DebitAmount, c => c.Decimal(2))
            .Computed("LineTotal", "Total", c => c.Decimal(2))
            .Build();

        var html = Render(options, new EditorRenderContext { HiddenColumns = new HashSet<string>(StringComparer.Ordinal) { "LineTotal" } });

        // A computed column is never posted, so hiding it means not drawing it at all.
        Assert.DoesNotContain("data-noe-col=\"LineTotal\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("fmtGrouped(row['LineTotal']", html, StringComparison.Ordinal);
        Assert.DoesNotContain("nameFor(i, 'LineTotal')", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowDelete_False_ReachesTheClientConfig()
    {
        var options = new EditorOptionsBuilder<TestLine>("fixed")
            .Column(l => l.Description)
            .AllowAdd(false)
            .AllowDelete(false)
            .Build();

        var html = Render(options);

        Assert.Contains("\"allowDelete\":false", html, StringComparison.Ordinal);
        Assert.False(options.AllowDelete);
    }
}

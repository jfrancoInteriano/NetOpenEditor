using System.Text;
using System.Text.Json;
using NetOpenEditor.Columns;
using NetOpenEditor.Options;
using NetOpenEditor.Rendering;
using NetOpenEditor.Runtime;

namespace NetOpenEditor.Tests;

public sealed class JsonWritersTests
{
    private static string ToJson(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static EditorOptions<TestLine> JournalOptions() => new EditorOptionsBuilder<TestLine>("journal")
        .Column(l => l.AccountId, c => c.Header("Cuenta").Required().Width("20rem").Lookup("/accounts/lookup", lk => lk
            .ValueField("accountId").LabelField("display").Display("code", "name")
            .Label(l => l.AccountCode is null ? null : $"{l.AccountCode} - {l.AccountName}")
            .Companion(l => l.AccountCode, "code")
            .Companion(l => l.AccountName, "name")))
        .Column(l => l.Description, c => c.Placeholder("Detalle"))
        .Column(l => l.DebitAmount, c => c.Decimal(2).Total())
        .Column(l => l.DueDate)
        .Column(l => l.Active)
        .Column(l => l.Unit, c => c.Select([new SelectOption("UND", "Unidad")]))
        .Computed("LineTotal", "Total", c => c.Decimal(2).Total())
        .MinRows(1)
        .Build();

    [Fact]
    public void Rows_WriteDeclaredColumnsOnlyWithLabels()
    {
        var options = JournalOptions();
        var id = Guid.Parse("00000000-0000-0000-0000-000000001101");
        var rows = new[]
        {
            new TestLine { AccountId = id, AccountCode = "1101", AccountName = "Caja", Description = "Apertura", DebitAmount = 100.5m, DueDate = new DateOnly(2026, 9, 21), Active = true, Unit = "UND", Quantity = 99 },
            new TestLine(),
        };

        var json = ToJson(w => EditorRowsJsonWriter.Write(w, rows, options.Columns));
        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement[0];

        Assert.Equal(id.ToString(), first.GetProperty("AccountId").GetString());
        Assert.Equal("1101", first.GetProperty("AccountCode").GetString());
        Assert.Equal("Apertura", first.GetProperty("Description").GetString());
        Assert.Equal(100.5m, first.GetProperty("DebitAmount").GetDecimal());
        Assert.Equal("2026-09-21", first.GetProperty("DueDate").GetString());
        Assert.True(first.GetProperty("Active").GetBoolean());
        Assert.Equal("UND", first.GetProperty("Unit").GetString());
        Assert.Equal("1101 - Caja", first.GetProperty("__labels").GetProperty("AccountId").GetString());
        Assert.False(first.TryGetProperty("Quantity", out _));
        Assert.False(first.TryGetProperty("LineTotal", out _));

        var second = doc.RootElement[1];
        Assert.Equal(JsonValueKind.Null, second.GetProperty("AccountId").ValueKind);
        Assert.Equal(JsonValueKind.Null, second.GetProperty("Description").ValueKind);
        Assert.Equal(0m, second.GetProperty("DebitAmount").GetDecimal());
        Assert.Equal(JsonValueKind.Null, second.GetProperty("__labels").GetProperty("AccountId").ValueKind);
    }

    [Fact]
    public void Rows_EmptyEnumerableWritesEmptyArray()
    {
        var json = ToJson(w => EditorRowsJsonWriter.Write(w, Array.Empty<TestLine>(), JournalOptions().Columns));
        Assert.Equal("[]", json);
    }

    [Fact]
    public void Config_WritesColumnsPrefixMinRowsAndLocale()
    {
        var options = JournalOptions();
        var ctx = new EditorRenderContext { NamePrefix = "Detalle" };
        var locale = new Dictionary<string, string> { ["totals"] = "Totales" };

        var json = ToJson(w => EditorConfigJsonWriter.Write(w, options, ctx, locale));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("journal", root.GetProperty("id").GetString());
        Assert.Equal("Detalle", root.GetProperty("prefix").GetString());
        Assert.Equal(1, root.GetProperty("minRows").GetInt32());
        Assert.Equal("Totales", root.GetProperty("locale").GetProperty("totals").GetString());

        var columns = root.GetProperty("columns").EnumerateArray().ToList();
        Assert.Equal(9, columns.Count);

        var account = columns.Single(c => c.GetProperty("field").GetString() == "AccountId");
        Assert.Equal("lookup", account.GetProperty("kind").GetString());
        Assert.True(account.GetProperty("required").GetBoolean());
        Assert.Equal("20rem", account.GetProperty("width").GetString());
        var lookup = account.GetProperty("lookup");
        Assert.Equal("/accounts/lookup", lookup.GetProperty("url").GetString());
        Assert.Equal("accountId", lookup.GetProperty("value").GetString());
        Assert.Equal("display", lookup.GetProperty("label").GetString());
        Assert.Equal(new[] { "code", "name" }, lookup.GetProperty("display").EnumerateArray().Select(e => e.GetString()).ToArray());
        Assert.Equal("code", lookup.GetProperty("companions").GetProperty("AccountCode").GetString());
        Assert.Equal(220, lookup.GetProperty("debounce").GetInt32());

        var debit = columns.Single(c => c.GetProperty("field").GetString() == "DebitAmount");
        Assert.Equal("decimal", debit.GetProperty("kind").GetString());
        Assert.Equal("end", debit.GetProperty("align").GetString());
        Assert.True(debit.GetProperty("total").GetBoolean());
        Assert.Equal(2, debit.GetProperty("decimals").GetInt32());
        Assert.Equal(JsonValueKind.Null, debit.GetProperty("placeholder").ValueKind);

        Assert.Equal("hidden", columns.Single(c => c.GetProperty("field").GetString() == "AccountCode").GetProperty("kind").GetString());
        Assert.Equal("computed", columns.Single(c => c.GetProperty("field").GetString() == "LineTotal").GetProperty("kind").GetString());
        Assert.Equal("Detalle", columns.Single(c => c.GetProperty("field").GetString() == "Description").GetProperty("placeholder").GetString());
    }

    [Fact]
    public void Config_HiddenColumnsOverrideKind()
    {
        var options = JournalOptions();
        var ctx = new EditorRenderContext { HiddenColumns = new HashSet<string> { "Description", "LineTotal" } };

        var json = ToJson(w => EditorConfigJsonWriter.Write(w, options, ctx, new Dictionary<string, string>()));
        using var doc = JsonDocument.Parse(json);
        var columns = doc.RootElement.GetProperty("columns").EnumerateArray().ToList();

        Assert.Equal("hidden", columns.Single(c => c.GetProperty("field").GetString() == "Description").GetProperty("kind").GetString());
        Assert.Equal("computed", columns.Single(c => c.GetProperty("field").GetString() == "LineTotal").GetProperty("kind").GetString());
    }

    [Fact]
    public void Errors_AreWrittenAsObjectOfArrays()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Lines[0].DebitAmount"] = ["Debe ser mayor a 0"],
            ["Lines[1]"] = ["Indique débito o crédito", "Otra"],
        };

        var json = ToJson(w => EditorConfigJsonWriter.WriteErrors(w, errors));
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Debe ser mayor a 0", doc.RootElement.GetProperty("Lines[0].DebitAmount")[0].GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("Lines[1]").GetArrayLength());
    }

    [Fact]
    public void Json_EscapesScriptBreakingCharacters()
    {
        var options = new EditorOptionsBuilder<TestLine>("x").Column(l => l.Description).Build();
        var json = ToJson(w => EditorRowsJsonWriter.Write(w, [new TestLine { Description = "</script><b>" }], options.Columns));
        Assert.DoesNotContain("</script>", json);
        Assert.Contains("\\u003C/script\\u003E", json);
    }

    [Fact]
    public void KindName_IsLowercase()
    {
        Assert.Equal("readonly", EditorConfigJsonWriter.KindName(EditorKind.ReadOnly));
        Assert.Equal("integer", EditorConfigJsonWriter.KindName(EditorKind.Integer));
    }
}

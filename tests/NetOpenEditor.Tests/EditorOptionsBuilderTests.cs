using NetOpenEditor.Columns;
using NetOpenEditor.Options;

namespace NetOpenEditor.Tests;

public sealed class EditorOptionsBuilderTests
{
    private static EditorOptionsBuilder<TestLine> Builder(string id = "lines") => new(id);

    [Theory]
    [InlineData("")]
    [InlineData("1abc")]
    [InlineData("has space")]
    [InlineData("a-very-long-id-that-goes-well-beyond-the-sixty-four-character-limit-allowed")]
    public void InvalidId_Throws(string id)
    {
        var ex = Assert.Throws<EditorConfigurationException>(() => Builder(id));
        Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InfersKindFromPropertyType()
    {
        var options = Builder()
            .Column(l => l.Description)
            .Column(l => l.Quantity)
            .Column(l => l.DebitAmount)
            .Column(l => l.CreditAmount)
            .Column(l => l.DueDate)
            .Column(l => l.Active)
            .Build();

        var kinds = options.Columns.ToDictionary(c => c.Field, c => c.Kind);
        Assert.Equal(EditorKind.Text, kinds["Description"]);
        Assert.Equal(EditorKind.Integer, kinds["Quantity"]);
        Assert.Equal(EditorKind.Decimal, kinds["DebitAmount"]);
        Assert.Equal(EditorKind.Decimal, kinds["CreditAmount"]);
        Assert.Equal(EditorKind.Date, kinds["DueDate"]);
        Assert.Equal(EditorKind.Toggle, kinds["Active"]);
        Assert.Equal(0, options.Columns.Single(c => c.Field == "Quantity").Decimals);
        Assert.Equal(2, options.Columns.Single(c => c.Field == "DebitAmount").Decimals);
    }

    [Fact]
    public void GuidWithoutLookupOrSelect_Throws()
    {
        var ex = Assert.Throws<EditorConfigurationException>(() => Builder().Column(l => l.AccountId).Build());
        Assert.Contains("AccountId", ex.Message);
        Assert.Contains("Lookup", ex.Message);
    }

    [Fact]
    public void DuplicateField_Throws()
    {
        var ex = Assert.Throws<EditorConfigurationException>(() =>
            Builder().Column(l => l.Description).Column(l => l.Description).Build());
        Assert.Contains("Description", ex.Message);
    }

    [Fact]
    public void RequiredToggle_Throws()
    {
        Assert.Throws<EditorConfigurationException>(() => Builder().Column(l => l.Active, c => c.Required()).Build());
    }

    [Fact]
    public void TotalOnText_Throws()
    {
        Assert.Throws<EditorConfigurationException>(() => Builder().Column(l => l.Description, c => c.Total()).Build());
    }

    [Fact]
    public void SelectWithoutOptions_Throws()
    {
        Assert.Throws<EditorConfigurationException>(() => Builder().Column(l => l.Unit, c => c.Select([])).Build());
    }

    [Fact]
    public void NoColumns_Throws()
    {
        Assert.Throws<EditorConfigurationException>(() => Builder().Build());
    }

    [Fact]
    public void SelectorMustBeDirectMemberAccess()
    {
        Assert.Throws<EditorConfigurationException>(() => Builder().Column(l => l.Description!.Length));
    }

    [Fact]
    public void Lookup_RegistersCompanionsAsHiddenColumns()
    {
        var options = Builder()
            .Column(l => l.AccountId, c => c.Header("Cuenta").Lookup("/accounts/lookup", lk => lk
                .ValueField("accountId").LabelField("display").Display("code", "name")
                .Label(l => l.AccountCode is null ? null : $"{l.AccountCode} - {l.AccountName}")
                .Companion(l => l.AccountCode, "code")
                .Companion(l => l.AccountName, "name")))
            .Build();

        var account = options.Columns.Single(c => c.Field == "AccountId");
        Assert.Equal(EditorKind.Lookup, account.Kind);
        Assert.NotNull(account.Lookup);
        Assert.Equal("accountId", account.Lookup!.ValueField);
        Assert.Equal(new[] { "code", "name" }, account.Lookup.DisplayFields);
        Assert.Equal("code", account.Lookup.Companions["AccountCode"]);
        Assert.NotNull(account.Label);
        Assert.Equal("1101 - Caja", account.Label!(new TestLine { AccountCode = "1101", AccountName = "Caja" }));

        Assert.Equal(EditorKind.Hidden, options.Columns.Single(c => c.Field == "AccountCode").Kind);
        Assert.Equal(EditorKind.Hidden, options.Columns.Single(c => c.Field == "AccountName").Kind);
        Assert.Equal("1101", options.Columns.Single(c => c.Field == "AccountCode").Getter(new TestLine { AccountCode = "1101" }));
    }

    [Fact]
    public void Companion_KeepsExplicitColumnKind()
    {
        var options = Builder()
            .Column(l => l.AccountCode, c => c.Header("Código"))
            .Column(l => l.AccountId, c => c.Lookup("/x", lk => lk.Companion(l => l.AccountCode, "code")))
            .Build();

        Assert.Equal(EditorKind.Text, options.Columns.Single(c => c.Field == "AccountCode").Kind);
        Assert.Equal(2, options.Columns.Count);
    }

    [Fact]
    public void Computed_HasLockedKindAndNoPosting()
    {
        var options = Builder()
            .Column(l => l.Quantity)
            .Computed("LineTotal", "Total", c => c.Decimal(2).Total())
            .Build();

        var total = options.Columns.Single(c => c.Field == "LineTotal");
        Assert.Equal(EditorKind.Computed, total.Kind);
        Assert.False(total.Posts);
        Assert.True(total.Total);
        Assert.Null(total.Getter(new TestLine()));
        Assert.Throws<EditorConfigurationException>(() => Builder().Computed("X", "X", c => c.Text()));
        Assert.Throws<EditorConfigurationException>(() => Builder().Computed("1bad", "X"));
    }

    [Fact]
    public void DefaultsHeaderAlignAndMinRows()
    {
        var options = Builder()
            .Column(l => l.DebitAmount)
            .Column(l => l.Description)
            .Column(l => l.Quantity, c => c.Align(CellAlign.Center))
            .MinRows(1)
            .Build();

        Assert.Equal("Debit amount", options.Columns[0].Header);
        Assert.Equal(CellAlign.End, options.Columns[0].Align);
        Assert.Equal(CellAlign.Start, options.Columns[1].Align);
        Assert.Equal(CellAlign.Center, options.Columns[2].Align);
        Assert.Equal(1, options.MinRows);
        Assert.Throws<EditorConfigurationException>(() => Builder().MinRows(-1));
    }

    [Fact]
    public void ExplicitConfigurationIsKept()
    {
        var options = Builder()
            .Column(l => l.Unit, c => c.Header("Unidad").Placeholder("UND").Width("6rem").Required()
                .Select([new SelectOption("UND", "Unidad"), new SelectOption("KG", "Kilo")]))
            .Column(l => l.DebitAmount, c => c.Decimal(4).Total())
            .Build();

        var unit = options.Columns[0];
        Assert.Equal(EditorKind.Select, unit.Kind);
        Assert.Equal("Unidad", unit.Header);
        Assert.Equal("UND", unit.Placeholder);
        Assert.Equal("6rem", unit.WidthCss);
        Assert.True(unit.Required);
        Assert.Equal(2, unit.Options.Count);
        Assert.Equal(4, options.Columns[1].Decimals);
        Assert.True(options.Columns[1].Total);
    }

    [Fact]
    public void AllowAdd_DefaultsToTrue()
    {
        var options = Builder().Column(l => l.Description).Build();

        Assert.True(options.AllowAdd);
    }

    [Fact]
    public void AllowAdd_False_RoundTripsThroughBuild()
    {
        var options = Builder().Column(l => l.Description).AllowAdd(false).Build();

        Assert.False(options.AllowAdd);
    }

    [Fact]
    public void AllowAdd_WithoutArgument_TurnsItOn()
    {
        var options = Builder().Column(l => l.Description).AllowAdd(false).AllowAdd().Build();

        Assert.True(options.AllowAdd);
    }

    [Fact]
    public void AllowAdd_False_CoexistsWithMinRows()
    {
        // Not a contradiction: MinRows is the floor for deleting rows that arrived from the server.
        var options = Builder().Column(l => l.Description).AllowAdd(false).MinRows(2).Build();

        Assert.False(options.AllowAdd);
        Assert.Equal(2, options.MinRows);
    }

    [Fact]
    public void MinAndMax_RoundTripOnNumericColumns()
    {
        var options = Builder()
            .Column(l => l.Quantity, c => c.Integer().Min(1).Max(99))
            .Column(l => l.DebitAmount, c => c.Decimal(2).Min(0m))
            .Build();

        var quantity = options.Columns.Single(c => c.Field == "Quantity");
        var amount = options.Columns.Single(c => c.Field == "DebitAmount");

        Assert.Equal(1m, quantity.Min);
        Assert.Equal(99m, quantity.Max);
        Assert.Equal(0m, amount.Min);
        Assert.Null(amount.Max);
    }

    [Fact]
    public void MinAndMax_DefaultToNull()
    {
        var options = Builder().Column(l => l.Quantity, c => c.Integer()).Build();

        var quantity = options.Columns.Single(c => c.Field == "Quantity");
        Assert.Null(quantity.Min);
        Assert.Null(quantity.Max);
    }

    [Fact]
    public void Min_OnANonNumericColumn_Throws()
    {
        var ex = Assert.Throws<EditorConfigurationException>(() =>
            Builder().Column(l => l.Description, c => c.Text().Min(1)).Build());

        Assert.Contains("Description", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MinGreaterThanMax_Throws()
    {
        var ex = Assert.Throws<EditorConfigurationException>(() =>
            Builder().Column(l => l.Quantity, c => c.Integer().Min(10).Max(5)).Build());

        Assert.Contains("Quantity", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LookupParams_RoundTripThroughBuild()
    {
        var options = Builder()
            .Column(l => l.AccountId, c => c.Lookup("/accounts/lookup", lk => lk
                .ValueField("accountId").LabelField("display")
                .Param("providerId", "[name='ProviderId']")
                .Param("warehouseId", "#warehouse")))
            .Build();

        var lookup = options.Columns.Single(c => c.Field == "AccountId").Lookup!;

        Assert.Equal(2, lookup.Params.Count);
        Assert.Equal("[name='ProviderId']", lookup.Params["providerId"]);
        Assert.Equal("#warehouse", lookup.Params["warehouseId"]);
    }

    [Fact]
    public void LookupParams_DefaultToEmpty()
    {
        var options = Builder()
            .Column(l => l.AccountId, c => c.Lookup("/accounts/lookup", lk => lk.ValueField("accountId").LabelField("display")))
            .Build();

        Assert.Empty(options.Columns.Single(c => c.Field == "AccountId").Lookup!.Params);
    }

    [Fact]
    public void LookupParam_WithTheSameNameTwice_Throws()
    {
        var ex = Assert.Throws<EditorConfigurationException>(() =>
            Builder()
                .Column(l => l.AccountId, c => c.Lookup("/accounts/lookup", lk => lk
                    .ValueField("accountId").LabelField("display")
                    .Param("providerId", "#a")
                    .Param("providerId", "#b")))
                .Build());

        Assert.Contains("providerId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LookupParam_CannotShadowTheTermParameter()
    {
        var ex = Assert.Throws<EditorConfigurationException>(() =>
            Builder()
                .Column(l => l.AccountId, c => c.Lookup("/accounts/lookup", lk => lk
                    .ValueField("accountId").LabelField("display")
                    .TermParameter("q")
                    .Param("q", "#other")))
                .Build());

        Assert.Contains("q", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Suggest_PostsTheTypedText_NotAForeignKey()
    {
        var options = Builder()
            .Column(l => l.Description, c => c.Suggest("/products/suggest", s => s.LabelField("name")))
            .Build();

        var column = options.Columns.Single(c => c.Field == "Description");

        Assert.Equal(EditorKind.Suggest, column.Kind);
        Assert.True(column.Posts);                     // the text is the data
        Assert.Null(column.Lookup);                    // it is not a lookup in disguise
        Assert.Equal("name", column.Suggest!.LabelField);
        Assert.Equal("/products/suggest", column.Suggest.Url);
    }

    [Fact]
    public void Suggest_CarriesItsOwnParamsAndThresholds()
    {
        var options = Builder()
            .Column(l => l.Description, c => c.Suggest("/products/suggest", s => s
                .LabelField("name")
                .TermParameter("q")
                .MinLength(2)
                .Debounce(150)
                .Param("providerId", "[name='ProviderId']")))
            .Build();

        var suggest = options.Columns.Single(c => c.Field == "Description").Suggest!;

        Assert.Equal("q", suggest.TermParameter);
        Assert.Equal(2, suggest.MinLength);
        Assert.Equal(150, suggest.DebounceMs);
        Assert.Equal("[name='ProviderId']", suggest.Params["providerId"]);
    }

    [Fact]
    public void Suggest_NeedsALabelField()
    {
        var ex = Assert.Throws<EditorConfigurationException>(() =>
            Builder().Column(l => l.Description, c => c.Suggest("/products/suggest")).Build());

        Assert.Contains("Description", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SuggestAndLookup_OnTheSameColumn_Throws()
    {
        var ex = Assert.Throws<EditorConfigurationException>(() =>
            Builder()
                .Column(l => l.Description, c => c
                    .Suggest("/products/suggest", s => s.LabelField("name"))
                    .Lookup("/accounts/lookup"))
                .Build());

        Assert.Contains("Description", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Adornment_IsOffByDefault_AndRoundTrips()
    {
        var options = Builder()
            .Column(l => l.Description)
            .Column(l => l.DebitAmount, c => c.Decimal(2).Adornment())
            .Build();

        Assert.False(options.Columns.Single(c => c.Field == "Description").Adornment);
        Assert.True(options.Columns.Single(c => c.Field == "DebitAmount").Adornment);
    }

    [Fact]
    public void Adornment_OnAColumnWithNoControl_Throws()
    {
        var ex = Assert.Throws<EditorConfigurationException>(() =>
            Builder().Column(l => l.Description, c => c.Hidden().Adornment()).Build());

        Assert.Contains("Description", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EditableComputed_StillNeverPosts()
    {
        var options = Builder()
            .Column(l => l.Quantity)
            .Computed("LineTotal", "Total", c => c.Decimal(2).Editable())
            .Build();

        var computed = options.Columns.Single(c => c.Field == "LineTotal");

        Assert.True(computed.Editable);
        Assert.Equal(EditorKind.Computed, computed.Kind);
        Assert.False(computed.Posts);          // the point: it takes input but is never posted
    }

    [Fact]
    public void Computed_IsNotEditableByDefault()
    {
        var options = Builder()
            .Column(l => l.Quantity)
            .Computed("LineTotal", "Total", c => c.Decimal(2))
            .Build();

        Assert.False(options.Columns.Single(c => c.Field == "LineTotal").Editable);
    }

    [Fact]
    public void Editable_OnANonComputedColumn_Throws()
    {
        var ex = Assert.Throws<EditorConfigurationException>(() =>
            Builder().Column(l => l.DebitAmount, c => c.Decimal(2).Editable()).Build());

        Assert.Contains("DebitAmount", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEditableComputedColumn_CanCarryAnAdornment()
    {
        var options = Builder()
            .Column(l => l.Quantity)
            .Computed("Discount", "Desc.", c => c.Decimal(2).Editable().Adornment())
            .Build();

        var computed = options.Columns.Single(c => c.Field == "Discount");

        Assert.True(computed.Editable);
        Assert.True(computed.Adornment);
        Assert.False(computed.Posts);
    }

    [Fact]
    public void AComputedColumnWithoutEditable_StillRejectsAnAdornment()
    {
        var ex = Assert.Throws<EditorConfigurationException>(() =>
            Builder()
                .Column(l => l.Quantity)
                .Computed("Discount", "Desc.", c => c.Decimal(2).Adornment())
                .Build());

        Assert.Contains("renders no control", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("readonly")]
    public void HiddenAndReadOnlyColumns_StillRejectAnAdornment(string kind)
    {
        var ex = Assert.Throws<EditorConfigurationException>(() =>
            Builder()
                .Column(l => l.Description, c => { if (kind == "hidden") c.Hidden(); else c.ReadOnly(); c.Adornment(); })
                .Build());

        Assert.Contains("renders no control", ex.Message, StringComparison.Ordinal);
    }
}

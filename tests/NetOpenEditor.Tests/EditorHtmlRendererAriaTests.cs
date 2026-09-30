using NetOpenEditor.Options;
using NetOpenEditor.Rendering;
using NetOpenEditor.Runtime;

namespace NetOpenEditor.Tests;

public sealed class EditorHtmlRendererAriaTests
{
    private static string Render()
    {
        var options = new EditorOptionsBuilder<TestLine>("journal")
            .Column(l => l.Description, c => c.Header("Descripción").Required())
            .Column(l => l.DebitAmount, c => c.Header("Débito").Decimal(2).Total())
            .Column(l => l.Unit, c => c.ReadOnly())
            .Build();

        var renderer = new EditorHtmlRenderer<TestLine>(options, new NetOpenEditorLocalizationOptions().Effective);
        return renderer.Render([new TestLine { Description = "Uno" }], new EditorRenderContext());
    }

    [Fact]
    public void Table_IsAGridThatReportsItsRowCount()
    {
        var html = Render();

        Assert.Contains("<table class=\"noe-table\" role=\"grid\" :aria-rowcount=\"count() + 1\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Headers_AreColumnHeadersWithScopeAndIndex()
    {
        var html = Render();

        Assert.Contains("role=\"columnheader\" scope=\"col\" aria-colindex=\"1\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"columnheader\" scope=\"col\" aria-colindex=\"2\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Rows_And_Cells_CarryRolesAndIndexes()
    {
        var html = Render();

        Assert.Contains("<tr role=\"row\" aria-rowindex=\"1\">", html, StringComparison.Ordinal);   // header
        Assert.Contains("<tr role=\"row\" :aria-rowindex=\"i + 2\"", html, StringComparison.Ordinal); // data rows
        Assert.Contains("role=\"gridcell\" aria-colindex=\"2\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiredColumn_MarksItsInput()
    {
        var html = Render();

        Assert.Contains("aria-required=\"true\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void CellError_IsAnnouncedAndLinkedToItsMessage()
    {
        var html = Render();

        Assert.Contains(":aria-invalid=\"cellError(row, 'Description') ? 'true' : 'false'\"", html, StringComparison.Ordinal);
        Assert.Contains(":aria-describedby=\"cellError(row, 'Description') ? 'journal-r' + i + '-Description-err' : null\"", html, StringComparison.Ordinal);
        Assert.Contains(":id=\"'journal-r' + i + '-Description-err'\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void FormError_IsAnAlert_AndTotalsAnnouncePolitely()
    {
        var html = Render();

        Assert.Contains("class=\"noe-form-error\" role=\"alert\"", html, StringComparison.Ordinal);
        Assert.Contains("<tfoot class=\"noe-tfoot\"><tr role=\"row\" aria-live=\"polite\">", html, StringComparison.Ordinal);
    }
}

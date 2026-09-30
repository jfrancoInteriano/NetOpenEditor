using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.DependencyInjection;
using NetOpenEditor.DependencyInjection;
using NetOpenEditor.Runtime;
using NetOpenEditor.TagHelpers;

namespace NetOpenEditor.Tests;

public sealed class NetOpenEditorTagHelperTests
{
    private static (NetOpenEditorTagHelper Helper, ModelStateDictionary ModelState) Create(string editorId)
        => Create(editorId, withSource: false);

    private static (NetOpenEditorTagHelper Helper, ModelStateDictionary ModelState) Create(string editorId, bool withSource)
    {
        var services = new ServiceCollection();
        var builder = services.AddNetOpenEditor().AddEditor<TestLine>("journal", e => e
            .Column(l => l.Description)
            .Column(l => l.DebitAmount)
            .Column(l => l.Unit, c => c.ReadOnly()));
        if (withSource)
        {
            builder.FromSource<TestLineSource>();
        }

        // The line source is scoped, so the request services must be a scope, not the root provider.
        var scope = services.BuildServiceProvider().CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var modelState = new ModelStateDictionary();
        var viewContext = new ViewContext
        {
            HttpContext = http,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState),
        };

        return (new NetOpenEditorTagHelper { EditorId = editorId, ViewContext = viewContext }, modelState);
    }

    private static async Task<string> RunAsync(NetOpenEditorTagHelper helper)
    {
        var context = new TagHelperContext("netopen-editor", new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
        var output = new TagHelperOutput("netopen-editor", new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(context, output);
        Assert.Null(output.TagName);
        return output.Content.GetContent();
    }

    [Fact]
    public async Task RendersRegisteredEditorWithRows()
    {
        var (helper, _) = Create("journal");
        helper.Rows = new List<TestLine> { new() { Description = "Uno" } };

        var html = await RunAsync(helper);

        Assert.Contains("data-noe-id=\"journal\"", html);
        Assert.Contains("\"Uno\"", html);
        Assert.Contains("\"prefix\":\"Lines\"", html);
    }

    [Fact]
    public async Task PassesModelStateErrorsForPrefix()
    {
        var (helper, modelState) = Create("journal");
        helper.NamePrefix = "Detalle";
        modelState.AddModelError("Detalle[0].DebitAmount", "Malo");
        modelState.AddModelError("Lines[0].DebitAmount", "Ignorado");

        var html = await RunAsync(helper);

        Assert.Contains("\"prefix\":\"Detalle\"", html);
        Assert.Contains("\"Detalle[0].DebitAmount\":[\"Malo\"]", html);
        Assert.DoesNotContain("Ignorado", html);
    }

    [Fact]
    public async Task HideColumnsBecomeHiddenInputs()
    {
        var (helper, _) = Create("journal");
        helper.HideColumns = " Description , Unit ";

        var html = await RunAsync(helper);

        Assert.DoesNotContain("data-noe-field=\"Description\"", html);
        Assert.Contains(":name=\"nameFor(i, 'Description')\"", html);
    }

    [Fact]
    public async Task UnknownEditorIdThrowsNamingIt()
    {
        var (helper, _) = Create("nope");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(helper));
        Assert.Contains("nope", ex.Message);
    }

    [Fact]
    public async Task MissingEditorIdThrows()
    {
        var (helper, _) = Create("");
        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(helper));
    }

    [Fact]
    public async Task Key_WithoutRows_RendersRowsFromTheSource()
    {
        var (helper, _) = Create("journal", withSource: true);
        helper.Key = "DOC-7";

        var html = await RunAsync(helper);

        Assert.Contains("from:DOC-7", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rows_WinOverTheSource_AndTheSourceIsNeverCalled()
    {
        var (helper, _) = Create("journal", withSource: true);
        helper.Rows = new List<TestLine> { new() { Description = "capturado" } };

        var html = await RunAsync(helper);

        Assert.Contains("capturado", html, StringComparison.Ordinal);
        Assert.DoesNotContain("from:", html, StringComparison.Ordinal);

        var source = (TestLineSource)helper.ViewContext!.HttpContext.RequestServices
            .GetRequiredKeyedService<IEditorLineSource<TestLine>>("journal");
        Assert.Equal(0, source.Calls);
    }

    [Fact]
    public async Task RowsAndKeyTogether_Throws()
    {
        var (helper, _) = Create("journal", withSource: true);
        helper.Rows = new List<TestLine>();
        helper.Key = "DOC-7";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(helper));
        Assert.Contains("journal", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NeitherRowsNorKey_RendersEmpty()
    {
        var (helper, _) = Create("journal", withSource: true);

        var html = await RunAsync(helper);

        Assert.Contains("data-noe-rows>[]", html, StringComparison.Ordinal);
    }
}

using Microsoft.Extensions.DependencyInjection;
using NetOpenEditor.DependencyInjection;
using NetOpenEditor.Options;
using NetOpenEditor.Runtime;

namespace NetOpenEditor.Tests;

public sealed class RuntimeAndDiTests
{
    private static ServiceProvider Build(Action<NetOpenEditorBuilder>? more = null)
    {
        var services = new ServiceCollection();
        var builder = services.AddNetOpenEditor(
            assets => assets.AssetPrefix = "editor-assets/",
            loc => loc.UseCulture("es"));
        builder.AddEditor<TestLine>("journal", e => e.Column(l => l.Description).Column(l => l.DebitAmount, c => c.Total()));
        more?.Invoke(builder);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddEditor_RegistersKeyedRuntime()
    {
        using var sp = Build();
        var runtime = sp.GetKeyedService<IEditorRuntime>("journal");
        Assert.NotNull(runtime);
        Assert.Equal("journal", runtime!.Id);
        Assert.Null(sp.GetKeyedService<IEditorRuntime>("missing"));
    }

    [Fact]
    public void AddEditor_DuplicateIdThrows()
    {
        Assert.Throws<EditorConfigurationException>(() =>
            Build(b => b.AddEditor<TestLine>("journal", e => e.Column(l => l.Description))));
    }

    [Fact]
    public void AddEditor_InvalidConfigurationFailsAtRegistration()
    {
        Assert.Throws<EditorConfigurationException>(() =>
            Build(b => b.AddEditor<TestLine>("bad", e => e.Column(l => l.AccountId))));
    }

    [Fact]
    public async Task Render_UsesLocalizationAndRowsOfExpectedType()
    {
        using var sp = Build();
        var runtime = sp.GetRequiredKeyedService<IEditorRuntime>("journal");

        var html = await runtime.RenderAsync(new List<TestLine> { new() { Description = "Uno" } }, new EditorRenderContext());

        Assert.Contains("\"Uno\"", html);
        Assert.Contains("\"totals\":\"Totales\"", html);
    }

    [Fact]
    public async Task Render_NullRowsRendersEmptyArray()
    {
        using var sp = Build();
        var runtime = sp.GetRequiredKeyedService<IEditorRuntime>("journal");

        var html = await runtime.RenderAsync(null, new EditorRenderContext());

        Assert.Contains("data-noe-rows>[]</script>", html);
    }

    [Fact]
    public async Task Render_WrongRowTypeThrowsWithExpectedType()
    {
        using var sp = Build();
        var runtime = sp.GetRequiredKeyedService<IEditorRuntime>("journal");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.RenderAsync(new[] { "not a line" }, new EditorRenderContext()).AsTask());

        Assert.Contains("TestLine", ex.Message);
        Assert.Contains("journal", ex.Message);
    }

    [Fact]
    public void AssetPrefix_IsNormalizedToLeadingSlashNoTrailingSlash()
    {
        using var sp = Build();
        Assert.Equal("/editor-assets", sp.GetRequiredService<NetOpenEditorAssetOptions>().AssetPrefix);
        Assert.Equal("/_noe", new NetOpenEditorAssetOptions().AssetPrefix);
    }
}

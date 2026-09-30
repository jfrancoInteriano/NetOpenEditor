using Microsoft.Extensions.DependencyInjection;
using NetOpenEditor.DependencyInjection;
using NetOpenEditor.Runtime;

namespace NetOpenEditor.Tests;

public sealed class EditorRuntimeSourceTests
{
    private static ServiceProvider Provider(bool withSource)
    {
        var services = new ServiceCollection();
        var builder = services.AddNetOpenEditor()
            .AddEditor<TestLine>("journal", e => e.Column(l => l.Description));
        if (withSource) builder.FromSource<TestLineSource>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RenderFromSourceAsync_RendersTheRowsTheSourceReturns()
    {
        using var provider = Provider(withSource: true);
        using var scope = provider.CreateScope();
        var runtime = provider.GetRequiredKeyedService<IEditorRuntime>("journal");

        var html = await runtime.RenderFromSourceAsync("DOC-7", new EditorRenderContext(), scope.ServiceProvider);

        Assert.Contains("from:DOC-7", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderFromSourceAsync_WithoutRegisteredSource_ThrowsNamingEditorAndType()
    {
        using var provider = Provider(withSource: false);
        using var scope = provider.CreateScope();
        var runtime = provider.GetRequiredKeyedService<IEditorRuntime>("journal");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await runtime.RenderFromSourceAsync("DOC-7", new EditorRenderContext(), scope.ServiceProvider));

        Assert.Contains("journal", ex.Message, StringComparison.Ordinal);
        Assert.Contains("IEditorLineSource", ex.Message, StringComparison.Ordinal);
    }
}

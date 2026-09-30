using Microsoft.Extensions.DependencyInjection;
using NetOpenEditor.DependencyInjection;
using NetOpenEditor.Options;
using NetOpenEditor.Runtime;

namespace NetOpenEditor.Tests;

public sealed class TestLineSource : IEditorLineSource<TestLine>
{
    public int Calls { get; private set; }

    public ValueTask<IReadOnlyList<TestLine>> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        Calls++;
        IReadOnlyList<TestLine> rows = [new TestLine { Description = $"from:{key}" }];
        return ValueTask.FromResult(rows);
    }
}

public sealed class EditorLineSourceRegistrationTests
{
    private static IServiceCollection Services() => new ServiceCollection();

    [Fact]
    public void FromSource_RegistersScopedKeyedSourceForTheEditorId()
    {
        var services = Services();
        services.AddNetOpenEditor()
            .AddEditor<TestLine>("journal", e => e.Column(l => l.Description))
            .FromSource<TestLineSource>();

        var descriptor = services.Single(d =>
            d.ServiceType == typeof(IEditorLineSource<TestLine>) && (string?)d.ServiceKey == "journal");

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetKeyedService<IEditorLineSource<TestLine>>("journal"));
    }

    [Fact]
    public void FromSource_TwiceForTheSameEditor_Throws()
    {
        var builder = Services().AddNetOpenEditor()
            .AddEditor<TestLine>("journal", e => e.Column(l => l.Description))
            .FromSource<TestLineSource>();

        var ex = Assert.Throws<EditorConfigurationException>(() => builder.FromSource<TestLineSource>());
        Assert.Contains("journal", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddEditor_StillChainsAfterFromSource()
    {
        var services = Services();
        services.AddNetOpenEditor()
            .AddEditor<TestLine>("journal", e => e.Column(l => l.Description))
            .FromSource<TestLineSource>()
            .AddEditor<TestLine>("quote", e => e.Column(l => l.Description));

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetKeyedService<IEditorRuntime>("journal"));
        Assert.NotNull(provider.GetKeyedService<IEditorRuntime>("quote"));
    }
}

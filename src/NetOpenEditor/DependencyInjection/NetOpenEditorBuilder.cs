using Microsoft.Extensions.DependencyInjection;
using NetOpenEditor.Options;
using NetOpenEditor.Runtime;

namespace NetOpenEditor.DependencyInjection;

public class NetOpenEditorBuilder
{
    private readonly HashSet<string> _ids;
    private readonly HashSet<string> _sourceIds;

    internal NetOpenEditorBuilder(IServiceCollection services)
    {
        Services = services;
        _ids = new HashSet<string>(StringComparer.Ordinal);
        _sourceIds = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Shares the registration state of <paramref name="parent"/>: the generic builder is the same
    /// builder, plus the id and line type just registered.
    /// </summary>
    private protected NetOpenEditorBuilder(NetOpenEditorBuilder parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        Services = parent.Services;
        _ids = parent._ids;
        _sourceIds = parent._sourceIds;
    }

    public IServiceCollection Services { get; }

    /// <summary>Validates and compiles the editor once, then registers it as a keyed singleton <see cref="IEditorRuntime"/>.</summary>
    public NetOpenEditorBuilder<TLine> AddEditor<TLine>(string id, Action<EditorOptionsBuilder<TLine>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new EditorOptionsBuilder<TLine>(id);
        configure(builder);
        var options = builder.Build();

        if (!_ids.Add(options.Id))
        {
            throw new EditorConfigurationException($"Editor '{options.Id}' is registered twice.");
        }

        Services.AddKeyedSingleton<IEditorRuntime>(
            options.Id,
            (sp, _) => new EditorRuntime<TLine>(options, sp.GetRequiredService<NetOpenEditorLocalizationOptions>()));

        return new NetOpenEditorBuilder<TLine>(this, options.Id);
    }

    private protected bool RegisterSourceId(string id) => _sourceIds.Add(id);
}

/// <summary>The builder returned by <see cref="NetOpenEditorBuilder.AddEditor{TLine}"/>: remembers the editor just added so its line source needs no repeated id or type.</summary>
public sealed class NetOpenEditorBuilder<TLine> : NetOpenEditorBuilder
{
    private readonly string _editorId;

    internal NetOpenEditorBuilder(NetOpenEditorBuilder parent, string editorId) : base(parent) => _editorId = editorId;

    /// <summary>Registers <typeparamref name="TSource"/> as the scoped, keyed line source of the editor just added.</summary>
    public NetOpenEditorBuilder<TLine> FromSource<TSource>() where TSource : class, IEditorLineSource<TLine>
    {
        if (!RegisterSourceId(_editorId))
        {
            throw new EditorConfigurationException($"Editor '{_editorId}' already has a line source registered.");
        }

        Services.AddKeyedScoped<IEditorLineSource<TLine>, TSource>(_editorId);
        return this;
    }
}

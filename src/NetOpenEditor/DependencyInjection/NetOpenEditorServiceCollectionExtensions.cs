using Microsoft.Extensions.DependencyInjection;
using NetOpenEditor.Options;

namespace NetOpenEditor.DependencyInjection;

public static class NetOpenEditorServiceCollectionExtensions
{
    public static NetOpenEditorBuilder AddNetOpenEditor(
        this IServiceCollection services,
        Action<NetOpenEditorAssetOptions>? configureAssets = null,
        Action<NetOpenEditorLocalizationOptions>? configureLocalization = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assets = new NetOpenEditorAssetOptions();
        configureAssets?.Invoke(assets);

        var localization = new NetOpenEditorLocalizationOptions();
        configureLocalization?.Invoke(localization);

        services.AddSingleton(assets);
        services.AddSingleton(localization);

        return new NetOpenEditorBuilder(services);
    }
}

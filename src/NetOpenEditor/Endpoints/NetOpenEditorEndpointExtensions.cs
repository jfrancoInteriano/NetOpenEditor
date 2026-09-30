using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NetOpenEditor.Assets;
using NetOpenEditor.Options;

namespace NetOpenEditor.Endpoints;

public static class NetOpenEditorEndpointExtensions
{
    /// <summary>Maps the embedded JS and CSS under <see cref="NetOpenEditorAssetOptions.AssetPrefix"/>. Anonymous; call it outside any authorized group.</summary>
    public static IEndpointRouteBuilder MapNetOpenEditor(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var options = endpoints.ServiceProvider.GetService<NetOpenEditorAssetOptions>() ?? new NetOpenEditorAssetOptions();
        MapAsset(endpoints, $"{options.AssetPrefix}/netopeneditor.js", EmbeddedEditorAssets.Script);
        MapAsset(endpoints, $"{options.AssetPrefix}/netopeneditor.css", EmbeddedEditorAssets.Stylesheet);
        return endpoints;
    }

    private static void MapAsset(IEndpointRouteBuilder endpoints, string path, EmbeddedAsset asset)
    {
        endpoints.MapGet(path, (HttpContext http) =>
        {
            var immutable = string.Equals(http.Request.Query["v"], asset.Version, StringComparison.Ordinal);
            http.Response.Headers.CacheControl = immutable ? "public, max-age=31536000, immutable" : "no-cache";
            http.Response.Headers.ETag = $"\"{asset.Version}\"";
            return Results.Bytes(asset.Bytes, asset.ContentType);
        }).AllowAnonymous();
    }
}

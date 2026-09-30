using System.Reflection;
using System.Security.Cryptography;

namespace NetOpenEditor.Assets;

public sealed record EmbeddedAsset(byte[] Bytes, string Version, string ContentType);

/// <summary>Client runtime and stylesheet embedded in the assembly, loaded once per process. Version = first 12 hex chars of SHA-256 (immutable ?v= caching).</summary>
public static class EmbeddedEditorAssets
{
    private const string ResourceRoot = "NetOpenEditor.Assets.";

    public static readonly EmbeddedAsset Script = Load("netopeneditor.js", "text/javascript; charset=utf-8");
    public static readonly EmbeddedAsset Stylesheet = Load("netopeneditor.css", "text/css; charset=utf-8");

    private static EmbeddedAsset Load(string fileName, string contentType)
    {
        var assembly = typeof(EmbeddedEditorAssets).Assembly;
        var logicalName = ResourceRoot + fileName;

        using var stream = assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded asset '{logicalName}' is missing from the assembly.");

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var version = Convert.ToHexString(SHA256.HashData(bytes))[..12].ToLowerInvariant();

        return new EmbeddedAsset(bytes, version, contentType);
    }
}

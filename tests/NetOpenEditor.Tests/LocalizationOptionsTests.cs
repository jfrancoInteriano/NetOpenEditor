using NetOpenEditor.Options;

namespace NetOpenEditor.Tests;

public sealed class LocalizationOptionsTests
{
    [Fact]
    public void DefaultIsEnglish()
    {
        var loc = new NetOpenEditorLocalizationOptions();
        Assert.Equal("Remove line", loc.Effective["remove"]);
        Assert.Equal("{n} lines", loc.Effective["rows.many"]);
        Assert.Equal(14, loc.Effective.Count);
    }

    [Fact]
    public void SpanishPreset()
    {
        var loc = new NetOpenEditorLocalizationOptions().UseCulture("es");
        Assert.Equal("Eliminar línea", loc.Effective["remove"]);
        Assert.Equal("Sin resultados", loc.Effective["lookup.empty"]);
        Assert.Equal("Se requiere al menos {n} línea(s)", loc.Effective["minRows"]);
    }

    [Fact]
    public void UnknownCultureFallsBackToEnglish()
    {
        var loc = new NetOpenEditorLocalizationOptions().UseCulture("fr-FR");
        Assert.Equal("Totals", loc.Effective["totals"]);
    }

    [Fact]
    public void SetOverridesPreset()
    {
        var loc = new NetOpenEditorLocalizationOptions().UseCulture("es").Set("totals", "Sumas");
        Assert.Equal("Sumas", loc.Effective["totals"]);
        Assert.Equal("Eliminar línea", loc.Effective["remove"]);
    }

    [Fact]
    public void UnknownKeyThrows()
    {
        Assert.Throws<ArgumentException>(() => new NetOpenEditorLocalizationOptions().Set("nope", "x"));
    }
}

using Microsoft.AspNetCore.Mvc.ModelBinding;
using NetOpenEditor.Rendering;

namespace NetOpenEditor.Tests;

public sealed class ModelStateErrorsTests
{
    [Fact]
    public void CollectsOnlyPrefixedKeys()
    {
        var state = new ModelStateDictionary();
        state.AddModelError("Description", "Header error");
        state.AddModelError("Lines[0].DebitAmount", "Debe ser mayor a 0");
        state.AddModelError("Lines[0].DebitAmount", "Segundo");
        state.AddModelError("Lines[2]", "Indique débito o crédito");
        state.AddModelError("Lines", "Agregue líneas");
        state.AddModelError("Other[0].X", "No");

        var errors = ModelStateErrors.Collect(state, "Lines");

        Assert.Equal(2, errors.Count);
        Assert.Equal(new[] { "Debe ser mayor a 0", "Segundo" }, errors["Lines[0].DebitAmount"]);
        Assert.Equal(new[] { "Indique débito o crédito" }, errors["Lines[2]"]);
    }

    [Fact]
    public void ExceptionWithoutMessageBecomesGenericText()
    {
        var state = new ModelStateDictionary();
        state.AddModelError("Lines[1].Quantity", new FormatException(), new EmptyModelMetadataProvider().GetMetadataForType(typeof(int)));

        var errors = ModelStateErrors.Collect(state, "Lines");

        Assert.Single(errors["Lines[1].Quantity"]);
        Assert.False(string.IsNullOrWhiteSpace(errors["Lines[1].Quantity"][0]));
    }

    [Fact]
    public void EmptyPrefixThrows()
    {
        Assert.Throws<ArgumentException>(() => ModelStateErrors.Collect(new ModelStateDictionary(), " "));
    }

    [Fact]
    public void KeysAreCaseInsensitiveOnPrefix()
    {
        var state = new ModelStateDictionary();
        state.AddModelError("lines[0].Description", "x");

        var errors = ModelStateErrors.Collect(state, "Lines");

        Assert.Single(errors);
    }
}

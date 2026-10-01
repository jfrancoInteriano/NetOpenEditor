using Microsoft.AspNetCore.Http;
using NetOpenEditor.Columns;

namespace NetOpenEditor.Example.Data;

/// <summary>
/// Stand-in for a per-request catalogue: the tax list depends on the company configuration, so it
/// cannot be captured when the editor is registered at startup. Here it varies by query string.
/// </summary>
public sealed class TaxCatalog(IHttpContextAccessor http)
{
    public IReadOnlyList<SelectOption> Current()
    {
        var reduced = string.Equals(http.HttpContext?.Request.Query["taxes"], "reduced", StringComparison.Ordinal);

        return reduced
            ? [new SelectOption("0", "Exento"), new SelectOption("15", "ISV 15%")]
            : [new SelectOption("0", "Exento"), new SelectOption("15", "ISV 15%"), new SelectOption("18", "ISV 18%")];
    }
}

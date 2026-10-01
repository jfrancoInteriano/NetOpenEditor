using Microsoft.AspNetCore.Mvc;
using NetOpenEditor.Example.Data;
using NetOpenEditor.Example.Models;

namespace NetOpenEditor.Example.Controllers;

/// <summary>Purchase request: the product cell posts free text, with a type-ahead over the catalogue.</summary>
public sealed class RequestController : Controller
{
    [HttpGet("/request/edit")]
    public IActionResult Edit() => View(new RequestForm());

    [HttpPost("/request/edit")]
    public IActionResult Edit(RequestForm form)
    {
        if (form.Lines.Count == 0)
        {
            ModelState.AddModelError("Lines", "Agregue al menos una línea");
        }

        return ModelState.IsValid ? View("Result", form) : View(form);
    }

    /// <summary>Returns items with no "display" field on purpose: the label field must be configurable.</summary>
    [HttpGet("/products/suggest")]
    public IActionResult Suggest(string? term, string? providerId)
    {
        var q = (term ?? string.Empty).Trim();
        var provider = (providerId ?? string.Empty).Trim();

        var items = SampleCatalog.Products
            .Where(p => provider.Length == 0 || string.Equals(p.ProviderId, provider, StringComparison.Ordinal))
            .Where(p => q.Length == 0
                        || p.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(p => new { productId = p.ProductId, code = p.Code, name = p.Name, unitOfMeasure = "UND" });

        return Json(items);
    }
}

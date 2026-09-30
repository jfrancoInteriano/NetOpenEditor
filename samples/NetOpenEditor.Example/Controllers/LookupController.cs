using Microsoft.AspNetCore.Mvc;
using NetOpenEditor.Example.Data;

namespace NetOpenEditor.Example.Controllers;

public sealed class LookupController : Controller
{
    [HttpGet("/accounts/lookup")]
    public IActionResult Accounts(string? term)
    {
        var q = (term ?? string.Empty).Trim();
        var items = SampleCatalog.Accounts
            .Where(a => q.Length == 0
                        || a.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || a.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(a => new { accountId = a.AccountId, code = a.Code, name = a.Name, display = a.Display });

        return Json(items);
    }

    [HttpGet("/products/lookup")]
    public IActionResult Products(string? term)
    {
        var q = (term ?? string.Empty).Trim();
        var items = SampleCatalog.Products
            .Where(p => q.Length == 0
                        || p.Code.Contains(q, StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(p => new { productId = p.ProductId, code = p.Code, name = p.Name, display = p.Display, price = p.Price, taxRate = p.TaxRate });

        return Json(items);
    }
}

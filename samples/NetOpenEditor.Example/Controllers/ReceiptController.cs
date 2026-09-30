using Microsoft.AspNetCore.Mvc;
using NetOpenEditor.Example.Models;

namespace NetOpenEditor.Example.Controllers;

/// <summary>Goods receipt: the lines arrive from the purchase order and the user only fills what arrived.</summary>
public sealed class ReceiptController : Controller
{
    private static ReceiptForm Pending() => new()
    {
        Reference = "OC-2026-0042",
        Lines =
        [
            new() { PurchaseOrderDetailId = Guid.NewGuid(), ProductCode = "P-001", ProductName = "Laptop", Ordered = 3 },
            new() { PurchaseOrderDetailId = Guid.NewGuid(), ProductCode = "P-002", ProductName = "Mouse", Ordered = 10 },
            new() { PurchaseOrderDetailId = Guid.NewGuid(), ProductCode = "P-003", ProductName = "Monitor", Ordered = 2 },
        ],
    };

    [HttpGet("/receipt/edit")]
    public IActionResult Edit(bool empty = false)
    {
        var form = Pending();
        if (empty) form.Lines.Clear();   // shows what an add-less editor renders with no rows
        return View(form);
    }

    [HttpPost("/receipt/edit")]
    public IActionResult Edit(ReceiptForm form)
    {
        if (form.Lines.Count == 0)
        {
            ModelState.AddModelError("Lines", "La recepción no tiene líneas");
        }

        return ModelState.IsValid ? View("Result", form) : View(form);
    }
}

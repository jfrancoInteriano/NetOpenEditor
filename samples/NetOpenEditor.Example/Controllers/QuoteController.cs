using Microsoft.AspNetCore.Mvc;
using NetOpenEditor.Example.Models;

namespace NetOpenEditor.Example.Controllers;

public sealed class QuoteController : Controller
{
    [HttpGet("/quote/edit")]
    public IActionResult Edit(string id = "Q-1")
    {
        // No lines are loaded here: the editor pulls them from IEditorLineSource<QuoteLine>.
        ViewData["LoadFromSource"] = true;
        return View(new QuoteForm { Id = id, CustomerName = "Cliente de prueba" });
    }

    [HttpPost("/quote/edit")]
    public IActionResult Edit(QuoteForm form)
    {
        if (form.Lines.Count == 0)
        {
            ModelState.AddModelError("Lines", "Agregue al menos una línea");
        }

        return ModelState.IsValid ? View("Result", form) : View(form);
    }
}

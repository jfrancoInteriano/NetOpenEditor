using Microsoft.AspNetCore.Mvc;
using NetOpenEditor.Example.Data;
using NetOpenEditor.Example.Models;

namespace NetOpenEditor.Example.Controllers;

public sealed class JournalController : Controller
{
    [HttpGet("/journal/edit")]
    public IActionResult Edit()
    {
        var caja = SampleCatalog.Accounts[0];
        var capital = SampleCatalog.Accounts[5];
        var form = new JournalEntryForm
        {
            Description = "Asiento de apertura",
            Lines =
            [
                new JournalLine { AccountId = caja.AccountId, AccountCode = caja.Code, AccountName = caja.Name, Description = "Apertura caja", DebitAmount = 100m },
                new JournalLine { AccountId = capital.AccountId, AccountCode = capital.Code, AccountName = capital.Name, Description = "Apertura capital", CreditAmount = 100m },
            ],
        };

        return View(form);
    }

    [HttpPost("/journal/edit")]
    public IActionResult Edit(JournalEntryForm form)
    {
        for (var i = 0; i < form.Lines.Count; i++)
        {
            var line = form.Lines[i];
            if (line.DebitAmount == 0 && line.CreditAmount == 0)
            {
                ModelState.AddModelError($"Lines[{i}]", "Indique débito o crédito");
            }
        }

        if (form.Lines.Count == 0)
        {
            ModelState.AddModelError("Lines", "Agregue al menos una línea");
        }

        return ModelState.IsValid ? View("Result", form) : View(form);
    }
}

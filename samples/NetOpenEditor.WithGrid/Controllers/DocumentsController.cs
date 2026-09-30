using Microsoft.AspNetCore.Mvc;
using NetOpenEditor.WithGrid.Data;
using NetOpenEditor.WithGrid.Models;

namespace NetOpenEditor.WithGrid.Controllers;

public sealed class DocumentsController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => Redirect("/documents");

    /// <summary>The grid. Rows expand in place; nothing about the selection lives in the URL.</summary>
    [HttpGet("/documents")]
    public IActionResult Documents() => View("Index");

    /// <summary>
    /// The editor for one document, as a fragment. The page injects it into a detail row under the
    /// grid row that was expanded.
    /// </summary>
    [HttpGet("/documents/{id}/editor")]
    public IActionResult Editor(string id)
    {
        var document = DocumentCatalog.Find(id);
        if (document is null) return NotFound($"No existe el documento '{id}'.");

        return PartialView("_LinesEditor", document);
    }

    [HttpPost("/documents/{id}/lines")]
    public IActionResult SaveLines(string id, [FromForm] List<DocumentLine> lines)
    {
        if (DocumentCatalog.Find(id) is null) return NotFound($"No existe el documento '{id}'.");

        DocumentCatalog.Save(id, lines ?? []);
        return Ok(new { saved = true, id, total = DocumentCatalog.Find(id)!.Total });
    }
}

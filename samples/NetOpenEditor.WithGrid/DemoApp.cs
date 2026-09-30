using NetOpenEditor.DependencyInjection;
using NetOpenEditor.Endpoints;
using NetOpenEditor.WithGrid.Data;
using NetOpenEditor.WithGrid.Models;
using NetOpenGrid.Application.DataSources;
using NetOpenGrid.Infrastructure;
using NetOpenGrid.Infrastructure.Endpoints;

namespace NetOpenEditor.WithGrid;

/// <summary>
/// Built as a method so the tests can boot the very same app on Kestrel.
///
/// <para>Both components share one page: the grid rides in a same-origin iframe
/// (<c>/netgrid/documents?embed=1</c>, the way NetOpenGrid 1.0.2 documents embedding) and the editor
/// renders inline underneath it, loading the selected document's lines from its line source.</para>
/// </summary>
public static class DemoApp
{
    public static WebApplication Build(WebApplicationOptions options)
    {
        var builder = WebApplication.CreateBuilder(options);

        builder.Services.AddControllersWithViews();
        builder.Services.AddHttpContextAccessor();   // the grid fragment tag helper needs it

        builder.Services.AddNetOpenGrid()
            .AddGrid<Document>("documents", grid => grid
                .WithTitle("Documentos")
                .WithTheme("grid")            // embedded in the package, served at /_netgrid/css
                .WithMinHeight("")            // the host view decides the height
                .WithDefaultPageSize(5)
                .AddColumn("id", d => d.Id, c => c.Header("Folio").Searchable())
                .AddColumn("customer", d => d.Customer, c => c.Header("Cliente").Searchable())
                .AddColumn("date", d => d.Date, c => c.Header("Fecha"))
                .AddColumn("total", d => d.Total, c => c
                    .Header("Total")
                    // Anchored so saving lines can refresh just this cell.
                    .RawCellHtml(d => $"<span data-total-for=\"{d.Id}\">{d.Total:0.00}</span>"))
                .AddColumn("edit", d => d.Id, c => c
                    .Header("")
                    .Sortable(false).Filterable(false).Searchable(false)
                    // The page turns this into a detail row under this very row; no navigation.
                    .RawCellHtml(d => $"<button type=\"button\" class=\"doc-toggle\" data-doc-id=\"{d.Id}\" " +
                                      "data-label-closed=\"Editar líneas\" data-label-open=\"Cerrar\">Editar líneas</button>")),
                (_, opts) => new InMemoryGridDataSource<Document>(opts, DocumentCatalog.Documents));

        builder.Services.AddNetOpenEditor(configureLocalization: loc => loc.UseCulture("es"))
            .AddEditor<DocumentLine>("doc-lines", e => e
                .Column(l => l.Description, c => c.Header("Descripción").Required())
                .Column(l => l.Quantity, c => c.Header("Cantidad").Integer().Width("8rem"))
                .Column(l => l.UnitPrice, c => c.Header("Precio").Decimal(2).Total().Width("9rem")))
            .FromSource<DocumentLineSource>();

        var app = builder.Build();
        app.MapNetOpenGrid();      // serves /netgrid/{id} and the grid assets under /_netgrid
        app.MapNetOpenEditor();    // serves the editor assets under /_noe
        app.MapControllers();
        return app;
    }
}

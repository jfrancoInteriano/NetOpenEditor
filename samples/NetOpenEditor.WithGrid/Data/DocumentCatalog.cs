using NetOpenEditor.WithGrid.Models;

namespace NetOpenEditor.WithGrid.Data;

/// <summary>
/// In-memory stand-in for the module repositories the host app would use. Mutable on purpose: saving the
/// lines of a document updates its grid total, which is how the demo shows the binding round-trip.
/// </summary>
public static class DocumentCatalog
{
    private static readonly Dictionary<string, List<DocumentLine>> LinesById = new(StringComparer.Ordinal)
    {
        ["DOC-1"] =
        [
            new() { Description = "Laptop", Quantity = 1, UnitPrice = 1200m },
            new() { Description = "Mouse", Quantity = 2, UnitPrice = 25.5m },
        ],
        ["DOC-2"] =
        [
            new() { Description = "Monitor", Quantity = 2, UnitPrice = 340m },
            new() { Description = "Cable HDMI", Quantity = 4, UnitPrice = 65m },
        ],
        ["DOC-3"] =
        [
            new() { Description = "Fertilizante 50kg", Quantity = 20, UnitPrice = 85m },
            new() { Description = "Semilla certificada", Quantity = 6, UnitPrice = 70m },
            new() { Description = "Flete", Quantity = 1, UnitPrice = 90m },
        ],
        ["DOC-4"] =
        [
            new() { Description = "Servicio de transporte", Quantity = 2, UnitPrice = 250m },
            new() { Description = "Maniobra de carga", Quantity = 1, UnitPrice = 60m },
        ],
        ["DOC-5"] =
        [
            new() { Description = "Harina 25kg", Quantity = 30, UnitPrice = 45m },
            new() { Description = "Levadura", Quantity = 10, UnitPrice = 18m },
            new() { Description = "Empaque", Quantity = 5, UnitPrice = 44m },
        ],
    };

    /// <summary>The same list instance the grid's data source reads, so a save is visible on the next render.</summary>
    public static readonly List<Document> Documents =
    [
        Build("DOC-1", "Ferretería Robles", new DateOnly(2026, 9, 1)),
        Build("DOC-2", "Constructora Vega", new DateOnly(2026, 9, 3)),
        Build("DOC-3", "Agroinsumos del Norte", new DateOnly(2026, 9, 7)),
        Build("DOC-4", "Transportes Lara", new DateOnly(2026, 9, 11)),
        Build("DOC-5", "Panadería Centro", new DateOnly(2026, 9, 15)),
    ];

    public static Document? Find(string documentId) =>
        Documents.FirstOrDefault(d => string.Equals(d.Id, documentId, StringComparison.Ordinal));

    public static IReadOnlyList<DocumentLine> LinesOf(string documentId) =>
        LinesById.TryGetValue(documentId, out var lines) ? lines : [];

    /// <summary>Replaces a document's lines and refreshes the total the grid shows.</summary>
    public static void Save(string documentId, IEnumerable<DocumentLine> lines)
    {
        var kept = lines.Where(l => !string.IsNullOrWhiteSpace(l.Description)).ToList();
        LinesById[documentId] = kept;

        var index = Documents.FindIndex(d => string.Equals(d.Id, documentId, StringComparison.Ordinal));
        if (index >= 0)
        {
            Documents[index] = Documents[index] with { Total = kept.Sum(l => l.Quantity * l.UnitPrice) };
        }
    }

    private static Document Build(string id, string customer, DateOnly date) =>
        new(id, customer, date, LinesById[id].Sum(l => l.Quantity * l.UnitPrice));
}

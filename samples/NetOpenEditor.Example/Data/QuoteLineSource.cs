using NetOpenEditor.Example.Data;
using NetOpenEditor.Example.Models;
using NetOpenEditor.Runtime;

namespace NetOpenEditor.Example.Data;

/// <summary>Stand-in for a module repository: the editor pulls its lines from here, like the ERP grids do.</summary>
public sealed class QuoteLineSource : IEditorLineSource<QuoteLine>
{
    public ValueTask<IReadOnlyList<QuoteLine>> LoadAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(key, "Q-1", StringComparison.Ordinal))
        {
            // A key with no document is a new document, not an error.
            return ValueTask.FromResult<IReadOnlyList<QuoteLine>>([]);
        }

        var product = SampleCatalog.Products[0];
        IReadOnlyList<QuoteLine> lines =
        [
            new QuoteLine
            {
                ProductId = product.ProductId,
                ProductCode = product.Code,
                ProductName = "desde-source " + product.Name,
                Quantity = 2,
                UnitPrice = product.Price,
                TaxRate = product.TaxRate,
            },
        ];
        return ValueTask.FromResult(lines);
    }
}

using System.ComponentModel.DataAnnotations;

namespace NetOpenEditor.Example.Models;

/// <summary>A line of a goods receipt: it comes from the purchase order, so the user cannot add lines.</summary>
public sealed class ReceiptLine
{
    public Guid? PurchaseOrderDetailId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public decimal Ordered { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "La cantidad recibida no puede ser negativa")]
    public decimal Received { get; set; }

    public string? Batch { get; set; }
}

public sealed class ReceiptForm
{
    public string? Reference { get; set; }
    public List<ReceiptLine> Lines { get; set; } = [];
}

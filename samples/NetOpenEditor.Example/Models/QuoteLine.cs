using System.ComponentModel.DataAnnotations;

namespace NetOpenEditor.Example.Models;

public sealed class QuoteLine
{
    [Required(ErrorMessage = "El producto es requerido")]
    public Guid? ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0")]
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }

    /// <summary>Always posted as a percentage, whatever unit the row is being edited in.</summary>
    public decimal DiscountPercent { get; set; }
}

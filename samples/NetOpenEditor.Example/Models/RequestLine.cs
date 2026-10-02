using System.ComponentModel.DataAnnotations;

namespace NetOpenEditor.Example.Models;

/// <summary>
/// A requested item. The description is what gets posted and is required; the catalogue ids are
/// optional, because asking for something the catalogue does not carry yet is a valid request.
/// </summary>
public sealed class RequestLine
{
    [Required(ErrorMessage = "La descripción es requerida")]
    [StringLength(500)]
    public string? Description { get; set; }

    public Guid? ProductId { get; set; }
    public Guid? ServiceId { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal Quantity { get; set; }
    public Guid? AccountId { get; set; }
    public string? AccountCode { get; set; }
}

public sealed class RequestForm
{
    public string? ProviderId { get; set; } = "PROV-A";
    public List<RequestLine> Lines { get; set; } = [];
}

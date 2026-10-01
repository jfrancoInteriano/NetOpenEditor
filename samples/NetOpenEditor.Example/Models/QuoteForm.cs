using System.ComponentModel.DataAnnotations;

namespace NetOpenEditor.Example.Models;

public sealed class QuoteForm
{
    /// <summary>Document key: handed to the editor so its line source loads this quote's lines.</summary>
    public string? Id { get; set; }

    [Required(ErrorMessage = "El cliente es requerido")]
    public string? CustomerName { get; set; }
    public string? ProviderId { get; set; } = "PROV-A";

    public List<QuoteLine> Lines { get; set; } = [];
}

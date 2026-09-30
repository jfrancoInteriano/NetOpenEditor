using System.ComponentModel.DataAnnotations;

namespace NetOpenEditor.Example.Models;

public sealed class JournalLine
{
    [Required(ErrorMessage = "La cuenta es requerida")]
    public Guid? AccountId { get; set; }

    /// <summary>Display-only companions, round-tripped so the editor can re-label the row after a failed POST.</summary>
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }

    public string? Description { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
}

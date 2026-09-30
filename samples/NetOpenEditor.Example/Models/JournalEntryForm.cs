using System.ComponentModel.DataAnnotations;

namespace NetOpenEditor.Example.Models;

public sealed class JournalEntryForm
{
    public DateOnly EntryDate { get; set; } = new(2026, 9, 21);

    [Required(ErrorMessage = "La descripción es requerida")]
    public string? Description { get; set; }

    public List<JournalLine> Lines { get; set; } = [];
}

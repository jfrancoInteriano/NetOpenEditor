namespace NetOpenEditor.WithGrid.Models;

public sealed record Document(string Id, string Customer, DateOnly Date, decimal Total);

public sealed class DocumentLine
{
    public string? Description { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

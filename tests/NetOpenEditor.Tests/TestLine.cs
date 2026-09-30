namespace NetOpenEditor.Tests;

public sealed class TestLine
{
    public Guid? AccountId { get; set; }
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public string? Description { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal? CreditAmount { get; set; }
    public int Quantity { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool Active { get; set; }
    public Guid? ProductId { get; set; }
    public string? Unit { get; set; }
}

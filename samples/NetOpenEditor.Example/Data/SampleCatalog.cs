namespace NetOpenEditor.Example.Data;

public sealed record AccountItem(Guid AccountId, string Code, string Name)
{
    public string Display => $"{Code} - {Name}";
}

public static class SampleCatalog
{
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    public static readonly IReadOnlyList<AccountItem> Accounts =
    [
        new(Id(1101), "1101", "Caja"),
        new(Id(1102), "1102", "Bancos"),
        new(Id(1201), "1201", "Clientes"),
        new(Id(1301), "1301", "Inventario"),
        new(Id(2101), "2101", "Proveedores"),
        new(Id(3101), "3101", "Capital"),
        new(Id(4101), "4101", "Ventas"),
        new(Id(5101), "5101", "Costo de ventas"),
        new(Id(6101), "6101", "Gastos de administración"),
        new(Id(6201), "6201", "Gastos de ventas"),
    ];

    public static readonly IReadOnlyList<ProductItem> Products =
    [
        new(Id(1), "P-001", "Laptop", 1200m, 15m, "PROV-A"),
        new(Id(2), "P-002", "Mouse", 25.5m, 15m, "PROV-A"),
        new(Id(3), "P-003", "Monitor", 340m, 15m, "PROV-B"),
        new(Id(4), "P-004", "Servicio de instalación", 80m, 0m, "PROV-B"),
    ];

    public sealed record ProductItem(Guid ProductId, string Code, string Name, decimal Price, decimal TaxRate, string ProviderId)
    {
        public string Display => $"{Code} - {Name}";
    }
}

using Microsoft.AspNetCore.Localization;
using NetOpenEditor.DependencyInjection;
using NetOpenEditor.Endpoints;
using NetOpenEditor.Example.Data;
using NetOpenEditor.Example.Models;

namespace NetOpenEditor.Example;

public static class SampleApp
{
    /// <summary>Builds the sample host. Tests call this directly (Kestrel on a random port) instead of Program.Main.</summary>
    public static WebApplication Build(WebApplicationOptions options)
    {
        var builder = WebApplication.CreateBuilder(options);

        // Controllers and compiled views come from the assembly named by ApplicationName
        // (the entry assembly when run normally; the E2E fixture sets it explicitly).
        builder.Services.AddControllersWithViews();
        builder.Services.AddRequestLocalization(o =>
        {
            o.DefaultRequestCulture = new RequestCulture("en-US");
            o.SupportedCultures = [new System.Globalization.CultureInfo("en-US")];
            o.SupportedUICultures = [new System.Globalization.CultureInfo("en-US")];
        });

        builder.Services.AddNetOpenEditor(configureLocalization: loc => loc.UseCulture("es"))
            .AddEditor<JournalLine>("journal-lines", e => e
                .Column(l => l.AccountId, c => c.Header("Cuenta").Required().Width("22rem").Placeholder("Buscar cuenta...")
                    .Lookup("/accounts/lookup", lk => lk
                        .ValueField("accountId").LabelField("display").Display("code", "name")
                        .Label(l => l.AccountCode is null ? null : $"{l.AccountCode} - {l.AccountName}")
                        .Companion(l => l.AccountCode, "code")
                        .Companion(l => l.AccountName, "name")))
                .Column(l => l.Description, c => c.Header("Descripción").Placeholder("Detalle de línea..."))
                .Column(l => l.DebitAmount, c => c.Header("Débito").Decimal(2).Total().Width("9rem"))
                .Column(l => l.CreditAmount, c => c.Header("Crédito").Decimal(2).Total().Width("9rem"))
                .MinRows(1))
            .AddEditor<QuoteLine>("quote-lines", e => e
                .Column(l => l.ProductId, c => c.Header("Producto").Required().Width("24rem").Placeholder("Buscar producto...")
                    .Lookup("/products/lookup", lk => lk
                        .ValueField("productId").LabelField("display").Display("code", "name")
                        .Label(l => l.ProductCode is null ? null : $"{l.ProductCode} - {l.ProductName}")
                        .Companion(l => l.ProductCode, "code")
                        .Companion(l => l.ProductName, "name")))
                .Column(l => l.Quantity, c => c.Header("Cantidad").Required().Width("7rem"))
                .Column(l => l.UnitPrice, c => c.Header("Precio").Decimal(2).Width("9rem"))
                .Column(l => l.TaxRate, c => c.Header("ISV %").Decimal(2).Width("7rem"))
                .Computed("LineTotal", "Total", c => c.Decimal(2).Total().Width("9rem"))
                .MinRows(1))
            .FromSource<QuoteLineSource>();

        var app = builder.Build();
        app.UseRequestLocalization();
        app.UseStaticFiles();
        app.MapNetOpenEditor();
        app.MapControllers();
        return app;
    }
}

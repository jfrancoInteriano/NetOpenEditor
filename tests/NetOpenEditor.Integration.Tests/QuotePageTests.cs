using System.Net;

namespace NetOpenEditor.Integration.Tests;

public sealed class QuotePageTests : IClassFixture<SampleAppFactory>
{
    private readonly HttpClient _client;

    public QuotePageTests(SampleAppFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task EditPage_RendersComputedColumnWithoutName()
    {
        var html = await _client.GetStringAsync("/quote/edit");

        Assert.Contains("data-noe-id=\"quote-lines\"", html);
        Assert.Contains("x-text=\"fmt(row['LineTotal'], 2)\"", html);
        Assert.DoesNotContain("nameFor(i, 'LineTotal')", html);
        Assert.Contains("data-noe-total=\"LineTotal\"", html);
    }

    [Fact]
    public async Task Post_BindsIntegerQuantityAndIgnoresComputedField()
    {
        var form = new Dictionary<string, string>
        {
            ["CustomerName"] = "ACME",
            ["Lines[0].ProductId"] = "00000000-0000-0000-0000-000000000001",
            ["Lines[0].ProductCode"] = "P-001",
            ["Lines[0].ProductName"] = "Laptop",
            ["Lines[0].Quantity"] = "2",
            ["Lines[0].UnitPrice"] = "1200.00",
            ["Lines[0].TaxRate"] = "15.00",
        };

        var response = await _client.PostAsync("/quote/edit", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = SampleAppFactory.BoundJson(await response.Content.ReadAsStringAsync());
        var line = json.RootElement.GetProperty("Lines")[0];
        Assert.Equal(2, line.GetProperty("Quantity").GetInt32());
        Assert.Equal(1200m, line.GetProperty("UnitPrice").GetDecimal());
        Assert.False(line.TryGetProperty("LineTotal", out _));
    }

    [Fact]
    public async Task ProductLookup_ReturnsPriceAndTaxRate()
    {
        var json = await _client.GetStringAsync("/products/lookup?term=lap");

        Assert.Contains("\"code\":\"P-001\"", json);
        Assert.Contains("\"price\":1200", json);
        Assert.Contains("\"taxRate\":15", json);
    }
}

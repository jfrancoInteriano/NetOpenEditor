using System.Net.Http;

namespace NetOpenEditor.Integration.Tests;

public sealed class QuoteSourceTests : IClassFixture<SampleAppFactory>
{
    private readonly SampleAppFactory _factory;
    public QuoteSourceTests(SampleAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_RendersLinesFromTheSource()
    {
        var html = await _factory.CreateClient().GetStringAsync("/quote/edit?id=Q-1");

        Assert.Contains("Q-1", html, StringComparison.Ordinal);
        Assert.Contains("data-noe-rows>[{", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_WithValidationErrors_ReRendersWhatTheUserTyped_NotTheSource()
    {
        var client = _factory.CreateClient();
        var form = new Dictionary<string, string>
        {
            ["Id"] = "Q-1",
            ["CustomerName"] = "",                       // inválido: fuerza el re-render
            ["Lines[0].ProductCode"] = "TECLEADO",
            ["Lines[0].Quantity"] = "3",
            ["Lines[0].UnitPrice"] = "10",
        };

        var response = await client.PostAsync("/quote/edit", new FormUrlEncodedContent(form));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("TECLEADO", html, StringComparison.Ordinal);
        Assert.DoesNotContain("desde-source", html, StringComparison.Ordinal);
    }
}

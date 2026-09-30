using System.Net;
using NetOpenEditor.Assets;

namespace NetOpenEditor.Integration.Tests;

public sealed class JournalPageTests : IClassFixture<SampleAppFactory>
{
    private readonly HttpClient _client;

    public JournalPageTests(SampleAppFactory factory) => _client = factory.CreateClient();

    private static Dictionary<string, string> ValidForm() => new()
    {
        ["EntryDate"] = "2026-09-21",
        ["Description"] = "Prueba",
        ["Lines[0].AccountId"] = "00000000-0000-0000-0000-000000001101",
        ["Lines[0].AccountCode"] = "1101",
        ["Lines[0].AccountName"] = "Caja",
        ["Lines[0].Description"] = "Apertura caja",
        ["Lines[0].DebitAmount"] = "100.50",
        ["Lines[0].CreditAmount"] = "0.00",
        ["Lines[1].AccountId"] = "00000000-0000-0000-0000-000000003101",
        ["Lines[1].AccountCode"] = "3101",
        ["Lines[1].AccountName"] = "Capital",
        ["Lines[1].Description"] = "Apertura capital",
        ["Lines[1].DebitAmount"] = "0.00",
        ["Lines[1].CreditAmount"] = "100.50",
    };

    [Fact]
    public async Task EditPage_RendersEditorShellWithRowsAndHeadAssets()
    {
        var html = await _client.GetStringAsync("/journal/edit");

        Assert.Contains("data-noe-id=\"journal-lines\"", html);
        Assert.Contains("x-data=\"netopenEditor('journal-lines')\"", html);
        Assert.Contains("data-noe-rows>", html);
        Assert.Contains("\"__labels\":{\"AccountId\":\"1101 - Caja\"}", html);
        Assert.Contains($"/_noe/netopeneditor.js?v={EmbeddedEditorAssets.Script.Version}", html);
        Assert.Contains("\"totals\":\"Totales\"", html);
    }

    [Fact]
    public async Task Post_BindsLinesByIndexFromFormFields()
    {
        var response = await _client.PostAsync("/journal/edit", new FormUrlEncodedContent(ValidForm()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = SampleAppFactory.BoundJson(await response.Content.ReadAsStringAsync());
        var lines = json.RootElement.GetProperty("Lines");
        Assert.Equal(2, lines.GetArrayLength());
        Assert.Equal("00000000-0000-0000-0000-000000001101", lines[0].GetProperty("AccountId").GetString());
        Assert.Equal(100.50m, lines[0].GetProperty("DebitAmount").GetDecimal());
        Assert.Equal("Apertura capital", lines[1].GetProperty("Description").GetString());
        Assert.Equal(100.50m, lines[1].GetProperty("CreditAmount").GetDecimal());
    }

    [Fact]
    public async Task Post_WithMissingAccountAndZeroAmounts_RendersErrorsPerLine()
    {
        var form = ValidForm();
        form["Lines[1].AccountId"] = "";
        form["Lines[1].CreditAmount"] = "0.00";

        var response = await _client.PostAsync("/journal/edit", new FormUrlEncodedContent(form));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-noe-errors>", html);
        Assert.Contains("\"Lines[1].AccountId\":[\"La cuenta es requerida\"]", html);
        Assert.Contains("\"Lines[1]\":[\"Indique d\\u00E9bito o cr\\u00E9dito\"]", html);
        Assert.Contains("\"Description\":\"Apertura capital\"", html);
    }

    [Fact]
    public async Task Lookup_FiltersByCodeOrName()
    {
        var json = await _client.GetStringAsync("/accounts/lookup?term=cap");

        Assert.Contains("\"code\":\"3101\"", json);
        Assert.Contains("\"display\":\"3101 - Capital\"", json);
        Assert.DoesNotContain("1101", json);
    }
}

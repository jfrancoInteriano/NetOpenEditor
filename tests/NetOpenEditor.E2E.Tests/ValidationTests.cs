using Microsoft.Playwright;
using static NetOpenEditor.E2E.Tests.SampleServerFixture;

namespace NetOpenEditor.E2E.Tests;

[Collection("sample")]
public sealed class ValidationTests(SampleServerFixture server)
{
    private Task<IPage> OpenAsync() => server.NewPageAsync("/journal/edit");

    [Fact]
    public async Task ServerRowError_IsShownOnTheRowAfterFailedPost()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(0, "DebitAmount")).FillAsync("0");
        await page.Keyboard.PressAsync("Tab");
        await page.Locator("#save").ClickAsync();
        await page.WaitForSelectorAsync("[data-noe-row='0'].noe-row-invalid");

        Assert.Equal("Indique débito o crédito", await page.Locator("[data-noe-row='0'] .noe-row-error").GetAttributeAsync("title"));
        Assert.Equal("Apertura caja", await page.Locator(Cell(0, "Description")).InputValueAsync());
        Assert.Equal("1101 - Caja", await page.Locator(Cell(0, "AccountId")).InputValueAsync());
        Assert.DoesNotContain("noe-row-invalid", await page.Locator("[data-noe-row='1']").GetAttributeAsync("class"));
        Assert.Equal(3, await page.Locator("[data-noe-row]").CountAsync());
    }

    [Fact]
    public async Task ServerCellError_IsShownUnderTheCell_AndClearsOnEdit()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(1, "AccountId")).FillAsync("");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(250);
        // Bypass client validation to reach the server: call the form's native submit.
        await page.EvaluateAsync("() => document.getElementById('entryForm').submit()");
        await page.WaitForSelectorAsync("[data-noe-row='1'] .noe-error:visible");

        Assert.Equal("La cuenta es requerida", await page.Locator("[data-noe-row='1'] .noe-error:visible").InnerTextAsync());
        Assert.Contains("noe-invalid", await page.Locator(Cell(1, "AccountId")).GetAttributeAsync("class"));

        await page.Locator(Cell(1, "AccountId")).FocusAsync();
        await page.Keyboard.TypeAsync("caj");
        await page.WaitForSelectorAsync(".noe-lookup-item.is-active");
        await page.Keyboard.PressAsync("Enter");

        Assert.Equal(0, await page.Locator("[data-noe-row='1'] .noe-error:visible").CountAsync());
        Assert.DoesNotContain("noe-row-invalid", await page.Locator("[data-noe-row='1']").GetAttributeAsync("class"));
    }

    [Fact]
    public async Task ClientRequired_BlocksSubmitAndFocusesFirstInvalidCell()
    {
        var page = await OpenAsync();

        await page.Locator(Cell(2, "Description")).FillAsync("Sin cuenta");   // promoted, AccountId empty
        await page.Locator("#save").ClickAsync();
        await page.WaitForTimeoutAsync(300);

        Assert.EndsWith("/journal/edit", page.Url);
        Assert.Equal("Requerido", await page.Locator("[data-noe-row='2'] .noe-error:visible").InnerTextAsync());
        Assert.Equal("2:AccountId", await ActiveCellAsync(page));
    }

    [Fact]
    public async Task MinRows_BlocksSubmitWithFormError()
    {
        var page = await OpenAsync();

        await page.Locator("[data-noe-row='0'] .noe-btn-remove").ClickAsync();
        await page.Locator("[data-noe-row='0'] .noe-btn-remove").ClickAsync();
        Assert.Equal(1, await page.Locator("[data-noe-row]").CountAsync());

        await page.Locator("#save").ClickAsync();
        await page.WaitForTimeoutAsync(300);

        Assert.EndsWith("/journal/edit", page.Url);
        Assert.Equal("Se requiere al menos 1 línea(s)", await page.Locator(".noe-form-error").InnerTextAsync());
    }

    [Fact]
    public async Task ValidateApi_ReturnsFalseAndMarksErrorsWithoutSubmitting()
    {
        var page = await OpenAsync();
        await page.Locator(Cell(2, "Description")).FillAsync("x");

        var ok = await page.EvaluateAsync<bool>("() => NetOpenEditor.get('journal-lines').validate()");

        Assert.False(ok);
        await page.WaitForTimeoutAsync(300);
        Assert.Equal(1, await page.Locator("[data-noe-row='2'] .noe-error:visible").CountAsync());
    }

    [Fact]
    public async Task ServerCellError_ExposesAriaInvalidAndDescribedBy()
    {
        var page = await OpenAsync();

        // Same scenario as the cell-error test: clear a required lookup and submit natively.
        await page.Locator(Cell(1, "AccountId")).FillAsync("");
        await page.Keyboard.PressAsync("Tab");
        await page.WaitForTimeoutAsync(250);
        await page.EvaluateAsync("() => document.getElementById('entryForm').submit()");
        await page.WaitForSelectorAsync("[data-noe-row='1'] .noe-error:visible");

        var cell = page.Locator("[data-noe-row='1'] input[aria-invalid='true']").First;
        var describedBy = await cell.GetAttributeAsync("aria-describedby");

        Assert.False(string.IsNullOrWhiteSpace(describedBy));
        Assert.NotEmpty(await page.Locator("#" + describedBy).InnerTextAsync());
    }
}

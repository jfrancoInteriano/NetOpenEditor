using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace NetOpenEditor.Integration.Tests;

public sealed class SampleAppFactory : WebApplicationFactory<Program>
{
    /// <summary>Extracts and parses the JSON echoed by Views/Shared/Result.cshtml.</summary>
    public static JsonDocument BoundJson(string html)
    {
        var match = Regex.Match(html, "<pre id=\"bound-json\">(.*?)</pre>", RegexOptions.Singleline);
        Assert.True(match.Success, "Result page did not contain #bound-json");
        return JsonDocument.Parse(WebUtility.HtmlDecode(match.Groups[1].Value));
    }
}

using Microsoft.AspNetCore.Mvc;

namespace NetOpenEditor.Example.Controllers;

public sealed class HomeController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View();
}

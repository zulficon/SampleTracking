using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SampleAnalysisTracking.Controllers;


[AllowAnonymous]
public sealed class HomeController : Controller
{
    public IActionResult Index() => View();
}

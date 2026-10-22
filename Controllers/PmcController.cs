using inzynierka.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace inzynierka.Controllers;

[Authorize]
public class PmcController : Controller
{
    private readonly IPmcService _pmcService;
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public PmcController(IPmcService pmcService)
    {
        _pmcService = pmcService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetData(int days = 90)
    {
        var data = await _pmcService.GetPmcDataAsync(CurrentUserId, days);
        return Json(data);
    }

    [HttpPost]
    public async Task<IActionResult> Recalculate()
    {
        await _pmcService.RecalculatePmcAsync(CurrentUserId);
        return Ok();
    }
}
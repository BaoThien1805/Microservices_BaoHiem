using MediatR;
using Microsoft.AspNetCore.Mvc;
using PricingService.Api.Commands;
namespace PricingService.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PricingController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CalculatePriceCommand command)
    {
        var result = await mediator.Send(command, HttpContext?.RequestAborted ?? CancellationToken.None);
        return new JsonResult(result);
    }
}

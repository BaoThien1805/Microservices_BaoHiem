using System;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PolicyService.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
public class ErrorController : ControllerBase
{
    [Route("/error")]
    public IActionResult Error()
    {
        var exception = HttpContext.Features.Get<IExceptionHandlerFeature>().Error;
        return exception switch
        {
            KeyNotFoundException => Problem(statusCode: 404, title: "Offer or policy not found"),
            ApplicationException => Problem(statusCode: 409, title: "Business rules violation", detail: exception.Message),
            ArgumentException => Problem(statusCode: 400, title: "Invalid request"),
            _ => Problem()
        };
    }
}
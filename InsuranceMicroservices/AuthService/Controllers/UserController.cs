using AuthService.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly Domain.AuthService authService;

    public UserController(Domain.AuthService authService)
    {
        this.authService = authService;
    }

    [AllowAnonymous]
    [HttpPost]
    public IActionResult Post([FromBody] AuthRequest user)
    {
        var authResult = authService.Authenticate(user.Login, user.Password);

        if (authResult == null)
            return Unauthorized(new { message = "Username or password incorrect" });

        return Ok(new
        {
            Token = authResult.Token,
            UserName = authResult.Login,
            Roles = authResult.Roles,
            Avatar = authResult.Avatar,
            UserType = authResult.UserType,
            ExpiryTimeStamp = authResult.ExpiryTimeStamp
        });
    }

    [HttpGet]
    public IActionResult Get()
    {
        var login = User.Identity?.Name;
        if (string.IsNullOrEmpty(login)) return Unauthorized();
        var agent = authService.AgentFromLogin(login);
        return agent is null ? NotFound() : Ok(agent);
    }
}
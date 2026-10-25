using System.ComponentModel.DataAnnotations;
namespace AuthService.Domain;
public class AuthRequest
{
    [Required, StringLength(100)]
    public string Login { get; set; } = "";
    [Required, StringLength(256)]
    public string Password { get; set; } = "";
}

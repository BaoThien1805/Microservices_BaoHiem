using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;

namespace AuthService.Domain;

public class InsuranceAgent
{
    public InsuranceAgent(string login, string password, string avatar, List<string> availableProducts)
    {
        Login = login;
        Password = new PasswordHasher<InsuranceAgent>().HashPassword(this, password);
        Avatar = avatar;
        AvailableProducts = availableProducts;
    }

    public string Login { get; }
    [JsonIgnore]
    public string Password { get; }
    public string Avatar { get; }
    public List<string> AvailableProducts { get; }

    public bool PasswordMatches(string passwordToTest)
    {
        return new PasswordHasher<InsuranceAgent>().VerifyHashedPassword(this, Password, passwordToTest) != PasswordVerificationResult.Failed;
    }
}
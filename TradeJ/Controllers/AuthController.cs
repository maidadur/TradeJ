using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace TradeJ.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(IConfiguration config) : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var adminPassword = config["APP_PASSWORD"] ?? "admin";
        if (request.Username != "admin" || request.Password != adminPassword)
            return Unauthorized(new { message = "Invalid credentials" });

        var key = Encoding.UTF8.GetBytes(
            config["Jwt:Key"] ?? "dev-only-insecure-key-change-in-production-32ch");

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")]),
            Expires = DateTime.UtcNow.AddDays(config.GetValue<int>("Jwt:ExpiryDays", 30)),
            Issuer = config["Jwt:Issuer"] ?? "tradej",
            Audience = config["Jwt:Audience"] ?? "tradej",
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return Ok(new { token = handler.WriteToken(token) });
    }
}

public record LoginRequest(string Username, string Password);

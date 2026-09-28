using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JobAppTrackerApi.Models;
using Microsoft.IdentityModel.Tokens;

namespace JobAppTrackerApi.Services
{
    public class TokenService(IConfiguration config)
    {
        public (string Token, DateTime ExpiresAt) CreateToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(int.Parse(config["Jwt:ExpiryMinutes"]!));

            var claims = new[]
            {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email)
        };

            var token = new JwtSecurityToken(
                issuer: config["Jwt:Issuer"],
                audience: config["Jwt:Audience"],
                claims: claims,
                signingCredentials: credentials,
                expires: expires);

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }
    }
}

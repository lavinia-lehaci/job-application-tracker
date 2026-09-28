using JobAppTrackerApi.Data;
using JobAppTrackerApi.DTOs;
using JobAppTrackerApi.Models;
using JobAppTrackerApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobAppTrackerApi.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
    {
        private readonly PasswordHasher<User> _hasher = new();

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            if (await db.Users.AnyAsync(u => u.Email == email))
                return Conflict(new { message = "Email already registered." });

            var user = new User { Email = email };
            user.Password = _hasher.HashPassword(user, request.Password);

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created);
        } 
    
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user is null ||
                _hasher.VerifyHashedPassword(user, user.Password, request.Password) == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            var (token, expiresAt) = tokens.CreateToken(user);
            return new AuthResponse(token, expiresAt);
        }
    }
}
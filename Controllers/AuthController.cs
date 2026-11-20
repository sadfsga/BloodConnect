using BloodConnect.Api.Data;
using BloodConnect.Api.Dtos;
using BloodConnect.Api.Models;
using BloodConnect.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BloodConnect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;

    public AuthController(AppDbContext db, TokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "This email is already registered." });

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            // BCrypt adds a random salt and is slow on purpose, so stolen hashes are hard to crack.
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Phone = dto.Phone.Trim(),
            City = dto.City.Trim(),
            BloodType = dto.BloodType,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(_tokens.CreateAuthResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Same message for "no such email" and "wrong password", so attackers learn nothing.
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        return Ok(_tokens.CreateAuthResponse(user));
    }
}

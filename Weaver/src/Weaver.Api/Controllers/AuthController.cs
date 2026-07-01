using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Weaver.Domain;
using Weaver.Infrastructure.Auth;
using Weaver.Infrastructure.Persistence;

namespace Weaver.Api.Controllers;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, Guid UserId, string Email);

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly WeaverDbContext _db;
    private readonly IPasswordHashingService _passwordHashing;
    private readonly ITokenService _tokenService;

    public AuthController(WeaverDbContext db, IPasswordHashingService passwordHashing, ITokenService tokenService)
    {
        _db = db;
        _passwordHashing = passwordHashing;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Email and password are required.");
        }

        if (request.Password.Length < 8)
        {
            return BadRequest("Password must be at least 8 characters.");
        }

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            return Conflict("An account with that email already exists.");
        }

        var user = new User { Email = email };
        user.PasswordHash = _passwordHashing.Hash(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(_tokenService.CreateToken(user), user.Id, user.Email);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !_passwordHashing.Verify(user, request.Password))
        {
            return Unauthorized("Invalid email or password.");
        }

        return new AuthResponse(_tokenService.CreateToken(user), user.Id, user.Email);
    }
}

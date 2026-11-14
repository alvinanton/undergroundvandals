using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Api.DTOs;
using UndergroundVandals.Api.Entities;

namespace UndergroundVandals.Api.Controllers;

[Tags("Users")]
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")] // Restrict entire controller to Admin role only
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets a list of all registered users.
    /// </summary>
    [HttpGet]
    [EndpointSummary("List users")]
    [EndpointDescription("Admin only. Returns all users ordered by creation date descending, without passwords or pagination.")]
    [ProducesResponseType(typeof(IEnumerable<UserResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new UserResponseDto(
                u.Id,
                u.Username,
                u.Email,
                u.Role.ToString(),
                u.IsActive,
                u.CreatedAt,
                u.LastLoginAt
            ))
            .ToListAsync();

        return Ok(users);
    }

    /// <summary>
    /// Creates a new user (Editor or Admin).
    /// </summary>
    [HttpPost]
    [EndpointSummary("Create user")]
    [EndpointDescription("Admin only. Creates an active account. The default role is Editor. Rejects email addresses that are already registered.")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest, Description = "Automatic validation error (ValidationProblemDetails) or a controller error with a message field.")]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
        {
            return BadRequest(new ApiMessageResponseDto("Email is already registered."));
        }

        var newUser = new User
        {
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = dto.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        var response = new UserResponseDto(
            newUser.Id,
            newUser.Username,
            newUser.Email,
            newUser.Role.ToString(),
            newUser.IsActive,
            newUser.CreatedAt,
            newUser.LastLoginAt
        );

        return CreatedAtAction(nameof(GetAllUsers), new { id = newUser.Id }, response);
    }

    /// <summary>
    /// Assigns active status for a specific user (activation/deactivation).
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [EndpointSummary("Set user status")]
    [EndpointDescription("Admin only. Sets isActive to the supplied value rather than toggling it. Does not revoke previously issued JWTs.")]
    [ProducesResponseType(typeof(ApiMessageResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiMessageResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleUserStatus(Guid id, [FromBody] ToggleUserStatusDto dto)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(new ApiMessageResponseDto("User not found."));
        }

        user.IsActive = dto.IsActive;
        await _context.SaveChangesAsync();

        return Ok(new ApiMessageResponseDto($"User status successfully updated to {(user.IsActive ? "Active" : "Inactive")}."));
    }
}

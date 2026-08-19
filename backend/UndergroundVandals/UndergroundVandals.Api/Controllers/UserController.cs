using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Api.DTOs;
using UndergroundVandals.Api.Entities;

namespace UndergroundVandals.Api.Controllers;

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
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
        {
            return BadRequest(new { message = "Email is already registered." });
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
    /// Toggles active status for a specific user (activation/deactivation).
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> ToggleUserStatus(Guid id, [FromBody] ToggleUserStatusDto dto)
    {
        var user = await _context.Users.FindAsync(id);

        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        user.IsActive = dto.IsActive;
        await _context.SaveChangesAsync();

        return Ok(new { message = $"User status successfully updated to {(user.IsActive ? "Active" : "Inactive")}." });
    }
}
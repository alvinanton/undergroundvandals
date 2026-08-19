using UndergroundVandals.Api.Entities;

namespace UndergroundVandals.Api.DTOs;

public record CreateUserDto(
    string Username,
    string Email,
    string Password,
    UserRole Role = UserRole.Editor
);

public record UserResponseDto(
    Guid Id,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

public record ToggleUserStatusDto(
    bool IsActive
);
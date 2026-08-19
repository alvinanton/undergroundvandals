namespace UndergroundVandals.Api.DTOs;

public record LoginDto(string Email, string Password);

public record AuthResponseDto(string Token, string Username, string Role);
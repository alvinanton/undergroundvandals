using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Api.Entities;

namespace UndergroundVandals.Api.Services;

public sealed class AdminBootstrapService(AppDbContext context)
{
    public static void Validate(string username, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 100)
            throw new ArgumentException("Username must contain between 1 and 100 characters.");

        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 254 ||
            !new EmailAddressAttribute().IsValid(email.Trim()))
            throw new ArgumentException("A valid email address is required.");

        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || Encoding.UTF8.GetByteCount(password) > 72)
            throw new ArgumentException("Password must contain at least 12 characters and no more than 72 UTF-8 bytes.");
    }

    public async Task CreateAsync(string username, string email, string password, CancellationToken cancellationToken = default)
    {
        Validate(username, email, password);
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("LOCK TABLE \"Users\" IN EXCLUSIVE MODE", cancellationToken);

        if (await context.Users.AnyAsync(user => user.Role == UserRole.Admin, cancellationToken))
            throw new InvalidOperationException("An administrator already exists, including possibly an inactive account. No changes were made.");

        if (await context.Users.AnyAsync(user => user.Email.ToLower() == normalizedEmail, cancellationToken))
            throw new InvalidOperationException("The email address already belongs to a user. No changes were made.");

        context.Users.Add(new User
        {
            Username = username.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

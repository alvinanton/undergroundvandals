using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Api.Services;

namespace UndergroundVandals.Admin;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 5 || args[0] != "bootstrap-admin" || args[1] != "--email" || args[3] != "--username")
        {
            Console.Error.WriteLine("Usage: bootstrap-admin --email <email> --username <username>");
            return 2;
        }

        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("An interactive terminal is required. Password input cannot be redirected.");
            return 2;
        }

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("Set ConnectionStrings__DefaultConnection to the intended PostgreSQL database.");
            return 2;
        }

        try
        {
            var password = ReadPassword("Password: ");
            var confirmation = ReadPassword("Confirm password: ");
            if (password != confirmation)
                throw new ArgumentException("Passwords do not match.");

            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await using var context = new AppDbContext(options);
            await new AdminBootstrapService(context).CreateAsync(args[4], args[2], password);
            Console.WriteLine("Initial administrator created. The API was not started.");
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Initialization failed. Verify database connectivity, permissions, and applied migrations. No credentials are printed.");
            return 1;
        }
    }

    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var characters = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return new string(characters.ToArray());
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (characters.Count > 0)
                    characters.RemoveAt(characters.Count - 1);
            }
            else if (!char.IsControl(key.KeyChar))
            {
                characters.Add(key.KeyChar);
            }
        }
    }
}

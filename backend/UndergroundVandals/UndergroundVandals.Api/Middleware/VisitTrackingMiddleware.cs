using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Api.Entities;

namespace UndergroundVandals.Api.Middleware
{
    public class VisitTrackingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServiceScopeFactory _scopeFactory;

        public VisitTrackingMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
        {
            _next = next;
            _scopeFactory = scopeFactory;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            if (HttpMethods.IsGet(context.Request.Method) && context.Response.StatusCode == 200)
            {
                var path = context.Request.Path.Value?.ToLower() ?? "/";

                // TODO: Filter out static file extensions if serving assets from this API
                // if (path.EndsWith(".css") || path.EndsWith(".js") || path.EndsWith(".png") || path.EndsWith(".ico")) return;

                var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "127.0.0.1";

                var userAgent = context.Request.Headers.UserAgent.ToString();
                var countryCode = context.Request.Headers["CF-IPCountry"].FirstOrDefault()
                    ?? context.Request.Headers["X-AppService-Country"].FirstOrDefault()
                    ?? "XX";

                _ = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var visit = new WebsiteVisit
                    {
                        Path = path,
                        CountryCode = countryCode.ToUpper(),
                        UserHash = ComputeSha256($"{ipAddress}_{userAgent}"),
                        CreatedAt = DateTime.UtcNow
                    };

                    dbContext.WebsiteVisits.Add(visit);
                    await dbContext.SaveChangesAsync();
                });
            }
        }

        private static string ComputeSha256(string rawData)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToHexString(bytes);
        }

    }
}

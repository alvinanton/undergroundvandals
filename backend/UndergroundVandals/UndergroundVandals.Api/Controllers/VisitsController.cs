using Microsoft.AspNetCore.Mvc;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Api.DTOs;
using UndergroundVandals.Api.Entities;
using System.Security.Cryptography;
using System.Text;

namespace UndergroundVandals.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VisitsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public VisitsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("track")]
        public async Task<IActionResult> TrackVisit([FromBody] TrackVisitDto dto)
        {
            var ipAddress = Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "127.0.0.1";

            var userAgent = Request.Headers.UserAgent.ToString();

            var countryCode = Request.Headers["CF-IPCountry"].FirstOrDefault()
                ?? Request.Headers["X-AppService-Country"].FirstOrDefault()
                ?? "XX";

            var region = Request.Headers["CF-RegionCode"].FirstOrDefault();
            var city = Request.Headers["CF-IPCity"].FirstOrDefault();

            var visit = new WebsiteVisit
            {
                Path = dto.Path?.ToLower() ?? "/",
                CountryCode = countryCode.ToUpper(),
                Region = region,
                City = city,
                UserHash = ComputeSha256($"{ipAddress}_{userAgent}"),
                DeviceType = string.IsNullOrWhiteSpace(dto.DeviceType) ? "Unknown" : dto.DeviceType,
                Referrer = dto.Referrer,
                CreatedAt = DateTime.UtcNow
            };

            _context.WebsiteVisits.Add(visit);
            await _context.SaveChangesAsync();

            return Ok();
        }

        private static string ComputeSha256(string rawData)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToHexString(bytes);
        }
    }
}
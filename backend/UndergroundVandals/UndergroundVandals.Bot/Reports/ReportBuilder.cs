using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using UndergroundVandals.Api.Data;
using UndergroundVandals.Bot.Commands;

namespace UndergroundVandals.Bot.Reports;

public sealed class ReportBuilder(AppDbContext db) : IReportBuilder
{
    private static readonly char[] SparkChars = ['▁', '▂', '▃', '▄', '▅', '▆', '▇', '█'];

    public async Task<string> BuildAsync(ReportKind kind, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;

        DateTime from, to, prevFrom, prevTo;
        string title, subtitle;

        if (kind == ReportKind.Today)
        {
            from = todayStart;
            to = now;
            // Same elapsed time yesterday, so the comparison is fair
            prevFrom = todayStart.AddDays(-1);
            prevTo = now.AddDays(-1);
            title = "Today so far";
            subtitle = $"{from:yyyy-MM-dd} 00:00 → {now:HH:mm} UTC";
        }
        else
        {
            from = todayStart.AddDays(-1);
            to = todayStart;
            prevFrom = todayStart.AddDays(-2);
            prevTo = from;
            title = "Daily Traffic Report";
            subtitle = $"{from:yyyy-MM-dd} (UTC)";
        }

        var all = db.WebsiteVisits.AsNoTracking();
        var current = all.Where(v => v.CreatedAt >= from && v.CreatedAt < to);
        var previous = all.Where(v => v.CreatedAt >= prevFrom && v.CreatedAt < prevTo);

        var visits = await current.CountAsync(ct);
        var unique = await current.Select(v => v.UserHash).Distinct().CountAsync(ct);
        var prevVisits = await previous.CountAsync(ct);
        var prevUnique = await previous.Select(v => v.UserHash).Distinct().CountAsync(ct);

        var topCountries = await current
            .GroupBy(v => v.CountryCode)
            .Select(g => new { Country = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Country)
            .Take(5)
            .ToListAsync(ct);

        var topPages = await current
            .GroupBy(v => v.Path)
            .Select(g => new { Path = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Path)
            .Take(5)
            .ToListAsync(ct);

        var pagesPerVisitor = unique > 0 ? (double)visits / unique : 0;

        var sb = new StringBuilder();
        sb.AppendLine($"📊 <b>{title} - Underground Vandals</b>");
        sb.AppendLine($"<i>{subtitle}</i>\n");

        sb.AppendLine($"👁️ <b>Visits:</b> {visits} {Trend(visits, prevVisits)}");
        sb.AppendLine($"👤 <b>Unique visitors:</b> {unique} {Trend(unique, prevUnique)}");
        sb.AppendLine($"📑 <b>Pages/visitor:</b> {pagesPerVisitor:0.0}\n");

        if (kind == ReportKind.Today)
            await AppendHourlyAsync(sb, from, to, ct);
        else
            await AppendWeekAsync(sb, from, to, ct);

        sb.AppendLine("📄 <b>Top pages:</b>");
        if (topPages.Count > 0)
        {
            foreach (var p in topPages)
                sb.AppendLine($" • <code>{WebUtility.HtmlEncode(p.Path)}</code>: {p.Count}");
        }
        else
        {
            sb.AppendLine(" <i>No data.</i>");
        }

        sb.AppendLine("\n🌍 <b>Top countries:</b>");
        if (topCountries.Count > 0)
        {
            foreach (var c in topCountries)
            {
                var country = c.Country == "XX" ? "Unknown" : c.Country;
                sb.AppendLine($" • <code>{WebUtility.HtmlEncode(country)}</code>: {c.Count}");
            }
        }
        else
        {
            sb.AppendLine(" <i>No data.</i>");
        }

        return sb.ToString();
    }

    /// <summary>Visits per hour of the current day (used by /today).</summary>
    private async Task AppendHourlyAsync(StringBuilder sb, DateTime from, DateTime to, CancellationToken ct)
    {
        var perHour = await db.WebsiteVisits.AsNoTracking()
            .Where(v => v.CreatedAt >= from && v.CreatedAt < to)
            .GroupBy(v => v.CreatedAt.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var counts = Enumerable.Range(0, 24)
            .Select(h => perHour.FirstOrDefault(x => x.Hour == h)?.Count ?? 0)
            .ToArray();

        sb.AppendLine("🕒 <b>Activity by hour (UTC)</b>");
        sb.AppendLine($"<code>{Sparkline(counts)}</code>");
        sb.AppendLine("<code>0     6     12    18</code>");

        var peak = counts.Max();
        if (peak > 0)
            sb.AppendLine($" • Peak: {Array.IndexOf(counts, peak):00}:00 ({peak} visits)");

        sb.AppendLine();
    }

    /// <summary>Last 7 full days plus all-time total (used by /report).</summary>
    private async Task AppendWeekAsync(StringBuilder sb, DateTime from, DateTime to, CancellationToken ct)
    {
        var all = db.WebsiteVisits.AsNoTracking();
        var weekStart = from.Date.AddDays(-6);
        var week = all.Where(v => v.CreatedAt >= weekStart && v.CreatedAt < to);

        var weekVisits = await week.CountAsync(ct);
        var weekUnique = await week.Select(v => v.UserHash).Distinct().CountAsync(ct);
        var allTime = await all.CountAsync(ct);

        var perDay = await week
            .GroupBy(v => v.CreatedAt.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var dailyCounts = Enumerable.Range(0, 7)
            .Select(i => weekStart.AddDays(i).Date)
            .Select(d => perDay.FirstOrDefault(x => x.Day.Date == d)?.Count ?? 0)
            .ToArray();

        var labels = Enumerable.Range(0, 7)
            .Select(i => weekStart.AddDays(i).ToString("ddd", CultureInfo.InvariantCulture)[0]);

        sb.AppendLine("📅 <b>Last 7 days</b>");
        sb.AppendLine($"<code>{Sparkline(dailyCounts)}</code>");
        sb.AppendLine($"<code>{string.Concat(labels)}</code>  ({string.Join(" ", dailyCounts)})");
        sb.AppendLine($" • Visits: {weekVisits}");
        sb.AppendLine($" • Unique: {weekUnique}");
        sb.AppendLine($" • Daily avg: {weekVisits / 7.0:0.#}");
        sb.AppendLine($" • All time: {allTime}\n");
    }

    private static string Trend(int current, int previous)
    {
        if (previous == 0) return current == 0 ? "" : "🆕";
        var pct = (current - previous) * 100.0 / previous;
        var icon = pct > 0 ? "📈" : pct < 0 ? "📉" : "➖";
        return $"{icon} {pct:+0;-0;0}%";
    }

    private static string Sparkline(int[] values)
    {
        var max = values.Max();
        if (max == 0) return new string(SparkChars[0], values.Length);

        return new string(values
            .Select(v => SparkChars[(int)Math.Round(v * (SparkChars.Length - 1) / (double)max)])
            .ToArray());
    }
}
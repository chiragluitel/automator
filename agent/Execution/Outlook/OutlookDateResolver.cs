namespace AutoFlow.Agent.Execution.Outlook;

/// <summary>
/// Resolves date strings — both ISO-8601 ("2026-06-01") and natural-language shorthand
/// ("yesterday", "last_week", "last_30_days", etc.) — into concrete DateTime ranges.
/// All resolution is relative to the moment of execution, so "yesterday" always means
/// the correct calendar day even if the IR was compiled the day before.
/// </summary>
public static class OutlookDateResolver
{
    /// <summary>
    /// Resolves the date filtering params on a step into an inclusive [from, to] window.
    /// Precedence (highest to lowest):
    ///   1. date_range named shorthand ("yesterday", "last_week", …)
    ///   2. Explicit date_from / date_to ISO strings
    ///   3. last_minutes / last_hours / last_days integer offsets
    /// Returns (null, null) if no date params are present.
    /// </summary>
    public static (DateTime? From, DateTime? To) Resolve(
        string? dateRange,
        string? dateFrom,
        string? dateTo,
        string? lastMinutes,
        string? lastHours,
        string? lastDays)
    {
        var now = DateTime.Now;

        // 1. Named shorthand
        if (!string.IsNullOrWhiteSpace(dateRange))
        {
            var (f, t) = ResolveNamedRange(dateRange.Trim().ToLowerInvariant(), now);
            return (f, t);
        }

        // 2. Explicit ISO boundaries
        if (!string.IsNullOrWhiteSpace(dateFrom) || !string.IsNullOrWhiteSpace(dateTo))
        {
            var from = string.IsNullOrWhiteSpace(dateFrom) ? (DateTime?)null : ParseIso(dateFrom!);
            var to   = string.IsNullOrWhiteSpace(dateTo)   ? (DateTime?)null : ParseIso(dateTo!).AddDays(1).AddSeconds(-1);
            return (from, to);
        }

        // 3. Relative integer offsets (last_minutes takes priority over last_hours over last_days)
        if (int.TryParse(lastMinutes, out var mins) && mins > 0)
            return (now.AddMinutes(-mins), null);

        if (int.TryParse(lastHours, out var hrs) && hrs > 0)
            return (now.AddHours(-hrs), null);

        if (int.TryParse(lastDays, out var days) && days > 0)
            return (now.AddDays(-days), null);

        return (null, null);
    }

    // ── Named ranges ─────────────────────────────────────────────────────────

    private static (DateTime? From, DateTime? To) ResolveNamedRange(string name, DateTime now) =>
        name switch
        {
            "today"         => (now.Date,                       now),
            "yesterday"     => (now.Date.AddDays(-1),           now.Date.AddSeconds(-1)),
            "this_week"     => (StartOfWeek(now),               now),
            "last_week"     => (StartOfWeek(now).AddDays(-7),   StartOfWeek(now).AddSeconds(-1)),
            "this_month"    => (new DateTime(now.Year, now.Month, 1), now),
            "last_month"    => ResolveLastMonth(now),
            "this_year"     => (new DateTime(now.Year, 1, 1),   now),
            "last_year"     => (new DateTime(now.Year - 1, 1, 1), new DateTime(now.Year, 1, 1).AddSeconds(-1)),
            "last_7_days"   => (now.AddDays(-7),                now),
            "last_14_days"  => (now.AddDays(-14),               now),
            "last_30_days"  => (now.AddDays(-30),               now),
            "last_60_days"  => (now.AddDays(-60),               now),
            "last_90_days"  => (now.AddDays(-90),               now),
            "last_6_months" => (now.AddMonths(-6),              now),
            _ => throw new InvalidOperationException(
                $"Unrecognised date_range '{name}'. " +
                $"Supported values: today, yesterday, this_week, last_week, this_month, last_month, " +
                $"this_year, last_year, last_7_days, last_14_days, last_30_days, last_60_days, " +
                $"last_90_days, last_6_months.")
        };

    private static (DateTime? From, DateTime? To) ResolveLastMonth(DateTime now)
    {
        var firstOfThisMonth = new DateTime(now.Year, now.Month, 1);
        var firstOfLastMonth = firstOfThisMonth.AddMonths(-1);
        return (firstOfLastMonth, firstOfThisMonth.AddSeconds(-1));
    }

    private static DateTime StartOfWeek(DateTime dt)
    {
        // ISO week: Monday is the start of the week.
        var diff = (7 + (dt.DayOfWeek - DayOfWeek.Monday)) % 7;
        return dt.Date.AddDays(-diff);
    }

    // ── ISO parsing ───────────────────────────────────────────────────────────

    /// <summary>
    /// Parses a date string in ISO-8601 format ("2026-06-01") or common US format ("06/01/2026").
    /// </summary>
    public static DateTime ParseIso(string raw)
    {
        // Try ISO date-only (most common from Claude)
        if (DateTime.TryParseExact(raw.Trim(), "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
            return dt;

        // Try ISO datetime with offset
        if (DateTimeOffset.TryParse(raw.Trim(), out var dto))
            return dto.LocalDateTime;

        throw new FormatException(
            $"Cannot parse date '{raw}'. Use ISO format: YYYY-MM-DD (e.g. 2026-06-01).");
    }
}

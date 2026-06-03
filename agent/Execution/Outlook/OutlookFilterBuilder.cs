using AutoFlow.Agent.Models;

namespace AutoFlow.Agent.Execution.Outlook;

/// <summary>
/// Builds Outlook COM Restrict() filter strings (JET query syntax) from IR step params,
/// and provides in-memory predicate matching for filters that JET cannot express
/// (partial string matches on subject, sender, body, etc.).
///
/// Strategy:
///   JET filter  — date ranges, read/unread, importance, flag status, has-attachment.
///                 Applied first via Items.Restrict() for server-side filtering.
///   In-memory   — subject_contains, subject (exact), from, to, cc, body_contains, category.
///                 Applied after Restrict() to refine the result set.
///
/// All JET date strings use the en-US MM/dd/yyyy HH:mm format required by Outlook's MAPI layer.
/// </summary>
public static class OutlookFilterBuilder
{
    // ── JET filter construction ───────────────────────────────────────────────

    /// <summary>
    /// Builds a JET filter string for Items.Restrict().
    /// Returns an empty string if no JET-compatible filters are present
    /// (caller should skip Restrict() and iterate all items).
    /// </summary>
    public static string BuildJetFilter(IrStep step)
    {
        var p = step.Params;
        var clauses = new List<string>();

        // ── Date range ────────────────────────────────────────────────────────
        var (dateFrom, dateTo) = OutlookDateResolver.Resolve(
            dateRange:   GetParam(p, "date_range"),
            dateFrom:    GetParam(p, "date_from"),
            dateTo:      GetParam(p, "date_to"),
            lastMinutes: GetParam(p, "last_minutes"),
            lastHours:   GetParam(p, "last_hours"),
            lastDays:    GetParam(p, "last_days"));

        if (dateFrom.HasValue)
            clauses.Add($"[ReceivedTime] >= '{dateFrom.Value:MM/dd/yyyy HH:mm}'");
        if (dateTo.HasValue)
            clauses.Add($"[ReceivedTime] <= '{dateTo.Value:MM/dd/yyyy HH:mm}'");

        // ── Read / Unread ─────────────────────────────────────────────────────
        var isUnread = GetParam(p, "is_unread");
        if (isUnread is not null)
        {
            if (IsTrue(isUnread))  clauses.Add("[UnRead] = True");
            if (IsFalse(isUnread)) clauses.Add("[UnRead] = False");
        }

        // ── Flag status ───────────────────────────────────────────────────────
        var isFlagged = GetParam(p, "is_flagged");
        if (isFlagged is not null)
        {
            if (IsTrue(isFlagged))  clauses.Add("[FlagStatus] = 2");
            if (IsFalse(isFlagged)) clauses.Add("[FlagStatus] = 0");
        }

        // ── Importance ────────────────────────────────────────────────────────
        var importance = GetParam(p, "importance");
        if (importance is not null)
        {
            var val = importance.ToLowerInvariant() switch
            {
                "high" => 2, "low" => 0, _ => 1
            };
            clauses.Add($"[Importance] = {val}");
        }

        // ── Has attachment ────────────────────────────────────────────────────
        var hasAtt = GetParam(p, "has_attachment");
        if (hasAtt is not null)
        {
            if (IsTrue(hasAtt))  clauses.Add("[Attachments.Count] > 0");
            if (IsFalse(hasAtt)) clauses.Add("[Attachments.Count] = 0");
        }

        return clauses.Count == 0
            ? string.Empty
            : string.Join(" AND ", clauses);
    }

    // ── In-memory predicate ───────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the given COM MailItem passes all in-memory filters defined in the step params.
    /// Called for every item that survives the JET Restrict() pass.
    /// </summary>
    public static bool MatchesInMemoryFilters(dynamic mail, IrStep step)
    {
        var p = step.Params;

        // ── From (sender email or display name, partial) ───────────────────────
        var from = GetParam(p, "from");
        if (from is not null)
        {
            var senderEmail = SafeString(mail.SenderEmailAddress);
            var senderName  = SafeString(mail.SenderName);
            if (!Contains(senderEmail, from) && !Contains(senderName, from))
                return false;
        }

        // ── To (recipient in To: field, partial) ──────────────────────────────
        var to = GetParam(p, "to");
        if (to is not null && !Contains(SafeString(mail.To), to))
            return false;

        // ── CC ────────────────────────────────────────────────────────────────
        var cc = GetParam(p, "cc");
        if (cc is not null && !Contains(SafeString(mail.CC), cc))
            return false;

        // ── Subject exact ─────────────────────────────────────────────────────
        var subject = GetParam(p, "subject");
        if (subject is not null &&
            !string.Equals(SafeString(mail.Subject), subject, StringComparison.OrdinalIgnoreCase))
            return false;

        // ── Subject contains (partial, case-insensitive) ───────────────────────
        var subjectContains = GetParam(p, "subject_contains");
        if (subjectContains is not null && !Contains(SafeString(mail.Subject), subjectContains))
            return false;

        // ── Body contains (slow — full-text scan; warn callers) ───────────────
        var bodyContains = GetParam(p, "body_contains");
        if (bodyContains is not null && !Contains(SafeString(mail.Body), bodyContains))
            return false;

        // ── Category ─────────────────────────────────────────────────────────
        var category = GetParam(p, "category");
        if (category is not null && !Contains(SafeString(mail.Categories), category))
            return false;

        return true;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string? GetParam(Dictionary<string, object?> p, string key) =>
        p.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static bool IsTrue(string? s)  => s is not null && bool.TryParse(s, out var b) && b;
    private static bool IsFalse(string? s) => s is not null && bool.TryParse(s, out var b) && !b;

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string SafeString(object? value)
    {
        try { return (string)(value ?? ""); } catch { return ""; }
    }
}

using System.Runtime.InteropServices;
using System.Text.Json;

namespace AutoFlow.Agent.Execution.Outlook;

/// <summary>
/// Converts Outlook COM MailItem objects to a stable JSON-serialisable dictionary
/// and back to identity tokens (EntryID + StoreID) for subsequent operations.
///
/// Variable format stored in ExecutionContext.Variables:
///   Single email  → JSON object   { entryId, storeId, subject, from, fromName, to, cc,
///                                   body, htmlBody, receivedAt, sentAt, isRead, isFlagged,
///                                   importance, hasAttachment, attachments[], categories }
///   Multiple emails → JSON array  [ {...}, {...}, … ]
/// </summary>
public static class EmailItemSerializer
{
    // ── Serialisation ─────────────────────────────────────────────────────────

    /// <summary>Serialises a COM MailItem to a JSON-safe dictionary.</summary>
    public static Dictionary<string, object?> Serialize(dynamic mail)
    {
        // Each COM property access is wrapped individually.
        // Dynamic COM objects can throw for non-standard items; a failure on one
        // field must never lose the rest of the email.

        string entryId    = ""; try { entryId    = (string)(mail.EntryID ?? ""); } catch { }
        string storeId    = ""; try { storeId    = (string)(mail.Parent.StoreID ?? ""); } catch { }
        string subject    = ""; try { subject    = (string)(mail.Subject ?? ""); } catch { }
        string from       = ""; try { from       = (string)(mail.SenderEmailAddress ?? ""); } catch { }
        string fromName   = ""; try { fromName   = (string)(mail.SenderName ?? ""); } catch { }
        string to         = ""; try { to         = (string)(mail.To  ?? ""); } catch { }
        string cc         = ""; try { cc         = (string)(mail.CC  ?? ""); } catch { }
        string bcc        = ""; try { bcc        = (string)(mail.BCC ?? ""); } catch { }
        string body       = ""; try { body       = (string)(mail.Body     ?? ""); } catch { }
        string htmlBody   = ""; try { htmlBody   = (string)(mail.HTMLBody ?? ""); } catch { }
        string receivedAt = ""; try { receivedAt = ((DateTime)mail.ReceivedTime).ToString("o"); } catch { }
        string sentAt     = ""; try { sentAt     = ((DateTime)mail.SentOn).ToString("o"); } catch { }
        bool   isRead     = false; try { isRead  = !(bool)mail.UnRead; } catch { }
        bool   isFlagged  = false; try { isFlagged = (int)mail.FlagStatus == 2; } catch { }
        string categories = ""; try { categories = (string)(mail.Categories ?? ""); } catch { }
        int    size       = 0;  try { size       = (int)mail.Size; } catch { }
        string convId     = ""; try { convId     = (string)(mail.ConversationID ?? ""); } catch { }
        string importance = "normal";
        try
        {
            importance = (int)mail.Importance switch { 2 => "high", 0 => "low", _ => "normal" };
        }
        catch { }

        var attachments = SerializeAttachments(mail);

        return new Dictionary<string, object?>
        {
            ["entryId"]          = entryId,
            ["storeId"]          = storeId,
            ["subject"]          = subject,
            ["from"]             = from,
            ["fromName"]         = fromName,
            ["to"]               = to,
            ["cc"]               = cc,
            ["bcc"]              = bcc,
            ["body"]             = body,
            ["htmlBody"]         = htmlBody,
            ["receivedAt"]       = receivedAt,
            ["sentAt"]           = sentAt,
            ["isRead"]           = isRead,
            ["isFlagged"]        = isFlagged,
            ["isHighImportance"] = importance == "high",
            ["importance"]       = importance,
            ["hasAttachment"]    = attachments.Count > 0,
            ["attachments"]      = attachments,
            ["categories"]       = categories,
            ["size"]             = size,
            ["conversationId"]   = convId,
        };
    }

    /// <summary>Converts a list of serialised items to a JSON string.</summary>
    public static string SerializeToJson(IList<Dictionary<string, object?>> items, bool singleObject)
    {
        if (singleObject && items.Count == 1)
            return JsonSerializer.Serialize(items[0]);

        return JsonSerializer.Serialize(items);
    }

    // ── Identity extraction ───────────────────────────────────────────────────

    /// <summary>
    /// Extracts the (entryId, storeId) pair from a variable JSON string.
    /// Handles both a single email object and an array (uses the first element).
    /// </summary>
    public static (string EntryId, string StoreId) ExtractIdentity(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var element = root.ValueKind == JsonValueKind.Array
            ? (root.GetArrayLength() > 0
                ? root[0]
                : throw new InvalidOperationException(
                    "The email variable is empty — no emails matched the filter."))
            : root;

        return ExtractIdentityFromElement(element);
    }

    /// <summary>
    /// Extracts all (entryId, storeId) pairs from a variable JSON string.
    /// Returns multiple pairs when the variable holds a JSON array.
    /// </summary>
    public static IReadOnlyList<(string EntryId, string StoreId)> ExtractAllIdentities(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            var list = new List<(string, string)>();
            foreach (var el in root.EnumerateArray())
                list.Add(ExtractIdentityFromElement(el));
            if (list.Count == 0)
                throw new InvalidOperationException("The email variable is empty.");
            return list;
        }

        return new[] { ExtractIdentityFromElement(root) };
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static (string, string) ExtractIdentityFromElement(JsonElement el)
    {
        var entryId = el.TryGetProperty("entryId", out var eid) ? eid.GetString() : null;
        if (string.IsNullOrWhiteSpace(entryId))
            throw new InvalidOperationException(
                "Email element is missing entryId. " +
                "Make sure the variable was populated by a read_email step.");

        var storeId = el.TryGetProperty("storeId", out var sid) ? sid.GetString() ?? "" : "";
        return (entryId!, storeId);
    }

    private static List<Dictionary<string, object?>> SerializeAttachments(dynamic mail)
    {
        var result = new List<Dictionary<string, object?>>();
        int count  = 0;
        try { count = (int)mail.Attachments.Count; } catch { return result; }

        for (var i = 1; i <= count; i++)
        {
            dynamic? att = null;
            try
            {
                att = mail.Attachments[i];
                string fn = ""; try { fn = (string)(att.FileName    ?? ""); } catch { }
                string dn = ""; try { dn = (string)(att.DisplayName ?? fn); } catch { dn = fn; }
                if (string.IsNullOrEmpty(fn)) fn = $"attachment_{i}";
                int    sz = 0;  try { sz = (int)att.Size; } catch { }
                result.Add(new Dictionary<string, object?> { ["filename"] = fn, ["displayName"] = dn, ["size"] = sz, ["index"] = i });
            }
            catch { }
            finally { if (att is not null) { try { Marshal.ReleaseComObject(att); } catch { } } }
        }

        return result;
    }
}

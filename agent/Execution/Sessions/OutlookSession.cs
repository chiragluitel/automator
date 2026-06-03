using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace AutoFlow.Agent.Execution.Sessions;

/// <summary>
/// Wraps a COM connection to classic Outlook (Win32).
/// New Outlook (web-based) has no COM Object Model and is explicitly not supported.
/// The session is shared across all Outlook handlers within a single run via ExecutionContext.Sessions.
/// </summary>
public sealed class OutlookSession : ISession
{
    public const string Key = "outlook";

    // olDefaultFolders constants
    private const int OlFolderDeletedItems = 3;
    private const int OlFolderOutbox       = 4;
    private const int OlFolderSentMail     = 5;
    private const int OlFolderInbox        = 6;
    private const int OlFolderDrafts       = 16;
    private const int OlFolderJunk         = 23;

    private readonly dynamic _application;
    private readonly dynamic _namespace;

    private OutlookSession(dynamic application, dynamic ns)
    {
        _application = application;
        _namespace   = ns;
    }

    /// <summary>Outlook.Application COM object — use to create mail items, access Explorer etc.</summary>
    public dynamic Application => _application;

    /// <summary>Outlook.NameSpace — use to resolve folders, get items by ID, access address book.</summary>
    public dynamic Namespace => _namespace;

    // ── Factory ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Connects to a running Outlook instance or launches one.
    /// Retries up to <paramref name="maxAttempts"/> times with 2-second gaps to allow startup time.
    /// </summary>
    public static async Task<OutlookSession> CreateAsync(
        CancellationToken ct = default,
        int maxAttempts = 3,
        ILogger? log = null)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "Outlook automation requires Windows. This agent must run on the user's Windows desktop.");

        Exception? last = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            if (attempt > 1)
            {
                log?.LogInformation("Waiting for Outlook to initialize (attempt {A}/{M})…", attempt, maxAttempts);
                await Task.Delay(2_000, ct);
            }

            try
            {
                var t = Type.GetTypeFromProgID("Outlook.Application")
                    ?? throw new InvalidOperationException(
                        "Outlook.Application ProgID not found. Classic Outlook (Win32) is not installed. " +
                        "New Outlook (web-based) is not supported — switch to classic Outlook from the toggle in the title bar.");

                dynamic app  = Activator.CreateInstance(t)!;
                dynamic ns   = app.GetNamespace("MAPI");

                // Verify connection by probing the folder tree — throws if MAPI is not ready.
                var _ = (int)ns.Folders.Count;

                return new OutlookSession(app, ns);
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                last = ex;
            }
        }

        throw new InvalidOperationException(
            $"Could not connect to Outlook after {maxAttempts} attempts. " +
            $"Make sure classic Outlook is installed and your profile is configured. " +
            $"Last error: {last?.Message}", last!);
    }

    // ── Folder resolution ────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a folder by name or slash-separated path.
    /// Examples: "Inbox", "Archive", "Archive/2024", "Projects/Q2".
    /// Named defaults (case-insensitive): Inbox, Sent Items, Drafts, Deleted Items,
    /// Outbox, Junk Email, Archive.
    /// </summary>
    public dynamic GetFolder(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return _namespace.GetDefaultFolder(OlFolderInbox);

        // Fast path for well-known folder names.
        var named = TryGetDefaultFolder(folderPath.Trim());
        if (named != null) return named;

        // Slash-separated path traversal starting from the mailbox root.
        // The root is the parent of the default Inbox (the store/mailbox).
        var parts = folderPath.Trim().Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

        dynamic root;
        try
        {
            root = _namespace.GetDefaultFolder(OlFolderInbox).Parent;
        }
        catch
        {
            root = _namespace.Folders[1]; // Fallback: first store
        }

        dynamic current = root;
        foreach (var part in parts)
        {
            // Check if this segment is a named default folder at current level.
            var defaultFolder = TryGetDefaultFolder(part);
            if (defaultFolder != null)
            {
                current = defaultFolder;
                continue;
            }

            var found = FindChildFolderByName(current.Folders, part);
            if (found == null)
                throw new InvalidOperationException(
                    $"Outlook folder '{part}' not found under '{(string)current.Name}'. " +
                    $"Check the folder name matches exactly as shown in Outlook.");
            current = found;
        }

        return current;
    }

    // ── Item lookup ──────────────────────────────────────────────────────────

    /// <summary>
    /// Retrieves an Outlook MailItem by its unique EntryID.
    /// The storeId (message store identifier) is optional but prevents ambiguity
    /// when the user has multiple mailboxes.
    /// </summary>
    public dynamic GetItemFromId(string entryId, string? storeId = null)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            throw new ArgumentException("entryId is required to look up an Outlook mail item.");
        try
        {
            return string.IsNullOrWhiteSpace(storeId)
                ? _namespace.GetItemFromID(entryId)
                : _namespace.GetItemFromID(entryId, storeId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "The email could not be found in Outlook. It may have been moved, deleted, or the " +
                "session variable is stale (from a previous run). Re-run read_email to refresh it.", ex);
        }
    }

    // ── ISession ─────────────────────────────────────────────────────────────

    public Task WaitForCloseIfNeededAsync(CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        try { Marshal.ReleaseComObject(_namespace);   } catch { }
        try { Marshal.ReleaseComObject(_application); } catch { }
        return ValueTask.CompletedTask;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private dynamic? TryGetDefaultFolder(string name) =>
        name.ToLowerInvariant() switch
        {
            "inbox"                             => _namespace.GetDefaultFolder(OlFolderInbox),
            "sent" or "sent items"              => _namespace.GetDefaultFolder(OlFolderSentMail),
            "drafts"                            => _namespace.GetDefaultFolder(OlFolderDrafts),
            "deleted" or "trash" or
            "deleted items"                     => _namespace.GetDefaultFolder(OlFolderDeletedItems),
            "outbox"                            => _namespace.GetDefaultFolder(OlFolderOutbox),
            "junk" or "junk email"              => _namespace.GetDefaultFolder(OlFolderJunk),
            "archive"                           => TryGetArchiveFolder(),
            _                                   => null
        };

    private dynamic? TryGetArchiveFolder()
    {
        // The Archive folder (online archive) is olFolderArchive = 46 in Outlook 2016+.
        // Fall back to a custom "Archive" folder if the built-in doesn't exist.
        try { return _namespace.GetDefaultFolder(46); }
        catch
        {
            var inbox = _namespace.GetDefaultFolder(OlFolderInbox);
            return FindChildFolderByName(inbox.Parent.Folders, "Archive");
        }
    }

    private static dynamic? FindChildFolderByName(dynamic folders, string name)
    {
        foreach (dynamic f in folders)
        {
            try
            {
                if (string.Equals((string)f.Name, name, StringComparison.OrdinalIgnoreCase))
                    return f;
            }
            catch { }
        }
        return null;
    }
}

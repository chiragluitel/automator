using ClosedXML.Excel;

namespace AutoFlow.Agent.Execution.Sessions;

/// <summary>
/// Wraps a ClosedXML workbook for the lifetime of a run.
/// Keyed in ExecutionContext.Sessions by the fully-resolved file path,
/// or by NewKey when created by open_application for a brand-new workbook.
/// </summary>
public sealed class OfficeSession : ISession
{
    public const string NewKey = "excel:new";

    private string? _filePath;
    private readonly XLWorkbook _workbook;
    private readonly bool _isNew;

    // Open an existing file.
    public OfficeSession(string filePath)
    {
        _filePath = Path.GetFullPath(filePath);
        _workbook = new XLWorkbook(_filePath);
        _isNew = false;
    }

    // New blank workbook — used when open_application launches Excel.
    private OfficeSession()
    {
        _filePath = null;
        _workbook = new XLWorkbook();
        _workbook.Worksheets.Add("Sheet1");
        _isNew = true;
    }

    public static OfficeSession CreateNew() => new();

    public IXLWorksheet GetWorksheet(string? sheetName)
    {
        if (sheetName is null)
            return _workbook.Worksheets.First();

        if (_workbook.TryGetWorksheet(sheetName, out var ws))
            return ws;

        // Auto-create sheets for new workbooks; throw for existing files (sheet name is probably a typo).
        if (_isNew)
            return _workbook.Worksheets.Add(sheetName);

        throw new InvalidOperationException(
            $"Worksheet '{sheetName}' not found in '{_filePath}'.");
    }

    // Saves in place unless a different path is supplied.
    // For new workbooks, path is required on the first save.
    public void Save(string? path = null)
    {
        var savePath = path is not null
            ? Path.GetFullPath(path)
            : _filePath ?? throw new InvalidOperationException(
                "New workbook has no save path. Provide a path in the save_file step params.");

        _workbook.SaveAs(savePath);
        _filePath = savePath; // remember for subsequent saves without a path
    }

    public Task WaitForCloseIfNeededAsync(CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _workbook.Dispose();
        return ValueTask.CompletedTask;
    }
}

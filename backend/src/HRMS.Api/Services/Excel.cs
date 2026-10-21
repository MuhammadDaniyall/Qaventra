using System.Globalization;
using ClosedXML.Excel;

namespace HRMS.Api.Services;

public record ExcelColumn<T>(string Header, Func<T, object?> Value, string? Format = null);

/// <summary>A data row read from an uploaded sheet: its Excel row number and cell text by header (case-insensitive).</summary>
public record ExcelRow(int Number, IReadOnlyDictionary<string, string> Cells)
{
    public string? Get(string header) => Cells.TryGetValue(header, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
}

/// <summary>Writes and reads simple one-table .xlsx files.</summary>
public static class Excel
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const int MaxUploadBytes = 5 * 1024 * 1024;

    /// <summary>One sheet with a bold, frozen, filterable header row. Dates and numbers stay real Excel values.</summary>
    public static byte[] Write<T>(string sheetName, IEnumerable<T> rows, params ExcelColumn<T>[] columns)
    {
        using var wb = new XLWorkbook();
        AddSheet(wb, sheetName, rows, columns);
        return Save(wb);
    }

    public static IXLWorksheet AddSheet<T>(XLWorkbook wb, string sheetName, IEnumerable<T> rows, params ExcelColumn<T>[] columns)
    {
        var ws = wb.Worksheets.Add(sheetName);
        for (var c = 0; c < columns.Length; c++)
            ws.Cell(1, c + 1).Value = columns[c].Header;
        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < columns.Length; c++)
            {
                var cell = ws.Cell(r, c + 1);
                SetValue(cell, columns[c].Value(row));
                if (columns[c].Format is { } f)
                    cell.Style.NumberFormat.Format = f;
            }
            r++;
        }
        StyleTable(ws, columns.Length, r - 1);
        return ws;
    }

    public static void StyleTable(IXLWorksheet ws, int columnCount, int lastRow)
    {
        var header = ws.Range(1, 1, 1, columnCount);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F46E5");
        ws.SheetView.FreezeRows(1);
        if (lastRow >= 1)
            ws.Range(1, 1, Math.Max(lastRow, 1), columnCount).SetAutoFilter();
        ws.Columns(1, columnCount).AdjustToContents(1, Math.Min(lastRow, 500), 10, 60);
    }

    public static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: cell.Value = Blank.Value; break;
            case DateOnly d: cell.Value = d.ToDateTime(TimeOnly.MinValue); cell.Style.NumberFormat.Format = "dd-mmm-yyyy"; break;
            case DateTime dt: cell.Value = dt; cell.Style.NumberFormat.Format = dt.TimeOfDay == TimeSpan.Zero ? "dd-mmm-yyyy" : "dd-mmm-yyyy hh:mm"; break;
            case TimeOnly t: cell.Value = t.ToTimeSpan(); cell.Style.NumberFormat.Format = "hh:mm"; break;
            case decimal m: cell.Value = m; cell.Style.NumberFormat.Format = "#,##0"; break;
            case int i: cell.Value = i; break;
            case double n: cell.Value = n; break;
            case bool b: cell.Value = b ? "Yes" : "No"; break;
            default: cell.Value = value.ToString(); break;
        }
    }

    /// <summary>Reads the first sheet: row 1 is headers, then every non-empty row. Throws for files that are not .xlsx.</summary>
    public static List<ExcelRow> Read(Stream stream, out List<string> headers)
    {
        XLWorkbook wb;
        try
        {
            wb = new XLWorkbook(stream);
        }
        catch (Exception)
        {
            throw new Domain.Common.DomainException("This is not an Excel (.xlsx) file. Download the template and fill that in.");
        }
        using (wb)
        {
            var ws = wb.Worksheets.First();
            var used = ws.RangeUsed();
            headers = [];
            var rows = new List<ExcelRow>();
            if (used is null)
                return rows;
            var lastCol = used.LastColumn().ColumnNumber();
            for (var c = 1; c <= lastCol; c++)
                headers.Add(Clean(ws.Cell(1, c).GetString()));
            for (var r = 2; r <= used.LastRow().RowNumber(); r++)
            {
                var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var any = false;
                for (var c = 1; c <= lastCol; c++)
                {
                    if (string.IsNullOrEmpty(headers[c - 1]))
                        continue;
                    var text = CellText(ws.Cell(r, c));
                    cells[headers[c - 1]] = text;
                    any |= !string.IsNullOrWhiteSpace(text);
                }
                if (any)
                    rows.Add(new ExcelRow(r, cells));
            }
            return rows;
        }
    }

    /// <summary>Header text without the "*" that marks required columns in templates.</summary>
    public static string Clean(string header) => header.Replace("*", "").Trim();

    private static string CellText(IXLCell cell) => cell.DataType switch
    {
        XLDataType.DateTime => DateText(cell.GetDateTime()),
        XLDataType.TimeSpan => TimeOnly.FromTimeSpan(cell.GetTimeSpan()).ToString("HH:mm", CultureInfo.InvariantCulture),
        XLDataType.Number => cell.GetDouble().ToString(CultureInfo.InvariantCulture),
        XLDataType.Boolean => cell.GetBoolean() ? "Yes" : "No",
        _ => cell.GetString(),
    };

    /// <summary>A date cell as yyyy-MM-dd; a time-only cell (Excel stores it on 1899-12-30) as HH:mm.</summary>
    private static string DateText(DateTime dt) =>
        dt.Year < 1901 ? dt.ToString("HH:mm", CultureInfo.InvariantCulture)
        : dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd-MMM-yyyy", "d-MMM-yyyy", "d MMM yyyy", "dd MMM yyyy", "MM/dd/yyyy"];

    /// <summary>Accepts real Excel dates and common typed formats (2026-10-21, 21/10/2026, 21-Oct-2026).</summary>
    public static bool TryDate(string? text, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        text = text.Trim();
        if (DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 20000 and < 80000)
        {
            date = DateOnly.FromDateTime(DateTime.FromOADate(serial));
            return true;
        }
        return false;
    }

    /// <summary>Accepts 09:05, 9:05, 9:05 AM, 17:30:00 and Excel time fractions.</summary>
    public static bool TryTime(string? text, out TimeOnly time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        text = text.Trim();
        if (TimeOnly.TryParseExact(text, ["HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss", "h:mm tt", "hh:mm tt", "h:mmtt", "hh:mm:ss tt"],
                CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out time))
            return true;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction) && fraction is >= 0 and < 1)
        {
            time = TimeOnly.FromTimeSpan(TimeSpan.FromDays(fraction));
            return true;
        }
        return false;
    }
}

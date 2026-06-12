using AccountingSaaS.Application.Interfaces;
using ClosedXML.Excel;

namespace AccountingSaaS.Infrastructure.Services;

public sealed class ExcelReaderService : IExcelReaderService
{
    public Task<ExcelReadResult> ReadWorksheetAsync(string filePath, string? worksheetName, int maxRows, int maxColumns, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = !string.IsNullOrWhiteSpace(worksheetName)
            ? workbook.Worksheets.Worksheet(worksheetName)
            : workbook.Worksheets.First();

        var usedRange = worksheet.RangeUsed();
        if (usedRange is null)
        {
            return Task.FromResult(new ExcelReadResult([], []));
        }

        var columnCount = Math.Min(usedRange.ColumnCount(), maxColumns);
        var rowCount = Math.Min(usedRange.RowCount(), maxRows + 1);
        var headers = new List<string>();
        for (var col = 1; col <= columnCount; col++)
        {
            var header = NormalizeHeader(worksheet.Cell(1, col).GetFormattedString());
            if (!string.IsNullOrWhiteSpace(header))
            {
                headers.Add(header);
            }
        }

        var rows = new List<ExcelRowData>();
        for (var row = 2; row <= rowCount; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var hasValue = false;
            for (var col = 1; col <= headers.Count; col++)
            {
                var cell = worksheet.Cell(row, col);
                var value = cell.HasFormula ? cell.CachedValue.ToString() : cell.GetFormattedString();
                value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                hasValue = hasValue || !string.IsNullOrWhiteSpace(value);
                values[headers[col - 1]] = value;
            }

            if (hasValue)
            {
                rows.Add(new ExcelRowData(row, values));
            }
        }

        return Task.FromResult(new ExcelReadResult(headers, rows));
    }

    private static string NormalizeHeader(string? header) => (header ?? string.Empty).Trim();
}

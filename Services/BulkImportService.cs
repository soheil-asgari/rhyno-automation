using System.Data;
using System.Globalization;
using System.Text;
using OfficeOpenXml;

namespace OfficeAutomation.Services;

/// <summary>
/// سرویس ورود داده‌های انبوه از فایل Excel/CSV
/// </summary>
public interface IBulkImportService
{
    /// <summary>خواندن فایل Excel و تبدیل به DataTable</summary>
    ImportResult<DataTable> ReadExcel(Stream fileStream, string fileName);

    /// <summary>خواندن فایل CSV و تبدیل به DataTable</summary>
    ImportResult<DataTable> ReadCsv(Stream fileStream, bool hasHeader = true, string delimiter = ",");

    /// <summary>اعتبارسنجی DataTable و بازگشت گزارش خطاها</summary>
    ValidationResult Validate(DataTable table, ImportValidationOptions? options = null);

    /// <summary>آماده‌سازی قالب Excel برای import</summary>
    byte[] GenerateExcelTemplate(string[] columns, string sheetName = "Sheet1");
}

/// <summary>
/// نتیجه عملیات Import
/// </summary>
public class ImportResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public List<ImportValidationError> ValidationErrors { get; set; } = new();
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }

    public static ImportResult<T> Ok(T data, int totalRows)
    {
        return new ImportResult<T>
        {
            Success = true,
            Data = data,
            TotalRows = totalRows,
            ValidRows = totalRows
        };
    }

    public static ImportResult<T> Fail(string error)
    {
        return new ImportResult<T>
        {
            Success = false,
            ErrorMessage = error
        };
    }
}

/// <summary>
/// خطای اعتبارسنجی
/// </summary>
public class ImportValidationError
{
    public int RowNumber { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? CellValue { get; set; }
}

/// <summary>
/// نتیجه اعتبارسنجی
/// </summary>
public class ValidationResult
{
    public bool IsValid => ValidationErrors.Count == 0;
    public List<ImportValidationError> ValidationErrors { get; set; } = new();
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public string Summary => $"تعداد کل ردیف‌ها: {TotalRows}, معتبر: {ValidRows}, نامعتبر: {InvalidRows}";
}

/// <summary>
/// گزینه‌های اعتبارسنجی داده‌ها
/// </summary>
public class ImportValidationOptions
{
    /// <summary>ستون‌های اجباری</summary>
    public List<string> RequiredColumns { get; set; } = new();

    /// <summary>حداکثر طول کاراکتر برای هر ستون (ستون -> max length)</summary>
    public Dictionary<string, int> MaxLengths { get; set; } = new();

    /// <summary>ستون‌های عددی که باید مقدار داشته باشند</summary>
    public List<string> NumericColumns { get; set; } = new();

    /// <summary>ستون‌های تاریخ</summary>
    public List<string> DateColumns { get; set; } = new();

    /// <summary>حداکثر تعداد ردیف</summary>
    public int MaxRows { get; set; } = 10000;
}

public sealed class BulkImportService : IBulkImportService
{
    private static readonly CultureInfo FaCulture = CultureInfo.GetCultureInfo("fa-IR");

    public ImportResult<DataTable> ReadExcel(Stream fileStream, string fileName)
    {
        try
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage(fileStream);
            var worksheet = package.Workbook.Worksheets[0];

            if (worksheet.Dimension == null)
            {
                return ImportResult<DataTable>.Fail("فایل Excel خالی است یا فرمت صحیحی ندارد.");
            }

            var table = new DataTable();
            var rowCount = worksheet.Dimension.Rows;
            var colCount = worksheet.Dimension.Columns;

            // Read headers (row 1)
            for (int col = 1; col <= colCount; col++)
            {
                var headerValue = worksheet.Cells[1, col].Text?.Trim();
                if (string.IsNullOrWhiteSpace(headerValue))
                {
                    headerValue = $"ستون {col}";
                }
                table.Columns.Add(headerValue);
            }

            // Read data rows
            for (int row = 2; row <= rowCount; row++)
            {
                var dataRow = table.NewRow();
                bool hasData = false;

                for (int col = 1; col <= colCount; col++)
                {
                    var cellValue = worksheet.Cells[row, col].Value;
                    dataRow[col - 1] = cellValue ?? DBNull.Value;
                    if (cellValue != null && !string.IsNullOrWhiteSpace(cellValue.ToString()))
                    {
                        hasData = true;
                    }
                }

                if (hasData)
                {
                    table.Rows.Add(dataRow);
                }
            }

            return ImportResult<DataTable>.Ok(table, table.Rows.Count);
        }
        catch (Exception ex)
        {
            return ImportResult<DataTable>.Fail($"خطا در خواندن فایل Excel: {ex.Message}");
        }
    }

    public ImportResult<DataTable> ReadCsv(Stream fileStream, bool hasHeader = true, string delimiter = ",")
    {
        try
        {
            using var reader = new StreamReader(fileStream, Encoding.UTF8);
            var table = new DataTable();
            var errors = new List<ImportValidationError>();
            int lineNumber = 0;

            string? headerLine = null;
            if (hasHeader)
            {
                headerLine = reader.ReadLine();
                lineNumber++;
            }

            if (headerLine == null && hasHeader)
            {
                return ImportResult<DataTable>.Fail("فایل CSV خالی است.");
            }

            // Parse headers
            var headerFields = hasHeader ? ParseCsvLine(headerLine!, delimiter) : Array.Empty<string>();
            if (hasHeader && headerFields.Length > 0)
            {
                foreach (var header in headerFields)
                {
                    table.Columns.Add(header.Trim());
                }
            }

            // Parse data lines
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = ParseCsvLine(line, delimiter);

                // Auto-detect columns if no header
                if (!hasHeader && table.Columns.Count == 0)
                {
                    for (int j = 0; j < fields.Length; j++)
                    {
                        table.Columns.Add($"ستون {j + 1}");
                    }
                }

                var row = table.NewRow();
                for (int i = 0; i < Math.Min(fields.Length, table.Columns.Count); i++)
                {
                    row[i] = fields[i].Trim();
                }
                table.Rows.Add(row);
            }

            return ImportResult<DataTable>.Ok(table, table.Rows.Count);
        }
        catch (Exception ex)
        {
            return ImportResult<DataTable>.Fail($"خطا در خواندن فایل CSV: {ex.Message}");
        }
    }

    public ValidationResult Validate(DataTable table, ImportValidationOptions? options = null)
    {
        options ??= new ImportValidationOptions();
        var result = new ValidationResult { TotalRows = table.Rows.Count };

        // Check max rows
        if (table.Rows.Count > options.MaxRows)
        {
            result.ValidationErrors.Add(new ImportValidationError
            {
                RowNumber = 0,
                ColumnName = "فایل",
                ErrorMessage = $"تعداد ردیف‌ها ({table.Rows.Count}) بیشتر از حد مجاز ({options.MaxRows}) است.",
                CellValue = table.Rows.Count.ToString()
            });
            result.InvalidRows = table.Rows.Count;
            return result;
        }

        for (int rowIdx = 0; rowIdx < table.Rows.Count; rowIdx++)
        {
            var row = table.Rows[rowIdx];
            var rowNumber = rowIdx + 2; // +2 because row 1 is header
            bool rowHasError = false;

            // Check required columns
            foreach (var colName in options.RequiredColumns)
            {
                if (!table.Columns.Contains(colName))
                {
                    result.ValidationErrors.Add(new ImportValidationError
                    {
                        RowNumber = rowNumber,
                        ColumnName = colName,
                        ErrorMessage = $"ستون اجباری '{colName}' در فایل وجود ندارد.",
                    });
                    rowHasError = true;
                    continue;
                }

                var value = row[colName]?.ToString();
                if (string.IsNullOrWhiteSpace(value))
                {
                    result.ValidationErrors.Add(new ImportValidationError
                    {
                        RowNumber = rowNumber,
                        ColumnName = colName,
                        ErrorMessage = $"مقدار ستون '{colName}' الزامی است.",
                    });
                    rowHasError = true;
                }
            }

            // Check max lengths
            foreach (var maxLen in options.MaxLengths)
            {
                if (!table.Columns.Contains(maxLen.Key)) continue;
                var value = row[maxLen.Key]?.ToString() ?? "";
                if (value.Length > maxLen.Value)
                {
                    result.ValidationErrors.Add(new ImportValidationError
                    {
                        RowNumber = rowNumber,
                        ColumnName = maxLen.Key,
                        ErrorMessage = $"طول مقدار ({value.Length}) بیشتر از حد مجاز ({maxLen.Value}) است.",
                        CellValue = value.Length > 50 ? value[..50] + "..." : value
                    });
                    rowHasError = true;
                }
            }

            // Check numeric columns
            foreach (var colName in options.NumericColumns)
            {
                if (!table.Columns.Contains(colName)) continue;
                var value = row[colName]?.ToString();
                if (!string.IsNullOrWhiteSpace(value) && !decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                {
                    // Try Persian number parsing
                    var normalized = NormalizePersianNumbers(value);
                    if (!decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    {
                        result.ValidationErrors.Add(new ImportValidationError
                        {
                            RowNumber = rowNumber,
                            ColumnName = colName,
                            ErrorMessage = $"مقدار '{value}' یک عدد معتبر نیست.",
                            CellValue = value
                        });
                        rowHasError = true;
                    }
                }
            }

            // Check date columns
            foreach (var colName in options.DateColumns)
            {
                if (!table.Columns.Contains(colName)) continue;
                var value = row[colName]?.ToString();
                if (!string.IsNullOrWhiteSpace(value) && !DateTime.TryParse(value, FaCulture, DateTimeStyles.None, out _))
                {
                    result.ValidationErrors.Add(new ImportValidationError
                    {
                        RowNumber = rowNumber,
                        ColumnName = colName,
                        ErrorMessage = $"مقدار '{value}' یک تاریخ معتبر نیست.",
                        CellValue = value
                    });
                    rowHasError = true;
                }
            }

            if (rowHasError)
            {
                result.InvalidRows++;
            }
            else
            {
                result.ValidRows++;
            }
        }

        return result;
    }

    public byte[] GenerateExcelTemplate(string[] columns, string sheetName = "Sheet1")
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add(sheetName);

        for (int i = 0; i < columns.Length; i++)
        {
            var cell = worksheet.Cells[1, i + 1];
            cell.Value = columns[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(240, 253, 250));
            cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
            cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
        }

        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        worksheet.View.RightToLeft = true;

        // Add sample row
        for (int i = 0; i < columns.Length; i++)
        {
            worksheet.Cells[2, i + 1].Value = $"مثال {columns[i]}";
            worksheet.Cells[2, i + 1].Style.Font.Italic = true;
            worksheet.Cells[2, i + 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
        }

        return package.GetAsByteArray();
    }

    private static string[] ParseCsvLine(string line, string delimiter)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++; // Skip escaped quote
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c.ToString() == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    private static string NormalizePersianNumbers(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var sb = new StringBuilder(input);
        sb.Replace("۰", "0").Replace("۱", "1").Replace("۲", "2")
          .Replace("۳", "3").Replace("۴", "4").Replace("۵", "5")
          .Replace("۶", "6").Replace("۷", "7").Replace("۸", "8")
          .Replace("۹", "9").Replace("،", ",");
        return sb.ToString();
    }
}
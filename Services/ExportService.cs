using System.Data;
using System.Globalization;
using System.Text;
using OfficeOpenXml;

namespace OfficeAutomation.Services;

/// <summary>
/// سرویس یکپارچه خروجی‌گیری - پشتیبانی از Excel, PDF, CSV
/// </summary>
public interface IExportService
{
    /// <summary>خروجی DataTable به Excel</summary>
    byte[] ExportToExcel(DataTable table, string? sheetName = null);

    /// <summary>خروجی DataTable به CSV</summary>
    byte[] ExportToCsv(DataTable table, string delimiter = ",");

    /// <summary>خروجی DataTable به PDF (HTML-based)</summary>
    byte[] ExportToPdf(DataTable table, string title = "گزارش");

    /// <summary>خروجی لیست اشیاء به Excel با استفاده از reflection</summary>
    byte[] ExportToExcel<T>(IEnumerable<T> data, string? sheetName = null);

    /// <summary>خروجی لیست اشیاء به CSV</summary>
    byte[] ExportToCsv<T>(IEnumerable<T> data, string delimiter = ",");
}

public sealed class ExportService : IExportService
{
    private static readonly CultureInfo FaCulture = CultureInfo.GetCultureInfo("fa-IR");

    public byte[] ExportToExcel(DataTable table, string? sheetName = null)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add(sheetName ?? "Sheet1");

        // Write headers
        for (int col = 0; col < table.Columns.Count; col++)
        {
            var cell = worksheet.Cells[1, col + 1];
            cell.Value = table.Columns[col].ColumnName;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(240, 253, 250));
            cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
            cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
        }

        // Write data
        for (int row = 0; row < table.Rows.Count; row++)
        {
            for (int col = 0; col < table.Columns.Count; col++)
            {
                var cell = worksheet.Cells[row + 2, col + 1];
                var value = table.Rows[row][col];
                cell.Value = value;
                cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
            }
        }

        // Auto-fit columns
        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

        // RTL for Persian
        worksheet.View.RightToLeft = true;

        return package.GetAsByteArray();
    }

    public byte[] ExportToExcel<T>(IEnumerable<T> data, string? sheetName = null)
    {
        var table = ConvertToDataTable(data);
        return ExportToExcel(table, sheetName);
    }

    public byte[] ExportToCsv(DataTable table, string delimiter = ",")
    {
        var sb = new StringBuilder();

        // BOM for UTF-8
        sb.Append('\uFEFF');

        // Write headers
        var headers = table.Columns.Cast<DataColumn>().Select(c => QuoteCsvField(c.ColumnName, delimiter));
        sb.AppendLine(string.Join(delimiter, headers));

        // Write rows
        foreach (DataRow row in table.Rows)
        {
            var fields = row.ItemArray.Select(v => QuoteCsvField(v?.ToString() ?? "", delimiter));
            sb.AppendLine(string.Join(delimiter, fields));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public byte[] ExportToCsv<T>(IEnumerable<T> data, string delimiter = ",")
    {
        var table = ConvertToDataTable(data);
        return ExportToCsv(table, delimiter);
    }

    public byte[] ExportToPdf(DataTable table, string title = "گزارش")
    {
        // HTML-based PDF generation using simple HTML to byte array
        // In production, use a proper PDF library like QuestPDF, iTextSharp, or PuppeteerSharp
        var html = BuildPdfHtml(table, title);

        // Return HTML as bytes - caller can use a proper PDF renderer
        // For now, return an HTML file that browsers can print to PDF
        return Encoding.UTF8.GetBytes(html);
    }

    private DataTable ConvertToDataTable<T>(IEnumerable<T> data)
    {
        var table = new DataTable();
        var properties = typeof(T).GetProperties()
            .Where(p => p.CanRead)
            .ToList();

        foreach (var prop in properties)
        {
            var columnType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            table.Columns.Add(prop.Name, columnType);
        }

        foreach (var item in data)
        {
            var row = table.NewRow();
            foreach (var prop in properties)
            {
                var value = prop.GetValue(item);
                row[prop.Name] = value ?? DBNull.Value;
            }
            table.Rows.Add(row);
        }

        return table;
    }

    private static string QuoteCsvField(string field, string delimiter)
    {
        if (field.Contains(delimiter) || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    private static string BuildPdfHtml(DataTable table, string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='fa' dir='rtl'>");
        sb.AppendLine("<head><meta charset='utf-8'><title>" + title + "</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("@page { size: A4 landscape; margin: 1cm; }");
        sb.AppendLine("body { font-family: Tahoma, Arial, sans-serif; font-size: 11px; direction: rtl; }");
        sb.AppendLine("h1 { text-align: center; font-size: 16px; margin-bottom: 10px; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; }");
        sb.AppendLine("th { background: #f0fdfa; border: 1px solid #cbd5e1; padding: 6px 8px; font-weight: bold; text-align: center; }");
        sb.AppendLine("td { border: 1px solid #cbd5e1; padding: 4px 8px; text-align: right; }");
        sb.AppendLine("tr:nth-child(even) { background: #f8fafc; }");
        sb.AppendLine(".footer { text-align: center; margin-top: 15px; font-size: 9px; color: #94a3b8; }");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine("<h1>" + title + "</h1>");
        sb.AppendLine("<table><thead><tr>");

        foreach (DataColumn col in table.Columns)
        {
            sb.AppendLine("<th>" + System.Net.WebUtility.HtmlEncode(col.ColumnName) + "</th>");
        }

        sb.AppendLine("</tr></thead><tbody>");

        foreach (DataRow row in table.Rows)
        {
            sb.Append("<tr>");
            foreach (var value in row.ItemArray)
            {
                sb.Append("<td>" + System.Net.WebUtility.HtmlEncode(value?.ToString() ?? "") + "</td>");
            }
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table>");
        sb.AppendLine("<div class='footer'>تولید شده توسط راینو ERP - " + DateTime.Now.ToString("yyyy/MM/dd HH:mm", FaCulture) + "</div>");
        sb.AppendLine("</body></html>");

        return sb.ToString();
    }
}
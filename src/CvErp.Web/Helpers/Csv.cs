using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace CvErp.Web.Helpers;

public static class Csv
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Xuất CSV UTF-8 có BOM, dấu phân cách ';' để mở trực tiếp bằng Excel tiếng Việt.</summary>
    public static FileContentResult Export(string fileName, IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(';', headers.Select(Escape)));

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(';', row.Select(Format)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
        return new FileContentResult(bytes, "text/csv; charset=utf-8")
        {
            FileDownloadName = fileName
        };
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        decimal number => Escape(number.ToString("0.####", Vi)),
        double number => Escape(number.ToString("0.####", Vi)),
        DateOnly date => date.ToString("dd/MM/yyyy", Vi),
        DateTime date => date.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Vi),
        bool flag => flag ? "Có" : "Không",
        _ => Escape(value.ToString() ?? string.Empty)
    };

    private static string Escape(string value)
    {
        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}

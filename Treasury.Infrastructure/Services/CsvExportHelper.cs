using System.Globalization;
using System.Text;

namespace Treasury.Infrastructure.Services;

internal static class CsvExportHelper
{
    public static string Escape(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var text =
            value switch
            {
                DateTime dateTime =>
                    dateTime.ToString("O", CultureInfo.InvariantCulture),

                DateTimeOffset dateTimeOffset =>
                    dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),

                IFormattable formattable =>
                    formattable.ToString(null, CultureInfo.InvariantCulture),

                _ =>
                    value.ToString() ?? string.Empty
            };

        text =
            text
                .Replace("\r\n", " ")
                .Replace("\n", " ")
                .Replace("\r", " ");

        var shouldQuote =
            text.Contains(',') ||
            text.Contains('"');

        text =
            text.Replace("\"", "\"\"");

        return shouldQuote
            ? $"\"{text}\""
            : text;
    }

    public static byte[] ToUtf8Bytes(string csv)
    {
        /*
         * UTF-8 BOM helps Excel open the file correctly.
         */
        return new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: true)
            .GetBytes(csv);
    }
}
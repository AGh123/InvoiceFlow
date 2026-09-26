using System.Globalization;

namespace InvoiceFlow.Web.Components.Invoices;

public static class InvoiceCurrencyFormatter
{
    public static string Format(decimal amount, string? currencyCode)
    {
        var prefix = (currencyCode ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "USD" => "$",
            "EUR" => "€",
            "GBP" => "£",
            "JPY" => "¥",
            var code when code.Length == 3 => $"{code} ",
            _ => string.Empty,
        };

        return $"{prefix}{amount.ToString("N2", CultureInfo.InvariantCulture)}";
    }
}

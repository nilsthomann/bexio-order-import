using System;
using System.Text.RegularExpressions;

namespace BexioOrderImport.Application.Helpers;

public static class IdNormalizationHelper
{
    // CustomerId pattern: Optional '#' followed by optional leading zeros and digits.
    private static readonly Regex CustomerIdRegex = new(
        @"^\s*#?0*([1-9]\d*)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // OrderId pattern: Optional 'AU-' or '#' (or combined) followed by optional leading zeros and digits.
    private static readonly Regex OrderIdRegex = new(
        @"^\s*(?:AU-?|#)*0*([1-9]\d*)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Normalizes raw Excel cell content into a Customer ID integer.
    /// Allowed formats: "2", "#2", "00002", "#00002".
    /// Disallowed formats: "BL-002", "AU-00002", "2A", etc. return null.
    /// </summary>
    public static int? NormalizeCustomerId(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput)) return null;

        var match = CustomerIdRegex.Match(rawInput);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int id) && id > 0)
        {
            return id;
        }

        return null;
    }

    /// <summary>
    /// Normalizes raw Excel cell content into an Order ID integer.
    /// Allowed formats: "2", "#2", "00002", "#00002", "AU-2", "AU-00002", "#AU-00002".
    /// Disallowed formats: "BL-002", "XY-002", "2A", etc. return null.
    /// </summary>
    public static int? NormalizeOrderId(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput)) return null;

        var match = OrderIdRegex.Match(rawInput);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int id) && id > 0)
        {
            return id;
        }

        return null;
    }
}

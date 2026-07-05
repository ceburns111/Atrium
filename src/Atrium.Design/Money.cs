using System.Globalization;

namespace Atrium.Design;

/// <summary>
/// The single way money is rendered across the portal: <c>$1,234.56</c> — always with cents, always
/// the en-US pattern regardless of server culture. Prices are <c>DECIMAL(10,2)</c> end to end, so a
/// rounding format ("N0") silently lies about cents; every module formats through here instead of
/// hand-rolling a <c>$</c> next to a culture-sensitive number.
/// </summary>
public static class Money
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    public static string Format(decimal amount) => amount.ToString("C2", Culture);
}
